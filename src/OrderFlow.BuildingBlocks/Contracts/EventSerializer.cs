using System.Text.Json;
using OrderFlow.BuildingBlocks.Messaging;

namespace OrderFlow.BuildingBlocks.Contracts;

/// <summary>
/// Serialización única para todos los eventos (JSON camelCase), así productor y consumidor
/// nunca difieren en el formato.
/// </summary>
public static class EventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T evt) where T : IIntegrationEvent => JsonSerializer.Serialize(evt, Options);

    /// <summary>
    /// Un mensaje que no se puede leer nunca se va a poder procesar: se marca como "poison"
    /// para mandarlo a la dead-letter queue en vez de reintentarlo infinitamente.
    /// </summary>
    public static T Deserialize<T>(ReadOnlyMemory<byte> body) where T : IIntegrationEvent
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body.Span, Options)
                ?? throw new PoisonMessageException($"El mensaje {typeof(T).Name} vino vacío.");
        }
        catch (JsonException ex)
        {
            throw new PoisonMessageException($"No se pudo deserializar {typeof(T).Name}: {ex.Message}", ex);
        }
    }
}
