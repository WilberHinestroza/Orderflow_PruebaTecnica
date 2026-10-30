using Microsoft.Extensions.Options;
using OrderFlow.BuildingBlocks.Contracts;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.Orders.Api.Features;

namespace OrderFlow.Orders.Api.Messaging;

/// <summary>
/// Escucha StockReserved / StockRejected. Solo traduce mensaje → handler;
/// la lógica está en StockResultHandler (testeable sin RabbitMQ).
/// </summary>
public sealed class StockResultsConsumer(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<StockResultsConsumer> logger)
    : RabbitMqConsumer(connection, options, logger)
{
    protected override string QueueName => Topology.OrdersStockResultsQueue;

    protected override async Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        // Un scope por mensaje: DbContext nuevo y limpio para cada uno.
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<StockResultHandler>();

        switch (routingKey)
        {
            case RoutingKeys.StockReserved:
                await handler.HandleAsync(EventSerializer.Deserialize<StockReserved>(body), ct);
                break;
            case RoutingKeys.StockRejected:
                await handler.HandleAsync(EventSerializer.Deserialize<StockRejected>(body), ct);
                break;
            default:
                throw new PoisonMessageException($"Routing key inesperada: {routingKey}");
        }
    }
}
