using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OrderFlow.Tests;

/// <summary>
/// Base de datos SQLite en memoria por test. A diferencia del provider InMemory de EF,
/// SQLite es una BD relacional real: respeta transacciones, llaves primarias y CHECK constraints,
/// que es justo lo que necesitamos para probar la idempotencia.
/// La base vive mientras la conexión esté abierta.
/// </summary>
public sealed class SqliteTestDatabase<TContext> : IAsyncDisposable where TContext : DbContext
{
    private readonly SqliteConnection _connection;
    private readonly Func<DbContextOptions<TContext>, TContext> _factory;
    private readonly DbContextOptions<TContext> _options;

    private SqliteTestDatabase(SqliteConnection connection, Func<DbContextOptions<TContext>, TContext> factory)
    {
        _connection = connection;
        _factory = factory;
        _options = new DbContextOptionsBuilder<TContext>().UseSqlite(connection).Options;
    }

    public static async Task<SqliteTestDatabase<TContext>> CreateAsync(
        Func<DbContextOptions<TContext>, TContext> factory,
        Func<TContext, Task>? seed = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var database = new SqliteTestDatabase<TContext>(connection, factory);
        await using var context = database.CreateContext();
        await context.Database.EnsureCreatedAsync();
        if (seed is not null) await seed(context);
        return database;
    }

    /// <summary>Un DbContext nuevo por llamada, como pasa en producción (un scope por mensaje).</summary>
    public TContext CreateContext() => _factory(_options);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
