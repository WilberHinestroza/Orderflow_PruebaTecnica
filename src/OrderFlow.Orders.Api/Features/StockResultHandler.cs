using OrderFlow.BuildingBlocks.Contracts;
using OrderFlow.Orders.Api.Data;
using OrderFlow.Orders.Api.Domain;

namespace OrderFlow.Orders.Api.Features;

/// <summary>
/// Aplica la respuesta de Inventory al pedido. Es idempotente gracias a la máquina de estados:
/// si el mismo StockReserved llega dos veces, la segunda devuelve AlreadyApplied y no se guarda nada.
/// </summary>
public sealed class StockResultHandler(OrdersDbContext db, TimeProvider clock, ILogger<StockResultHandler> logger)
{
    public Task<TransitionResult?> HandleAsync(StockReserved evt, CancellationToken ct) =>
        ApplyAsync(evt.OrderId, evt.EventId, order => order.Confirm(clock.GetUtcNow()), ct);

    public Task<TransitionResult?> HandleAsync(StockRejected evt, CancellationToken ct) =>
        ApplyAsync(evt.OrderId, evt.EventId, order => order.Reject(evt.Motivo, clock.GetUtcNow()), ct);

    private async Task<TransitionResult?> ApplyAsync(Guid orderId, Guid eventId, Func<Order, TransitionResult> transition, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([orderId], ct);
        if (order is null)
        {
            // No debería ocurrir: el pedido se guarda antes de publicar (outbox). Se registra y se descarta.
            logger.LogWarning("Evento {EventId} para pedido inexistente {OrderId}; se ignora", eventId, orderId);
            return null;
        }

        var result = transition(order);
        switch (result)
        {
            case TransitionResult.Applied:
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Pedido {OrderId} → {Estado}", orderId, order.Estado);
                break;
            case TransitionResult.AlreadyApplied:
                logger.LogInformation("Evento {EventId} duplicado: pedido {OrderId} ya estaba {Estado}", eventId, orderId, order.Estado);
                break;
            case TransitionResult.Invalid:
                logger.LogWarning("Transición inválida para pedido {OrderId} en estado {Estado} (evento {EventId})", orderId, order.Estado, eventId);
                break;
        }

        return result;
    }
}
