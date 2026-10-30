namespace OrderFlow.BuildingBlocks.Contracts;

/// <summary>
/// Todo evento de integración lleva un EventId único. Es la llave que usan los consumidores
/// para detectar duplicados (idempotencia).
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OcurridoEn { get; }
}

/// <summary>Publicado por Orders API cuando se persiste un pedido nuevo en estado Pending.</summary>
public sealed record OrderCreated(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    DateTimeOffset OcurridoEn) : IIntegrationEvent;

/// <summary>Publicado por Inventory cuando logró descontar el stock del pedido.</summary>
public sealed record StockReserved(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    DateTimeOffset OcurridoEn) : IIntegrationEvent;

/// <summary>Publicado por Inventory cuando no hay stock suficiente (o el SKU no existe en inventario).</summary>
public sealed record StockRejected(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    string Motivo,
    DateTimeOffset OcurridoEn) : IIntegrationEvent;

/// <summary>
/// Las routing keys identifican el tipo de evento dentro del exchange.
/// </summary>
public static class RoutingKeys
{
    public const string OrderCreated = "order.created";
    public const string StockReserved = "stock.reserved";
    public const string StockRejected = "stock.rejected";
}
