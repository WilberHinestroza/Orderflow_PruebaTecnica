using OrderFlow.BuildingBlocks.Contracts;
using RabbitMQ.Client;

namespace OrderFlow.BuildingBlocks.Messaging;

/// <summary>
/// Topología de RabbitMQ:
///
///   orderflow.events (topic) --order.created-----------------> inventory.order-created
///                            --stock.reserved|stock.rejected--> orders.stock-results
///
/// Cada cola tiene su dead-letter queue (*.dlq) a la que van los mensajes "poison".
/// Declarar es idempotente: ambos servicios la declaran al conectarse, así no importa cuál arranca primero.
/// </summary>
public static class Topology
{
    public const string EventsExchange = "orderflow.events";
    public const string DeadLetterExchange = "orderflow.dlx";

    public const string InventoryOrderCreatedQueue = "inventory.order-created";
    public const string OrdersStockResultsQueue = "orders.stock-results";

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(EventsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);

        await DeclareQueueAsync(channel, InventoryOrderCreatedQueue, [RoutingKeys.OrderCreated], ct);
        await DeclareQueueAsync(channel, OrdersStockResultsQueue, [RoutingKeys.StockReserved, RoutingKeys.StockRejected], ct);
    }

    private static async Task DeclareQueueAsync(IChannel channel, string queue, string[] routingKeys, CancellationToken ct)
    {
        var deadLetterQueue = $"{queue}.dlq";
        await channel.QueueDeclareAsync(deadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(deadLetterQueue, DeadLetterExchange, routingKey: queue, cancellationToken: ct);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = queue,
        };
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: arguments, cancellationToken: ct);

        foreach (var routingKey in routingKeys)
        {
            await channel.QueueBindAsync(queue, EventsExchange, routingKey, cancellationToken: ct);
        }
    }
}
