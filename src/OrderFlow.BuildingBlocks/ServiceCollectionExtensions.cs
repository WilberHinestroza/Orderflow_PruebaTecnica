using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.BuildingBlocks.Outbox;

namespace OrderFlow.BuildingBlocks;

public static class ServiceCollectionExtensions
{
    /// <summary>Opciones de RabbitMQ validadas al arrancar + conexión singleton.</summary>
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnection>();
        return services;
    }

    public static IServiceCollection AddOutboxPublisher<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext, IOutboxDbContext
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxPublisher<TContext>>();
        return services;
    }

    /// <summary>
    /// Crea el esquema (si no existe) y corre el seed. Reintenta porque, al levantar con
    /// docker compose, la base de datos puede tardar unos segundos más que el servicio.
    /// </summary>
    public static async Task InitializeDatabaseAsync<TContext>(
        this IServiceProvider services,
        Func<TContext, CancellationToken, Task> seed,
        CancellationToken ct = default)
        where TContext : DbContext
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        const int maxAttempts = 10;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TContext>();
                await db.Database.EnsureCreatedAsync(ct);
                await seed(db, ct);
                logger.LogInformation("Base de datos de {Context} lista", typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 10));
                logger.LogWarning(ex, "BD no disponible (intento {Attempt}/{Max}); reintento en {Delay}s", attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }
}
