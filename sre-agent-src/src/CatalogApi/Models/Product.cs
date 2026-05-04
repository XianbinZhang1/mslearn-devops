namespace CatalogApi.Models;

public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }

    /// <summary>
    /// When true, the product can be ordered even when Stock is 0.
    /// Backorderable items with 0 stock may appear in search results but are labeled accordingly.
    /// </summary>
    public bool IsBackorderable { get; set; }

    /// <summary>
    /// Optional free-text inventory status (e.g. "in_stock", "out_of_stock", "preorder").
    /// When non-empty this overrides the Stock-based in-stock determination.
    /// </summary>
    public string? InventoryStatus { get; set; }

    /// <summary>
    /// Returns true when the product is purchasable:
    /// • Stock > 0, OR
    /// • IsBackorderable is true (0-stock items that can still be ordered), OR
    /// • InventoryStatus explicitly signals availability ("in_stock" / "preorder").
    /// </summary>
    public bool IsInStock =>
        Stock > 0
        || IsBackorderable
        || (InventoryStatus is not null
            && (InventoryStatus.Equals("in_stock", StringComparison.OrdinalIgnoreCase)
                || InventoryStatus.Equals("preorder", StringComparison.OrdinalIgnoreCase)));
}
