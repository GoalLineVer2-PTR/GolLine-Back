using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace GoalLineBackend.Services;

public sealed record VarOutboundMessage(WebSocketMessageType Type, byte[] Payload);

public sealed class VarStreamRelayService
{
    private readonly ConcurrentDictionary<Guid, Channel<VarOutboundMessage>> _monitors = new();
    private readonly ConcurrentDictionary<Guid, Channel<byte[]>> _varStateClients = new();
    private readonly SemaphoreSlim _godotSendLock = new(1, 1);

    private WebSocket? _godotSocket;
    private readonly byte[]?[] _latestFrames = new byte[4][];
    private string? _latestTelemetryJson;
    private byte[]? _latestVarStateBytes;

    private long _framesReceived;
    private long _framesWindowCount;
    private DateTime _fpsWindowStart = DateTime.UtcNow;
    private double _fpsEstimate;
    private DateTime? _lastFrameUtc;

    private static readonly string[] DefaultCameraLabels =
    {
        "CAM 1 • EJECUTOR",
        "CAM 2 • LÍNEA DE GOL",
        "CAM 3 • CENITAL HAWK-EYE",
        "CAM 4 • DETRÁS DEL ARCO"
    };

    public bool IsGodotConnected => _godotSocket is { State: WebSocketState.Open };

    public object GetStatus()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _fpsWindowStart).TotalSeconds;
        if (elapsed >= 3.0)
        {
            _fpsEstimate = 0.0;
        }

        return new
        {
            godotConnected = IsGodotConnected,
            monitorClients = _monitors.Count,
            framesReceived = Interlocked.Read(ref _framesReceived),
            fpsEstimate = Math.Round(_fpsEstimate, 1),
            lastFrameUtc = _lastFrameUtc,
            cameraLabels = DefaultCameraLabels
        };
    }

    public async Task HandleGodotConnectionAsync(WebSocket socket, CancellationToken ct)
    {
        var previous = Interlocked.Exchange(ref _godotSocket, socket);
        if (previous is { State: WebSocketState.Open })
        {
            try
            {
                await previous.CloseAsync(WebSocketCloseStatus.NormalClosure, "Replaced by new Godot connection", CancellationToken.None);
            }
            catch { /* ignore */ }
        }

        BroadcastStatusEvent();

        var buffer = new byte[256 * 1024];
        using var ms = new MemoryStream();

        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage && ms.Length < 2 * 1024 * 1024);

                if (ms.Length == 0)
                {
                    continue;
                }

                var payload = ms.ToArray();

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (payload.Length > 1 && payload[0] < _latestFrames.Length)
                    {
                        _latestFrames[payload[0]] = payload;
                    }

                    Interlocked.Increment(ref _framesReceived);
                    _lastFrameUtc = DateTime.UtcNow;
                    UpdateFpsWindow();

                    BroadcastToMonitors(new VarOutboundMessage(WebSocketMessageType.Binary, payload));
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    _latestTelemetryJson = Encoding.UTF8.GetString(payload);
                    BroadcastToMonitors(new VarOutboundMessage(WebSocketMessageType.Text, payload));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            Interlocked.CompareExchange(ref _godotSocket, null, socket);
            BroadcastStatusEvent();
        }
    }

    public async Task HandleMonitorConnectionAsync(WebSocket socket, CancellationToken ct)
    {
        var clientId = Guid.NewGuid();
        var channel = Channel.CreateBounded<VarOutboundMessage>(new BoundedChannelOptions(12)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _monitors[clientId] = channel;

        // Enviar estado inicial, última telemetría y últimos frames disponibles
        EnqueueStatus(channel);
        if (!string.IsNullOrEmpty(_latestTelemetryJson))
        {
            channel.Writer.TryWrite(new VarOutboundMessage(
                WebSocketMessageType.Text,
                Encoding.UTF8.GetBytes(_latestTelemetryJson)));
        }

        for (int i = 0; i < _latestFrames.Length; i++)
        {
            var frame = _latestFrames[i];
            if (frame is not null)
            {
                channel.Writer.TryWrite(new VarOutboundMessage(WebSocketMessageType.Binary, frame));
            }
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sendTask = PumpMonitorOutboundAsync(socket, channel.Reader, linkedCts.Token);
        var recvTask = ReadMonitorCommandsAsync(socket, linkedCts.Token);

        await Task.WhenAny(sendTask, recvTask);
        linkedCts.Cancel();

        _monitors.TryRemove(clientId, out _);
        channel.Writer.TryComplete();

        try { await Task.WhenAll(sendTask, recvTask); } catch { /* ignore */ }
    }

    public void BroadcastVarState(object snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot);
        var bytes = Encoding.UTF8.GetBytes(json);
        _latestVarStateBytes = bytes;
        foreach (var kvp in _varStateClients)
        {
            kvp.Value.Writer.TryWrite(bytes);
        }
    }

    public void BroadcastNewShot(object disparo)
    {
        var json = JsonSerializer.Serialize(new { type = "new_shot", shot = disparo });
        BroadcastToMonitors(new VarOutboundMessage(WebSocketMessageType.Text, Encoding.UTF8.GetBytes(json)));
    }

    public async Task HandleVarStateConnectionAsync(WebSocket socket, CancellationToken ct)
    {
        var clientId = Guid.NewGuid();
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(8)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _varStateClients[clientId] = channel;

        if (_latestVarStateBytes is not null)
        {
            channel.Writer.TryWrite(_latestVarStateBytes);
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sendTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var payload in channel.Reader.ReadAllAsync(linkedCts.Token))
                {
                    if (socket.State != WebSocketState.Open) break;
                    await socket.SendAsync(payload, WebSocketMessageType.Text, true, linkedCts.Token);
                }
            }
            catch { /* ignore */ }
        }, linkedCts.Token);

        var recvTask = Task.Run(async () =>
        {
            var buf = new byte[256];
            try
            {
                while (socket.State == WebSocketState.Open && !linkedCts.Token.IsCancellationRequested)
                {
                    var r = await socket.ReceiveAsync(buf, linkedCts.Token);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch { /* ignore */ }
        }, linkedCts.Token);

        await Task.WhenAny(sendTask, recvTask);
        linkedCts.Cancel();
        _varStateClients.TryRemove(clientId, out _);
        channel.Writer.TryComplete();
        try { await Task.WhenAll(sendTask, recvTask); } catch { /* ignore */ }
    }

    public async Task<bool> SendCommandToGodotAsync(string jsonCommand, CancellationToken ct = default)
    {
        var socket = _godotSocket;
        if (socket is not { State: WebSocketState.Open })
        {
            return false;
        }

        var bytes = Encoding.UTF8.GetBytes(jsonCommand);
        await _godotSendLock.WaitAsync(ct);
        try
        {
            if (socket.State != WebSocketState.Open)
            {
                return false;
            }
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _godotSendLock.Release();
        }
    }

    private void UpdateFpsWindow()
    {
        var count = Interlocked.Increment(ref _framesWindowCount);
        var now = DateTime.UtcNow;
        var elapsed = (now - _fpsWindowStart).TotalSeconds;
        if (elapsed >= 1.0)
        {
            _fpsEstimate = count / elapsed;
            Interlocked.Exchange(ref _framesWindowCount, 0);
            _fpsWindowStart = now;
        }
    }

    private void BroadcastToMonitors(VarOutboundMessage msg)
    {
        foreach (var kvp in _monitors)
        {
            kvp.Value.Writer.TryWrite(msg);
        }
    }

    private void BroadcastStatusEvent()
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "status",
            godotConnected = IsGodotConnected,
            monitorClients = _monitors.Count
        });
        BroadcastToMonitors(new VarOutboundMessage(WebSocketMessageType.Text, Encoding.UTF8.GetBytes(json)));
    }

    private void EnqueueStatus(Channel<VarOutboundMessage> channel)
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "status",
            godotConnected = IsGodotConnected,
            monitorClients = _monitors.Count
        });
        channel.Writer.TryWrite(new VarOutboundMessage(WebSocketMessageType.Text, Encoding.UTF8.GetBytes(json)));
    }

    private static async Task PumpMonitorOutboundAsync(
        WebSocket socket,
        ChannelReader<VarOutboundMessage> reader,
        CancellationToken ct)
    {
        try
        {
            await foreach (var msg in reader.ReadAllAsync(ct))
            {
                if (socket.State != WebSocketState.Open)
                {
                    break;
                }
                await socket.SendAsync(msg.Payload, msg.Type, true, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    private async Task ReadMonitorCommandsAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();

        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage && ms.Length < 128 * 1024);

                if (result.MessageType == WebSocketMessageType.Text && ms.Length > 0)
                {
                    var cmdJson = Encoding.UTF8.GetString(ms.ToArray());
                    await SendCommandToGodotAsync(cmdJson, ct);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }
}
