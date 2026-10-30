namespace OrderFlow.Inventory.Worker.Domain;

public sealed class StockItem
{
    public StockItem(string sku, int disponible)
    {
        Sku = sku;
        Disponible = disponible;
    }

    public string Sku { get; private set; }

    /// <summary>
    /// Nunca se modifica en memoria: se descuenta con un UPDATE atómico en la BD
    /// (ver ReserveStockHandler) para evitar condiciones de carrera.
    /// </summary>
    public int Disponible { get; private set; }
}

/// <summary>
/// Registro de eventos ya procesados. Su llave primaria es el EventId:
/// la base de datos impide físicamente registrar dos veces el mismo evento.
/// </summary>
public sealed class ProcessedEvent
{
    public ProcessedEvent(Guid eventId, Guid orderId, string resultado, DateTimeOffset procesadoEn)
    {
        EventId = eventId;
        OrderId = orderId;
        Resultado = resultado;
        ProcesadoEn = procesadoEn;
    }

    public Guid EventId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Resultado { get; private set; }
    public DateTimeOffset ProcesadoEn { get; private set; }
}
