using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace GoalLineBackend.Models;

[BsonIgnoreExtraElements]
public class Disparo
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("coordenadaX")]
    [JsonPropertyName("coordenadaX")]
    public float CoordenadaX { get; set; }

    [BsonElement("coordenadaY")]
    [JsonPropertyName("coordenadaY")]
    public float CoordenadaY { get; set; }

    [BsonElement("velocidad")]
    [JsonPropertyName("velocidad")]
    public float Velocidad { get; set; }

    [BsonElement("fueGol")]
    [JsonPropertyName("fueGol")]
    public bool FueGol { get; set; }

    [BsonElement("timestampUnity")]
    [JsonPropertyName("timestampUnity")]
    public string TimestampUnity { get; set; } = string.Empty;

    [BsonElement("timestampServidor")]
    [JsonPropertyName("timestampServidor")]
    public DateTime TimestampServidor { get; set; }

    // --- Campos extendidos para el Simulador 3D Godot 4 y Cabina VAR ---

    [BsonElement("origen")]
    [BsonIgnoreIfNull]
    [JsonPropertyName("origen")]
    public string? Origen { get; set; }

    [BsonElement("efectoCurva")]
    [BsonIgnoreIfNull]
    [JsonPropertyName("efectoCurva")]
    public float? EfectoCurva { get; set; }

    [BsonElement("potencia")]
    [BsonIgnoreIfNull]
    [JsonPropertyName("potencia")]
    public float? Potencia { get; set; }

    [BsonElement("accionArquero")]
    [BsonIgnoreIfNull]
    [JsonPropertyName("accionArquero")]
    public string? AccionArquero { get; set; }

    [BsonElement("resultadoDetalle")]
    [BsonIgnoreIfNull]
    [JsonPropertyName("resultadoDetalle")]
    public string? ResultadoDetalle { get; set; }

    [BsonElement("porcentajeCruzoLinea")]
    [BsonIgnoreIfNull]
    [JsonPropertyName("porcentajeCruzoLinea")]
    public float? PorcentajeCruzoLinea { get; set; }

    [BsonElement("framesReplay")]
    [BsonIgnoreIfNull]
    [JsonPropertyName("framesReplay")]
    public List<ReplayFrameDto>? FramesReplay { get; set; }
}

[BsonIgnoreExtraElements]
public class ReplayFrameDto
{
    [BsonElement("t")]
    [JsonPropertyName("t")]
    public float T { get; set; }

    [BsonElement("bx")]
    [JsonPropertyName("bx")]
    public float Bx { get; set; }

    [BsonElement("by")]
    [JsonPropertyName("by")]
    public float By { get; set; }

    [BsonElement("bz")]
    [JsonPropertyName("bz")]
    public float Bz { get; set; }

    [BsonElement("brx")]
    [JsonPropertyName("brx")]
    public float Brx { get; set; }

    [BsonElement("bry")]
    [JsonPropertyName("bry")]
    public float Bry { get; set; }

    [BsonElement("brz")]
    [JsonPropertyName("brz")]
    public float Brz { get; set; }

    [BsonElement("gkx")]
    [JsonPropertyName("gkx")]
    public float Gkx { get; set; }

    [BsonElement("gky")]
    [JsonPropertyName("gky")]
    public float Gky { get; set; }

    [BsonElement("gkz")]
    [JsonPropertyName("gkz")]
    public float Gkz { get; set; }

    [BsonElement("gkrz")]
    [JsonPropertyName("gkrz")]
    public float Gkrz { get; set; }

    [BsonElement("anim")]
    [JsonPropertyName("anim")]
    public string? Anim { get; set; }

    [BsonElement("animT")]
    [JsonPropertyName("animT")]
    public float AnimT { get; set; }

    [BsonElement("pct")]
    [JsonPropertyName("pct")]
    public float Pct { get; set; }
}

