using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Api.Domain;

namespace OrderFlow.Orders.Api.Data;

/// <summary>
/// Mismos SKUs que el seed de Inventory. En un sistema real el catálogo sería su propio servicio
/// o se replicaría con eventos (ProductCreated); aquí se duplica para mantener la prueba proporcional.
/// </summary>
public static class CatalogSeed
{
    public static async Task SeedAsync(OrdersDbContext db, CancellationToken ct)
    {
        if (await db.Products.AnyAsync(ct)) return;

        db.Products.AddRange(
            new Product("ABC-01", "Teclado mecánico"),
            new Product("DEF-02", "Mouse inalámbrico"),
            new Product("XYZ-03", "Monitor 27\""),
            new Product("GHI-04", "Audífonos (agotado)"));

        await db.SaveChangesAsync(ct);
    }
}
