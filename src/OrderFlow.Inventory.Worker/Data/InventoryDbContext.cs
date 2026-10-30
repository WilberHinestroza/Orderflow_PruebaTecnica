using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Outbox;
using OrderFlow.Inventory.Worker.Domain;

namespace OrderFlow.Inventory.Worker.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options), IOutboxDbContext
{
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>(e =>
        {
            e.ToTable("stock", t => t.HasCheckConstraint("ck_stock_disponible_no_negativo", "\"Disponible\" >= 0"));
            e.HasKey(x => x.Sku);
            e.Property(x => x.Sku).HasMaxLength(32);
        });

        modelBuilder.Entity<ProcessedEvent>(e =>
        {
            e.ToTable("processed_events");
            e.HasKey(x => x.EventId);
            e.Property(x => x.Resultado).HasMaxLength(16).IsRequired();
        });

        modelBuilder.ApplyOutbox();
    }
}
