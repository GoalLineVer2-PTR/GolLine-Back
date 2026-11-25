using GoalLineBackend.Hubs;
using GoalLineBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

[Route("api/[controller]")]
[ApiController]
public class EstadoVARController : ControllerBase
{
    private static readonly object _lock = new();
    private static string estadoActual = "idle";
    private static long _seq = 1;
    private static float? _velocidad;
    private static float? _porcentaje;
    private static string? _detalle;
    private static DateTime _updatedUtc = DateTime.UtcNow;

    private readonly IHubContext<GoalHub> _hubContext;
    private readonly VarStreamRelayService _relay;

    public EstadoVARController(IHubContext<GoalHub> hubContext, VarStreamRelayService relay)
    {
        _hubContext = hubContext;
        _relay = relay;
    }

    public static object SetEstado(string nuevoEstado, float? velocidad = null, float? porcentaje = null, string? detalle = null)
    {
        lock (_lock)
        {
            if (nuevoEstado == "idle" || nuevoEstado == "gol" || nuevoEstado == "nogol")
            {
                estadoActual = nuevoEstado;
                _seq++;
                _updatedUtc = DateTime.UtcNow;
                if (nuevoEstado == "idle")
                {
                    _velocidad = null;
                    _porcentaje = null;
                    _detalle = null;
                }
                else
                {
                    if (velocidad.HasValue) _velocidad = velocidad;
                    if (porcentaje.HasValue) _porcentaje = porcentaje;
                    if (!string.IsNullOrEmpty(detalle)) _detalle = detalle;
                }
            }
            return BuildSnapshotUnsafe();
        }
    }

    private static object BuildSnapshotUnsafe() => new
    {
        estado = estadoActual,
        seq = _seq,
        velocidad = _velocidad,
        porcentajeCruzoLinea = _porcentaje,
        resultadoDetalle = _detalle,
        timestampUtc = _updatedUtc
    };

    // 1. EL GET (Lo que usa el Cartel Estadio como fallback de sincronización)
    [HttpGet]
    public IActionResult ObtenerEstado()
    {
        lock (_lock)
        {
            return Ok(BuildSnapshotUnsafe());
        }
    }

    // 2. EL POST (Lo que usa Godot 4 para notificar instantáneamente gol, nogol o idle)
    [HttpPost]
    public async Task<IActionResult> ActualizarEstado([FromBody] EstadoVARRequest request)
    {
        if (request.estado == "idle" || request.estado == "gol" || request.estado == "nogol")
        {
            var snap = SetEstado(request.estado, request.velocidad, request.porcentajeCruzoLinea, request.resultadoDetalle);
            _relay.BroadcastVarState(snap);
            await _hubContext.Clients.All.SendAsync("EstadoVARActualizado", snap);
            return Ok(snap);
        }
        return BadRequest("Estado inválido.");
    }
}

public class EstadoVARRequest
{
    public string estado { get; set; } = "idle";
    public float? velocidad { get; set; }
    public float? porcentajeCruzoLinea { get; set; }
    public string? resultadoDetalle { get; set; }
}
