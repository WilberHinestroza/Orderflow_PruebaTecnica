using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Contracts;
using OrderFlow.Orders.Api.Data;
using OrderFlow.Orders.Api.Domain;
using OrderFlow.Orders.Api.Features;

namespace OrderFlow.Tests.Orders;

public sealed class CreateOrderTests
{
    [Theory]
    [InlineData("", "ABC-01", 1, true, "clienteNombre")]
    [InlineData("   ", "ABC-01", 1, true, "clienteNombre")]
    [InlineData("Ana", "NO-EXISTE", 1, false, "sku")]
    [InlineData("Ana", "ABC-01", 0, true, "cantidad")]
    [InlineData("Ana", "ABC-01", 101, true, "cantidad")]
    [InlineData("Ana", "ABC-01", null, true, "cantidad")]
    public void Validacion_reporta_el_campo_invalido(string nombre, string sku, int? cantidad, bool skuExists, string campo)
    {
        var errors = CreateOrderValidator.Validate(new CreateOrderRequest(nombre, sku, cantidad), skuExists);

        var key = Assert.Single(errors.Keys);
        Assert.Equal(campo, key);
    }

    [Fact]
    public async Task Pedido_valido_se_guarda_Pending_junto_con_su_evento_en_el_outbox()
    {
        await using var database = await SqliteTestDatabase<OrdersDbContext>.CreateAsync(
            options => new OrdersDbContext(options),
            CatalogSeedForTests);

        await using (var db = database.CreateContext())
        {
            var handler = new CreateOrderHandler(db, TimeProvider.System);

            var invalid = await handler.HandleAsync(new CreateOrderRequest("Ana", "NO-EXISTE", 1), CancellationToken.None);
            Assert.False(invalid.IsValid);

            var created = await handler.HandleAsync(new CreateOrderRequest("  Ana  ", "ABC-01", 2), CancellationToken.None);
            Assert.True(created.IsValid);
        }

        await using var verify = database.CreateContext();
        var order = Assert.Single(await verify.Orders.ToListAsync());   // el inválido no se guardó
        Assert.Equal(OrderStatus.Pending, order.Estado);
        Assert.Equal("Ana", order.ClienteNombre);

        var outbox = Assert.Single(await verify.OutboxMessages.ToListAsync());
        Assert.Equal(RoutingKeys.OrderCreated, outbox.Type);
        Assert.Equal(order.Id, outbox.AggregateId);
        Assert.Null(outbox.PublishedAt);                                 // lo publicará el OutboxPublisher
    }

    private static async Task CatalogSeedForTests(OrdersDbContext db) =>
        await CatalogSeed.SeedAsync(db, CancellationToken.None);
}
