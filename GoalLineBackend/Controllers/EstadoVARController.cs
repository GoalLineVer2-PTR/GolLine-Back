using GoalLineBackend.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

[Route("api/[controller]")]
[ApiController]
public class EstadoVARController : ControllerBase
{
    private static string estadoActual = "idle";
    private readonly IHubContext<GoalHub> _hubContext;

    public EstadoVARController(IHubContext<GoalHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public static void SetEstado(string nuevoEstado)
    {
        if (nuevoEstado == "idle" || nuevoEstado == "gol" || nuevoEstado == "nogol")
        {
            estadoActual = nuevoEstado;
        }
    }

    // 1. EL GET (Lo que usa el Cartel Estadio como fallback de sincronización)
    // URL: GET http://localhost:8080/api/EstadoVAR
    [HttpGet]
    public IActionResult ObtenerEstado()
    {
        return Ok(new { estado = estadoActual });
    }

    // 2. EL POST (Lo que usa Godot 4 para notificar si fue gol, no gol o reposo)
    // URL: POST http://localhost:8080/api/EstadoVAR
    [HttpPost]
    public async Task<IActionResult> ActualizarEstado([FromBody] EstadoVARRequest request)
    {
        if (request.estado == "idle" || request.estado == "gol" || request.estado == "nogol")
        {
            estadoActual = request.estado;
            await _hubContext.Clients.All.SendAsync("EstadoVARActualizado", new
            {
                estado = estadoActual,
                timestampUtc = DateTime.UtcNow
            });
            return Ok(new { mensaje = "Estado actualizado a: " + estadoActual, estado = estadoActual });
        }
        return BadRequest("Estado inválido.");
    }
}

// DTO para recibir actualizaciones de estado VAR desde Godot 4
public class EstadoVARRequest
{
    public string estado { get; set; } = "idle";
}
