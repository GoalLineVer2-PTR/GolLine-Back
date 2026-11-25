using GoalLineBackend.Hubs;
using GoalLineBackend.Models;
using GoalLineBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GoalLineBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DisparosController : ControllerBase
{
    private readonly IMongoCollection<Disparo> _disparosCollection;
    private readonly IHubContext<GoalHub> _hubContext;
    private readonly VarStreamRelayService _relay;

    public DisparosController(
        IOptions<MongoDBSettings> mongoDBSettings,
        IMongoClient mongoClient,
        IHubContext<GoalHub> hubContext,
        VarStreamRelayService relay)
    {
        var mongoDatabase = mongoClient.GetDatabase(mongoDBSettings.Value.DatabaseName);
        _disparosCollection = mongoDatabase.GetCollection<Disparo>(mongoDBSettings.Value.CollectionName);
        _hubContext = hubContext;
        _relay = relay;
    }

    // --- ENDPOINT PARA LEER LOS TIROS EN GODOT 4 Y CABINA VAR WEB ---
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var historial = await _disparosCollection.Find(_ => true)
                                           .SortByDescending(d => d.TimestampServidor)
                                           .Limit(50)
                                           .ToListAsync();

        return Ok(historial);
    }

    [HttpPost]
    public async Task<IActionResult> Post(Disparo disparo)
    {
        // Inyectar timestamp del servidor en UTC
        disparo.TimestampServidor = DateTime.UtcNow;
        if (string.IsNullOrEmpty(disparo.TimestampUnity))
        {
            disparo.TimestampUnity = disparo.TimestampServidor.ToString("o");
        }

        // Guardar asíncronamente en MongoDB
        await _disparosCollection.InsertOneAsync(disparo);

        // Actualizar el estado del VAR automáticamente y notificar por SignalR
        var nuevoEstadoVar = disparo.FueGol ? "gol" : "nogol";
        var snapVar = EstadoVARController.SetEstado(
            nuevoEstadoVar,
            disparo.Velocidad,
            disparo.PorcentajeCruzoLinea,
            disparo.ResultadoDetalle);
        _relay.BroadcastVarState(snapVar);
        await _hubContext.Clients.All.SendAsync("EstadoVARActualizado", snapVar);

        // Emitir evento de telemetría para cualquier disparo (Gol o No Gol) por WS y SignalR
        _relay.BroadcastNewShot(disparo);
        await _hubContext.Clients.All.SendAsync("NuevoDisparo", disparo);

        // Emitir alerta si fue gol (compatibilidad intacta)
        if (disparo.FueGol)
        {
            await _hubContext.Clients.All.SendAsync("AlertaGol", disparo);
        }

        return CreatedAtAction(nameof(Post), new { id = disparo.Id }, disparo);
    }
}
