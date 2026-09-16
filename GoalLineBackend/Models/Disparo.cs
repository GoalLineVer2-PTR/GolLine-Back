using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace GoalLineBackend.Models;

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
    public string TimestampUnity { get; set; } = null!;

    [BsonElement("timestampServidor")]
    [JsonPropertyName("timestampServidor")]
    public DateTime TimestampServidor { get; set; }
}
