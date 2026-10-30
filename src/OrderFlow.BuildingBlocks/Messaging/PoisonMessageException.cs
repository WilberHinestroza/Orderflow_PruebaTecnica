namespace OrderFlow.BuildingBlocks.Messaging;

/// <summary>
/// Error no recuperable: reintentar no sirve (JSON corrupto, routing key desconocida).
/// El consumidor lo envía a la dead-letter queue.
/// </summary>
public sealed class PoisonMessageException(string message, Exception? inner = null) : Exception(message, inner);
