namespace OrderFlow.Orders.Api.Domain;

/// <summary>
/// Catálogo de productos (solo lectura). Orders lo tiene para validar que el SKU exista
/// sin tener que llamar síncronamente a Inventory. El stock, en cambio, solo lo conoce Inventory.
/// </summary>
public sealed class Product
{
    public Product(string sku, string nombre)
    {
        Sku = sku;
        Nombre = nombre;
    }

    public string Sku { get; private set; }
    public string Nombre { get; private set; }
}
