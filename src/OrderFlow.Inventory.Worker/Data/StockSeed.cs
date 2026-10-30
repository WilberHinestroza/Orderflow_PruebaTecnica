using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Worker.Domain;

namespace OrderFlow.Inventory.Worker.Data;

public static class StockSeed
{
    public static async Task SeedAsync(InventoryDbContext db, CancellationToken ct)
    {
        if (await db.StockItems.AnyAsync(ct)) return;

        db.StockItems.AddRange(
            new StockItem("ABC-01", 100),
            new StockItem("DEF-02", 50),
            new StockItem("XYZ-03", 5),   // poco stock: fácil de agotar para ver un Rejected
            new StockItem("GHI-04", 0));  // agotado: todo pedido se rechaza

        await db.SaveChangesAsync(ct);
    }
}
