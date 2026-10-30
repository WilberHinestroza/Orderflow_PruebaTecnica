using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Contracts;

namespace OrderFlow.BuildingBlocks.Outbox;

/// <summary>
/// Patrón Transactional Outbox.
///
/// Problema: "guardar en la BD" y "publicar en RabbitMQ" son dos sistemas distintos; no hay una
/// transacción que cubra ambos. Si guardo el pedido y el broker está caído, el evento se pierde.
///
/// Solución: el evento se guarda como una fila más, en la MISMA transacción que el cambio de negocio.
/// Un proceso en segundo plano (OutboxPublisher) lee las filas pendientes y las publica.
/// Si el broker está caído, las filas esperan y se publican cuando vuelva.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage() { } // EF Core

    /// <summary>Autoincremental: da el orden de publicación.</summary>
    public long Id { get; private set; }

    /// <summary>Id del evento (el que usa el consumidor para deduplicar).</summary>
    public Guid EventId { get; private set; }

    /// <summary>Routing key, p. ej. "order.created".</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Id de la entidad de negocio a la que pertenece (el pedido).</summary>
    public Guid AggregateId { get; private set; }

    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxMessage Create<T>(string routingKey, T evt, Guid aggregateId) where T : IIntegrationEvent => new()
    {
        EventId = evt.EventId,
        Type = routingKey,
        AggregateId = aggregateId,
        Payload = EventSerializer.Serialize(evt),
        OccurredAt = evt.OcurridoEn,
    };

    /// <summary>
    /// Copia exacta (mismo EventId y payload) para volver a publicar un evento.
    /// Solo se usa para demostrar la idempotencia del consumidor.
    /// </summary>
    public OutboxMessage Duplicate(DateTimeOffset now) => new()
    {
        EventId = EventId,
        Type = Type,
        AggregateId = AggregateId,
        Payload = Payload,
        OccurredAt = now,
    };

    public void MarkPublished(DateTimeOffset when) => PublishedAt = when;

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 500 ? error[..500] : error;
    }
}

/// <summary>Cualquier DbContext que quiera publicar eventos expone su tabla de outbox.</summary>
public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
}

public static class OutboxModelBuilderExtensions
{
    public static ModelBuilder ApplyOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Type).HasMaxLength(100).IsRequired();
            e.Property(x => x.Payload).IsRequired();
            e.Property(x => x.LastError).HasMaxLength(500);
            e.HasIndex(x => x.PublishedAt);
            e.HasIndex(x => x.AggregateId);
        });
        return modelBuilder;
    }
}
