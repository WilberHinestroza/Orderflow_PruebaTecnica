using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks;
using OrderFlow.Orders.Api.Data;
using OrderFlow.Orders.Api.Features;
using OrderFlow.Orders.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Toda la configuración llega por variables de entorno (ConnectionStrings__OrdersDb, RabbitMq__Host, ...).
var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Falta la variable de entorno ConnectionStrings__OrdersDb.");
var demoEndpoints = builder.Configuration.GetValue("Features:DemoEndpoints", false);

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

builder.Services.AddDbContext<OrdersDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutboxPublisher<OrdersDbContext>(builder.Configuration);
builder.Services.AddHostedService<StockResultsConsumer>();

builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<StockResultHandler>();

builder.Services.AddProblemDetails();
// Los enums viajan como texto ("Confirmed"), no como número. Se configura en los dos sitios:
// HttpJsonOptions lo usa la Minimal API al responder; Mvc.JsonOptions lo lee Swagger para documentar.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Readiness depende solo de la BD. A propósito NO depende de RabbitMQ: gracias al outbox,
// la API sigue aceptando pedidos aunque el broker esté caído.
builder.Services.AddHealthChecks().AddDbContextCheck<OrdersDbContext>(tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();

await app.Services.InitializeDatabaseAsync<OrdersDbContext>(CatalogSeed.SeedAsync);

app.MapOrderEndpoints(demoEndpoints);
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();
