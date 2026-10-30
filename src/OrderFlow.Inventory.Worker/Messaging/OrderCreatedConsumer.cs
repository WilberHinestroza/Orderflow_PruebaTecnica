using Microsoft.Extensions.Options;
using OrderFlow.BuildingBlocks.Contracts;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.Inventory.Worker.Features;

namespace OrderFlow.Inventory.Worker.Messaging;

public sealed class OrderCreatedConsumer(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCreatedConsumer> logger)
    : RabbitMqConsumer(connection, options, logger)
{
    protected override string QueueName => Topology.InventoryOrderCreatedQueue;

    protected override async Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        if (routingKey != RoutingKeys.OrderCreated)
            throw new PoisonMessageException($"Routing key inesperada: {routingKey}");

        var evt = EventSerializer.Deserialize<OrderCreated>(body);

        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ReserveStockHandler>();
        await handler.HandleAsync(evt, ct);
    }
}
