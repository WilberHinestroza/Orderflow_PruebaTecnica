using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.BuildingBlocks.Contracts;
using OrderFlow.Inventory.Worker.Data;
using OrderFlow.Inventory.Worker.Domain;
using OrderFlow.Inventory.Worker.Features;

namespace OrderFlow.Tests.Inventory;

public sealed class ReserveStockHandlerTests
{
    private static Task<SqliteTestDatabase<InventoryDbContext>> CreateDatabaseAsync(int stockInicial) =>
        SqliteTestDatabase<InventoryDbContext>.CreateAsync(
            options => new InventoryDbContext(options),
            async db =>
            {
                db.StockItems.Add(new StockItem("ABC-01", stockInicial));
                await db.SaveChangesAsync();
            });

    private static ReserveStockHandler CreateHandler(InventoryDbContext db) =>
        new(db, TimeProvider.System, NullLogger<ReserveStockHandler>.Instance);

    [Fact]
    public async Task El_mismo_OrderCreated_procesado_dos_veces_descuenta_el_stock_una_sola_vez()
    {
        await using var database = await CreateDatabaseAsync(stockInicial: 10);
        var evt = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), "ABC-01", 3, DateTimeOffset.UtcNow);

        // Dos entregas del mismo evento, cada una con su propio DbContext (como en producción).
        await using (var db = database.CreateContext())
            Assert.Equal(ReservationOutcome.Reserved, await CreateHandler(db).HandleAsync(evt, CancellationToken.None));

        await using (var db = database.CreateContext())
            Assert.Equal(ReservationOutcome.Duplicate, await CreateHandler(db).HandleAsync(evt, CancellationToken.None));

        await using var verify = database.CreateContext();
        var stock = await verify.StockItems.SingleAsync(s => s.Sku == "ABC-01");
        Assert.Equal(7, stock.Disponible);                                   // 10 - 3, no 10 - 6
        Assert.Equal(1, await verify.ProcessedEvents.CountAsync());
        var outbox = Assert.Single(await verify.OutboxMessages.ToListAsync()); // un solo StockReserved
        Assert.Equal(RoutingKeys.StockReserved, outbox.Type);
        Assert.Equal(evt.OrderId, outbox.AggregateId);
    }

    [Fact]
    public async Task Sin_stock_suficiente_rechaza_y_no_toca_el_stock()
    {
        await using var database = await CreateDatabaseAsync(stockInicial: 2);
        var evt = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), "ABC-01", 5, DateTimeOffset.UtcNow);

        await using (var db = database.CreateContext())
            Assert.Equal(ReservationOutcome.Rejected, await CreateHandler(db).HandleAsync(evt, CancellationToken.None));

        await using var verify = database.CreateContext();
        Assert.Equal(2, (await verify.StockItems.SingleAsync()).Disponible);
        var outbox = Assert.Single(await verify.OutboxMessages.ToListAsync());
        Assert.Equal(RoutingKeys.StockRejected, outbox.Type);
        Assert.Contains("Stock insuficiente", outbox.Payload);
    }
}
