using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class EstadoVARController : ControllerBase
{
	// Esta variable estática guarda el estado actual en la memoria del servidor.
	// Arranca siempre en "idle" (Esperando jugada).
	private static string estadoActual = "idle";

	// 1. EL GET (Lo que usa la página web para leer)
	// URL: GET http://localhost:8080/api/EstadoVAR
	[HttpGet]
	public IActionResult ObtenerEstado()
	{
		// Le devolvemos el JSON exacto que espera tu app.js
		return Ok(new { estado = estadoActual });
	}

	// 2. EL POST (Lo que usa Unity para avisar si fue gol)
	// URL: POST http://localhost:8080/api/EstadoVAR
	[HttpPost]
	public IActionResult ActualizarEstado([FromBody] EstadoVARRequest request)
	{
		// Validamos que Unity no mande cualquier cosa
		if (request.estado == "idle" || request.estado == "gol" || request.estado == "nogol")
		{
			estadoActual = request.estado;
			return Ok(new { mensaje = "Estado actualizado a: " + estadoActual });
		}
		return BadRequest("Estado inválido.");
	}
}

// Una clase simple para recibir el JSON de Unity
public class EstadoVARRequest
{
	public string estado { get; set; }
}