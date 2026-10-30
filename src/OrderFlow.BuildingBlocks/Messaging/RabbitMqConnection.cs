using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderFlow.BuildingBlocks.Messaging;

/// <summary>
/// Una única conexión TCP a RabbitMQ por proceso (es lo recomendado: las conexiones son caras,
/// los canales son baratos). Se crea de forma perezosa: si el broker está caído al arrancar,
/// el servicio arranca igual y quien la pida reintentará más tarde.
/// Una vez creada, la librería se encarga de reconectar sola (AutomaticRecoveryEnabled).
/// </summary>
public sealed class RabbitMqConnection : IDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options, IHostEnvironment environment, ILogger<RabbitMqConnection> logger)
    {
        var o = options.Value;
        _logger = logger;
        _factory = new ConnectionFactory
        {
            HostName = o.Host,
            Port = o.Port,
            UserName = o.Username,
            Password = o.Password,
            VirtualHost = o.VirtualHost,
            ClientProvidedName = environment.ApplicationName,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        };
    }

    public bool IsOpen => _connection?.IsOpen == true;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is not null) return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is not null) return _connection;

            var connection = await _factory.CreateConnectionAsync(ct);

            // Declaramos la topología una vez por conexión (idempotente).
            using (var channel = await connection.CreateChannelAsync(cancellationToken: ct))
            {
                await Topology.DeclareAsync(channel, ct);
            }

            _logger.LogInformation("Conectado a RabbitMQ en {Host}:{Port}", _factory.HostName, _factory.Port);
            _connection = connection;
            return connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _lock.Dispose();
    }
}
