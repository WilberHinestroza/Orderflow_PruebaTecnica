using System.ComponentModel.DataAnnotations;

namespace OrderFlow.BuildingBlocks.Messaging;

/// <summary>
/// Se llena desde variables de entorno: RabbitMq__Host, RabbitMq__Username, etc.
/// Las credenciales no tienen valor por defecto: si faltan, el servicio no arranca.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required] public string Host { get; set; } = string.Empty;
    [Range(1, 65535)] public int Port { get; set; } = 5672;
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";

    /// <summary>Cuántos mensajes sin confirmar (ack) puede tener un consumidor a la vez.</summary>
    [Range(1, 1000)] public ushort PrefetchCount { get; set; } = 10;
}
