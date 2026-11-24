using GoalLineBackend.Hubs;
using GoalLineBackend.Models;
using GoalLineBackend.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// En local (--no-launch-profile) escucha en 0.0.0.0:8080; en Docker/Azure respeta ASPNETCORE_URLS
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
    string.IsNullOrEmpty(builder.Configuration["Urls"]))
{
    builder.WebHost.UseUrls("http://0.0.0.0:8080");
}

// Forwarded Headers para proxies reversos (Nginx en Docker, Azure Front Door / Container Apps)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// CORS configurable para Local, Docker Compose y Azure Static Web Apps (compatible con SignalR)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

// Configurar opciones de MongoDB (sobreescribible por variables de entorno MongoDBSettings__*)
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDBSettings"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

// Servicio Relay WebSocket para streaming multicámara 4x de Godot 4 y control remoto VAR
builder.Services.AddSingleton<VarStreamRelayService>();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(20)
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});

app.UseAuthorization();

// Healthcheck para Docker Compose y Azure Container Apps
app.MapGet("/health", (VarStreamRelayService relay) => Results.Ok(new
{
    status = "healthy",
    service = "GoalLineBackend",
    godotConnected = relay.IsGodotConnected,
    timestampUtc = DateTime.UtcNow
}));

// WebSocket de ingesta desde Godot 4 (frames 4x + telemetría -> comandos remotos VAR)
app.Map("/ws/godot", async (HttpContext ctx, VarStreamRelayService relay) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await relay.HandleGodotConnectionAsync(ws, ctx.RequestAborted);
});

// WebSocket de monitoreo para navegadores de la Cabina VAR
app.Map("/ws/var-monitor", async (HttpContext ctx, VarStreamRelayService relay) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await relay.HandleMonitorConnectionAsync(ws, ctx.RequestAborted);
});

app.MapControllers();
app.MapHub<GoalHub>("/goalHub");

app.Run();
