using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Outbox;
using OrderFlow.Orders.Api.Domain;

namespace OrderFlow.Orders.Api.Data;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options), IOutboxDbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.ClienteNombre).HasMaxLength(120).IsRequired();
            e.Property(x => x.Sku).HasMaxLength(32).IsRequired();
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.MotivoRechazo).HasMaxLength(200);
            e.HasIndex(x => x.CreadoEn);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Sku);
            e.Property(x => x.Sku).HasMaxLength(32);
            e.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        });

        modelBuilder.ApplyOutbox();
    }
}
