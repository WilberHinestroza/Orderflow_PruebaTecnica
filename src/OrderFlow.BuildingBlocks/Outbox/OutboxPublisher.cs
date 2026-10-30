using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.BuildingBlocks.Messaging;
using RabbitMQ.Client;

namespace OrderFlow.BuildingBlocks.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public int PollingIntervalMs { get; set; } = 1000;
    public int BatchSize { get; set; } = 50;
}

/// <summary>
/// Lee la tabla outbox cada PollingIntervalMs y publica lo pendiente en RabbitMQ.
///
/// - Usa "publisher confirms": BasicPublishAsync no termina hasta que el broker confirma que guardó
///   el mensaje. Solo entonces lo marcamos como publicado.
/// - Si falla (broker caído), se registra el error y se reintenta en el siguiente ciclo.
/// - Garantía resultante: "al menos una vez". Si el proceso muere entre publicar y marcar,
///   el mensaje se publica dos veces → el consumidor debe ser idempotente (lo es).
/// </summary>
public sealed class OutboxPublisher<TContext> : BackgroundService
    where TContext : DbContext, IOutboxDbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnection _connection;
    private readonly TimeProvider _clock;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxPublisher<TContext>> _logger;
    private IChannel? _channel;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnection connection,
        TimeProvider clock,
        IOptions<OutboxOptions> options,
        ILogger<OutboxPublisher<TContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _connection = connection;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.PollingIntervalMs));
        try
        {
            do
            {
                try
                {
                    await PublishPendingAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "No se pudo publicar el outbox; se reintentará en el siguiente ciclo");
                    ResetChannel();
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado normal
        }
        finally
        {
            ResetChannel();
        }
    }

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.Id)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        var channel = await GetChannelAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                var properties = new BasicProperties
                {
                    Persistent = true,               // sobrevive a un reinicio del broker
                    ContentType = "application/json",
                    MessageId = message.EventId.ToString(),
                    Type = message.Type,
                };

                await channel.BasicPublishAsync(
                    exchange: Topology.EventsExchange,
                    routingKey: message.Type,
                    mandatory: true,                 // si nadie la enruta, es un error (no se pierde en silencio)
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken: ct);

                message.MarkPublished(_clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Publicado {Type} {EventId}", message.Type, message.EventId);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Respetamos el orden: si uno falla, cortamos el lote y reintentamos todo después.
                message.MarkFailed(ex.Message);
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;

        ResetChannel();
        var connection = await _connection.GetAsync(ct);
        _channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);
        return _channel;
    }

    private void ResetChannel()
    {
        _channel?.Dispose();
        _channel = null;
    }
}
