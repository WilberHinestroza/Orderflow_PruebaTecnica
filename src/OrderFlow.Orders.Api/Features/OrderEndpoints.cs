using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Contracts;
using OrderFlow.Orders.Api.Data;
using OrderFlow.Orders.Api.Domain;

namespace OrderFlow.Orders.Api.Features;

public sealed record OrderResponse(
    Guid Id,
    string ClienteNombre,
    string Sku,
    int Cantidad,
    OrderStatus Estado,
    DateTimeOffset CreadoEn,
    DateTimeOffset? ActualizadoEn,
    string? MotivoRechazo)
{
    public static OrderResponse From(Order o) =>
        new(o.Id, o.ClienteNombre, o.Sku, o.Cantidad, o.Estado, o.CreadoEn, o.ActualizadoEn, o.MotivoRechazo);
}

public sealed record ProductResponse(string Sku, string Nombre);

public static class OrderEndpoints
{
    private const int MaxListado = 100;

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app, bool demoEndpoints)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders");

        orders.MapPost("", async (CreateOrderRequest request, CreateOrderHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(request, ct);
                return result.IsValid
                    ? Results.Created($"/orders/{result.Order!.Id}", OrderResponse.From(result.Order))
                    : Results.ValidationProblem(result.Errors!, title: "Los datos del pedido no son válidos.");
            })
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        orders.MapGet("", async (OrdersDbContext db, CancellationToken ct) =>
            {
                var list = await db.Orders.AsNoTracking()
                    .OrderByDescending(o => o.CreadoEn)
                    .Take(MaxListado)
                    .ToListAsync(ct);
                return Results.Ok(list.Select(OrderResponse.From));
            })
            .Produces<IEnumerable<OrderResponse>>();

        orders.MapGet("/{id:guid}", async (Guid id, OrdersDbContext db, CancellationToken ct) =>
            {
                var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
                return order is null ? Results.NotFound() : Results.Ok(OrderResponse.From(order));
            })
            .Produces<OrderResponse>()
            .Produces(StatusCodes.Status404NotFound);

        if (demoEndpoints)
        {
            // Solo para demostrar idempotencia: vuelve a encolar el MISMO OrderCreated (mismo EventId).
            // Inventory debe ignorarlo y el stock no debe cambiar. Se desactiva con Features__DemoEndpoints=false.
            orders.MapPost("/{id:guid}/republish", async (Guid id, OrdersDbContext db, TimeProvider clock, CancellationToken ct) =>
                {
                    var original = await db.OutboxMessages
                        .Where(m => m.AggregateId == id && m.Type == RoutingKeys.OrderCreated)
                        .OrderBy(m => m.Id)
                        .FirstOrDefaultAsync(ct);
                    if (original is null) return Results.NotFound();

                    db.OutboxMessages.Add(original.Duplicate(clock.GetUtcNow()));
                    await db.SaveChangesAsync(ct);
                    return Results.Accepted(value: new { original.EventId, mensaje = "Evento OrderCreated reenviado con el mismo EventId." });
                })
                .WithTags("Demo");
        }

        app.MapGet("/products", async (OrdersDbContext db, CancellationToken ct) =>
            {
                var products = await db.Products.AsNoTracking()
                    .OrderBy(p => p.Sku)
                    .Select(p => new ProductResponse(p.Sku, p.Nombre))
                    .ToListAsync(ct);
                return Results.Ok(products);
            })
            .WithTags("Catálogo")
            .Produces<IEnumerable<ProductResponse>>();

        return app;
    }
}
