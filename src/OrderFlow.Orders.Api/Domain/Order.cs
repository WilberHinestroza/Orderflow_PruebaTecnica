namespace OrderFlow.Orders.Api.Domain;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Rejected,
}

public enum TransitionResult
{
    /// <summary>El pedido cambió de estado.</summary>
    Applied,
    /// <summary>El pedido ya estaba en ese estado (evento duplicado): no se hace nada.</summary>
    AlreadyApplied,
    /// <summary>Transición no permitida (p. ej. Confirmed → Rejected): se ignora y se registra.</summary>
    Invalid,
}

/// <summary>
/// Pedido. La máquina de estados vive aquí y no en los endpoints ni en el consumidor:
///
///   Pending ──StockReserved──> Confirmed
///      └─────StockRejected──> Rejected
///
/// Confirmed y Rejected son estados finales. Recibir dos veces el mismo resultado no cambia nada,
/// lo que hace idempotente al consumidor de Orders sin necesitar una tabla extra.
/// </summary>
public sealed class Order
{
    private Order() { } // EF Core

    public Guid Id { get; private set; }
    public string ClienteNombre { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public int Cantidad { get; private set; }
    public OrderStatus Estado { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset? ActualizadoEn { get; private set; }
    public string? MotivoRechazo { get; private set; }

    public static Order Create(string clienteNombre, string sku, int cantidad, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ClienteNombre = clienteNombre.Trim(),
        Sku = sku.Trim(),
        Cantidad = cantidad,
        Estado = OrderStatus.Pending,
        CreadoEn = now,
    };

    public TransitionResult Confirm(DateTimeOffset now) => TransitionTo(OrderStatus.Confirmed, motivo: null, now);

    public TransitionResult Reject(string motivo, DateTimeOffset now) => TransitionTo(OrderStatus.Rejected, motivo, now);

    private TransitionResult TransitionTo(OrderStatus target, string? motivo, DateTimeOffset now)
    {
        if (Estado == target) return TransitionResult.AlreadyApplied;
        if (Estado != OrderStatus.Pending) return TransitionResult.Invalid;

        Estado = target;
        MotivoRechazo = motivo;
        ActualizadoEn = now;
        return TransitionResult.Applied;
    }
}
