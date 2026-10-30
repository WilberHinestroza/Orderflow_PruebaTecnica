using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks;
using OrderFlow.Inventory.Worker.Data;
using OrderFlow.Inventory.Worker.Features;
using OrderFlow.Inventory.Worker.Messaging;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("InventoryDb")
    ?? throw new InvalidOperationException("Falta la variable de entorno ConnectionStrings__InventoryDb.");

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

builder.Services.AddDbContext<InventoryDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutboxPublisher<InventoryDbContext>(builder.Configuration);
builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddScoped<ReserveStockHandler>();

builder.Services.AddHealthChecks().AddDbContextCheck<InventoryDbContext>(tags: ["ready"]);

var app = builder.Build();

await app.Services.InitializeDatabaseAsync<InventoryDbContext>(StockSeed.SeedAsync);

// Solo lectura, para que el panel muestre el stock y se pueda verificar la idempotencia a ojo.
app.MapGet("/stock", async (InventoryDbContext db, CancellationToken ct) =>
    Results.Ok(await db.StockItems.AsNoTracking()
        .OrderBy(s => s.Sku)
        .Select(s => new { s.Sku, s.Disponible })
        .ToListAsync(ct)));

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();
