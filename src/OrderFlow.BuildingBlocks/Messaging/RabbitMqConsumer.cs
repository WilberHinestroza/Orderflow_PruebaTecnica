using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderFlow.BuildingBlocks.Messaging;

/// <summary>
/// Consumidor base. Las subclases solo implementan HandleAsync; aquí vive la mecánica:
///
/// - autoAck = false: el mensaje solo sale de la cola cuando lo confirmamos (ack) DESPUÉS de procesarlo.
///   Si el proceso muere a mitad de camino, RabbitMQ lo vuelve a entregar → entrega "al menos una vez".
///   Por eso los handlers tienen que ser idempotentes.
/// - PoisonMessageException → nack sin requeue → el mensaje termina en la DLQ.
/// - Cualquier otro error (p. ej. la base de datos caída) → espera y nack con requeue → se reintenta.
/// </summary>
public abstract class RabbitMqConsumer : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequeueDelay = TimeSpan.FromSeconds(2);

    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;

    protected RabbitMqConsumer(RabbitMqConnection connection, IOptions<RabbitMqOptions> options, ILogger logger)
    {
        _connection = connection;
        _options = options.Value;
        Logger = logger;
    }

    protected ILogger Logger { get; }

    protected abstract string QueueName { get; }

    protected abstract Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Reintenta hasta lograr suscribirse (el broker puede no estar listo al arrancar).
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await _connection.GetAsync(stoppingToken);
                using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, delivery) => OnMessageAsync(channel, delivery, stoppingToken);

                await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
                Logger.LogInformation("Escuchando la cola {Queue}", QueueName);

                // Si el broker se reinicia, la librería recupera conexión, canal y suscripción sola.
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "No se pudo consumir {Queue}. Reintentando en {Delay}s", QueueName, RetryDelay.TotalSeconds);
                await DelaySafe(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        try
        {
            await HandleAsync(delivery.RoutingKey, delivery.Body, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (PoisonMessageException ex)
        {
            Logger.LogError(ex, "Mensaje inválido en {Queue} ({RoutingKey}); se envía a la DLQ", QueueName, delivery.RoutingKey);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Logger.LogWarning(ex, "Error transitorio procesando {RoutingKey}; se reencola", delivery.RoutingKey);
            await DelaySafe(RequeueDelay, ct);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
        }
    }

    private static async Task DelaySafe(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); }
        catch (OperationCanceledException) { /* apagando */ }
    }
}
