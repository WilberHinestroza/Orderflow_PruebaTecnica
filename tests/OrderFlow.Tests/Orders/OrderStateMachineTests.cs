using OrderFlow.Orders.Api.Domain;

namespace OrderFlow.Tests.Orders;

public sealed class OrderStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pending_pasa_a_Confirmed_y_los_eventos_repetidos_o_contradictorios_no_lo_cambian()
    {
        var order = Order.Create("Ana", "ABC-01", 2, Now);
        Assert.Equal(OrderStatus.Pending, order.Estado);

        Assert.Equal(TransitionResult.Applied, order.Confirm(Now.AddSeconds(1)));
        Assert.Equal(OrderStatus.Confirmed, order.Estado);

        // StockReserved duplicado: idempotente.
        Assert.Equal(TransitionResult.AlreadyApplied, order.Confirm(Now.AddSeconds(2)));

        // Un rechazo tardío no puede deshacer un estado final.
        Assert.Equal(TransitionResult.Invalid, order.Reject("Stock insuficiente.", Now.AddSeconds(3)));
        Assert.Equal(OrderStatus.Confirmed, order.Estado);
        Assert.Null(order.MotivoRechazo);
        Assert.Equal(Now.AddSeconds(1), order.ActualizadoEn);
    }
}
