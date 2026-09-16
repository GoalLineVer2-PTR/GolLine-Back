using GoalLineBackend.Hubs;
using GoalLineBackend.Models;
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

    public DisparosController(
        IOptions<MongoDBSettings> mongoDBSettings,
        IMongoClient mongoClient,
        IHubContext<GoalHub> hubContext)
    {
        var mongoDatabase = mongoClient.GetDatabase(mongoDBSettings.Value.DatabaseName);
        _disparosCollection = mongoDatabase.GetCollection<Disparo>(mongoDBSettings.Value.CollectionName);
        _hubContext = hubContext;
    }

    // --- ENDPOINT PARA LEER LOS TIROS EN UNITY ---
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        // Trae todo el historial ordenado del más nuevo al más viejo, sin límite
        var historial = await _disparosCollection.Find(_ => true)
                                           .SortByDescending(d => d.TimestampServidor)
                                           .ToListAsync();

        return Ok(historial);
    }

    [HttpPost]
    public async Task<IActionResult> Post(Disparo disparo)
    {
        // Inyectar timestamp del servidor en UTC
        disparo.TimestampServidor = DateTime.UtcNow;

        // Guardar asíncronamente en MongoDB
        await _disparosCollection.InsertOneAsync(disparo);

        // Emitir alerta si fue gol
        if (disparo.FueGol)
        {
            await _hubContext.Clients.All.SendAsync("AlertaGol", disparo);
        }

        return CreatedAtAction(nameof(Post), new { id = disparo.Id }, disparo);
    }
}