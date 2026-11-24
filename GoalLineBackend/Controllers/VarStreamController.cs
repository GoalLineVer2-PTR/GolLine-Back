using System.Text.Json;
using GoalLineBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoalLineBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VarStreamController : ControllerBase
{
    private readonly VarStreamRelayService _relay;

    public VarStreamController(VarStreamRelayService relay)
    {
        _relay = relay;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_relay.GetStatus());
    }

    [HttpPost("command")]
    public async Task<IActionResult> SendCommand([FromBody] JsonElement command, CancellationToken ct)
    {
        var sent = await _relay.SendCommandToGodotAsync(command.GetRawText(), ct);
        if (!sent)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                ok = false,
                error = "Godot 4 no está conectado al canal /ws/godot."
            });
        }

        return Ok(new { ok = true });
    }
}
