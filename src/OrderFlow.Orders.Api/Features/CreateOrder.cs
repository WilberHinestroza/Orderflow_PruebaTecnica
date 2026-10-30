using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Contracts;
using OrderFlow.BuildingBlocks.Outbox;
using OrderFlow.Orders.Api.Data;
using OrderFlow.Orders.Api.Domain;

namespace OrderFlow.Orders.Api.Features;

/// <summary>
/// Los campos son nullable a propósito: si el cliente no manda "cantidad", queremos responder
/// un 400 con un mensaje claro por campo, no un error genérico de deserialización.
/// </summary>
public sealed record CreateOrderRequest(string? ClienteNombre, string? Sku, int? Cantidad);

/// <summary>
/// Validación pura (sin base de datos): fácil de testear. La existencia del SKU se consulta antes
/// y se pasa como parámetro. Las llaves coinciden con los nombres JSON para que el frontend
/// pueda pintar cada error junto a su campo.
/// </summary>
public static class CreateOrderValidator
{
    public const int MinCantidad = 1;
    public const int MaxCantidad = 100;
    public const int MaxNombre = 120;

    public static Dictionary<string, string[]> Validate(CreateOrderRequest request, bool skuExists)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ClienteNombre))
            errors["clienteNombre"] = ["El nombre del cliente es obligatorio."];
        else if (request.ClienteNombre.Trim().Length > MaxNombre)
            errors["clienteNombre"] = [$"El nombre no puede superar {MaxNombre} caracteres."];

        if (string.IsNullOrWhiteSpace(request.Sku))
            errors["sku"] = ["El SKU es obligatorio."];
        else if (!skuExists)
            errors["sku"] = [$"El SKU '{request.Sku.Trim()}' no existe en el catálogo."];

        if (request.Cantidad is null)
            errors["cantidad"] = ["La cantidad es obligatoria."];
        else if (request.Cantidad is < MinCantidad or > MaxCantidad)
            errors["cantidad"] = [$"La cantidad debe estar entre {MinCantidad} y {MaxCantidad}."];

        return errors;
    }
}

public sealed record CreateOrderResult(Order? Order, Dictionary<string, string[]>? Errors)
{
    public bool IsValid => Errors is null;
}

public sealed class CreateOrderHandler(OrdersDbContext db, TimeProvider clock)
{
    public async Task<CreateOrderResult> HandleAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var sku = request.Sku?.Trim();
        var skuExists = !string.IsNullOrEmpty(sku) && await db.Products.AnyAsync(p => p.Sku == sku, ct);

        var errors = CreateOrderValidator.Validate(request, skuExists);
        if (errors.Count > 0) return new CreateOrderResult(null, errors);

        var now = clock.GetUtcNow();
        var order = Order.Create(request.ClienteNombre!, sku!, request.Cantidad!.Value, now);
        var evt = new OrderCreated(Guid.NewGuid(), order.Id, order.Sku, order.Cantidad, now);

        // Pedido + evento en el MISMO SaveChanges = una sola transacción.
        // O se guardan los dos, o ninguno. Publicar a RabbitMQ lo hace después el OutboxPublisher.
        db.Orders.Add(order);
        db.OutboxMessages.Add(OutboxMessage.Create(RoutingKeys.OrderCreated, evt, order.Id));
        await db.SaveChangesAsync(ct);

        return new CreateOrderResult(order, null);
    }
}
