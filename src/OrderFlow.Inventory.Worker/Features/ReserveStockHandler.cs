using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Contracts;
using OrderFlow.BuildingBlocks.Outbox;
using OrderFlow.Inventory.Worker.Data;
using OrderFlow.Inventory.Worker.Domain;

namespace OrderFlow.Inventory.Worker.Features;

public enum ReservationOutcome
{
    Reserved,
    Rejected,
    /// <summary>El evento ya se había procesado: no se tocó el stock ni se publicó nada.</summary>
    Duplicate,
}

/// <summary>
/// Corazón del Inventory: reservar stock de forma idempotente y atómica.
///
/// En UNA transacción:
///   1. UPDATE stock SET disponible = disponible - N WHERE sku = X AND disponible >= N
///      → atómico en la BD; dos pedidos simultáneos nunca dejan el stock negativo.
///   2. INSERT processed_events (EventId)  → la PK impide registrar el mismo evento dos veces.
///   3. INSERT outbox_messages (StockReserved | StockRejected) → el resultado se publicará sí o sí.
///
/// Si el mismo evento llega dos veces:
///   - Normalmente lo detecta la consulta previa (camino rápido) y no se hace nada.
///   - Si llegan dos copias A LA VEZ, ambas pasan la consulta previa, pero la segunda choca con la PK
///     en el paso 2 → rollback → su descuento del paso 1 se deshace. El stock se descuenta una sola vez.
/// </summary>
public sealed class ReserveStockHandler(InventoryDbContext db, TimeProvider clock, ILogger<ReserveStockHandler> logger)
{
    public async Task<ReservationOutcome> HandleAsync(OrderCreated evt, CancellationToken ct)
    {
        if (await AlreadyProcessedAsync(evt.EventId, ct))
        {
            logger.LogInformation("Evento {EventId} (pedido {OrderId}) ya procesado; se ignora", evt.EventId, evt.OrderId);
            return ReservationOutcome.Duplicate;
        }

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var reserved = evt.Cantidad > 0 && await db.StockItems
                .Where(s => s.Sku == evt.Sku && s.Disponible >= evt.Cantidad)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Disponible, x => x.Disponible - evt.Cantidad), ct) == 1;

            ReservationOutcome outcome;
            OutboxMessage result;
            if (reserved)
            {
                outcome = ReservationOutcome.Reserved;
                result = OutboxMessage.Create(
                    RoutingKeys.StockReserved,
                    new StockReserved(Guid.NewGuid(), evt.OrderId, evt.Sku, evt.Cantidad, now),
                    evt.OrderId);
            }
            else
            {
                outcome = ReservationOutcome.Rejected;
                var motivo = await db.StockItems.AnyAsync(s => s.Sku == evt.Sku, ct)
                    ? "Stock insuficiente."
                    : "El SKU no existe en inventario.";
                result = OutboxMessage.Create(
                    RoutingKeys.StockRejected,
                    new StockRejected(Guid.NewGuid(), evt.OrderId, evt.Sku, evt.Cantidad, motivo, now),
                    evt.OrderId);
            }

            db.ProcessedEvents.Add(new ProcessedEvent(evt.EventId, evt.OrderId, outcome.ToString(), now));
            db.OutboxMessages.Add(result);
            await db.SaveChangesAsync(ct); // ← aquí falla si otro consumidor ya registró este EventId
            await tx.CommitAsync(ct);

            logger.LogInformation("Pedido {OrderId}: {Outcome} ({Cantidad} x {Sku})", evt.OrderId, outcome, evt.Cantidad, evt.Sku);
            return outcome;
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();

            // ¿Falló porque otra copia del evento ganó la carrera? Entonces es un duplicado, no un error.
            if (await AlreadyProcessedAsync(evt.EventId, ct))
            {
                logger.LogInformation("Evento {EventId} procesado en paralelo por otra entrega; se descarta", evt.EventId);
                return ReservationOutcome.Duplicate;
            }
            throw;
        }
    }

    private Task<bool> AlreadyProcessedAsync(Guid eventId, CancellationToken ct) =>
        db.ProcessedEvents.AsNoTracking().AnyAsync(p => p.EventId == eventId, ct);
}
