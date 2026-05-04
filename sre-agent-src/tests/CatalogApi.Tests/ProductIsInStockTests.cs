using CatalogApi.Models;
using Xunit;

namespace CatalogApi.Tests;

/// <summary>
/// Unit tests for <see cref="Product.IsInStock"/>.
///
/// IsInStock returns true when ANY of the following hold:
///   1. Stock > 0
///   2. IsBackorderable = true  (ordered despite 0 stock)
///   3. InventoryStatus is "in_stock" or "preorder" (case-insensitive)
///
/// A product is out of stock only when ALL of the following hold:
///   • Stock = 0
///   • IsBackorderable = false
///   • InventoryStatus is null, empty, or something other than in_stock/preorder
/// </summary>
public class ProductIsInStockTests
{
    // ── In-stock via positive quantity ─────────────────────────────────────────

    [Fact]
    public void IsInStock_WhenStockPositive_ReturnsTrue()
    {
        var product = new Product { Stock = 1 };
        Assert.True(product.IsInStock);
    }

    [Fact]
    public void IsInStock_WhenStockLarge_ReturnsTrue()
    {
        var product = new Product { Stock = 500 };
        Assert.True(product.IsInStock);
    }

    // ── Out-of-stock when no overrides ────────────────────────────────────────

    [Fact]
    public void IsInStock_WhenZeroStockNotBackorderableNoStatus_ReturnsFalse()
    {
        var product = new Product { Stock = 0, IsBackorderable = false, InventoryStatus = null };
        Assert.False(product.IsInStock);
    }

    [Fact]
    public void IsInStock_WhenZeroStockNotBackorderableEmptyStatus_ReturnsFalse()
    {
        var product = new Product { Stock = 0, IsBackorderable = false, InventoryStatus = "" };
        Assert.False(product.IsInStock);
    }

    [Fact]
    public void IsInStock_WhenZeroStockNotBackorderableOutOfStockStatus_ReturnsFalse()
    {
        var product = new Product { Stock = 0, IsBackorderable = false, InventoryStatus = "out_of_stock" };
        Assert.False(product.IsInStock);
    }

    // ── In-stock via backorder flag ───────────────────────────────────────────

    [Fact]
    public void IsInStock_WhenZeroStockAndBackorderable_ReturnsTrue()
    {
        var product = new Product { Stock = 0, IsBackorderable = true };
        Assert.True(product.IsInStock);
    }

    // ── In-stock via inventoryStatus ──────────────────────────────────────────

    [Fact]
    public void IsInStock_WhenStatusIsInStock_ReturnsTrue()
    {
        var product = new Product { Stock = 0, IsBackorderable = false, InventoryStatus = "in_stock" };
        Assert.True(product.IsInStock);
    }

    [Fact]
    public void IsInStock_WhenStatusIsInStockUpperCase_ReturnsTrue()
    {
        var product = new Product { Stock = 0, IsBackorderable = false, InventoryStatus = "IN_STOCK" };
        Assert.True(product.IsInStock);
    }

    [Fact]
    public void IsInStock_WhenStatusIsPreorder_ReturnsTrue()
    {
        var product = new Product { Stock = 0, IsBackorderable = false, InventoryStatus = "preorder" };
        Assert.True(product.IsInStock);
    }

    [Fact]
    public void IsInStock_WhenStatusIsPreorderMixedCase_ReturnsTrue()
    {
        var product = new Product { Stock = 0, IsBackorderable = false, InventoryStatus = "Preorder" };
        Assert.True(product.IsInStock);
    }

    // ── Positive stock overrides negative flags ───────────────────────────────

    [Fact]
    public void IsInStock_WhenPositiveStockAndExplicitOutOfStockStatus_ReturnsTrue()
    {
        // Stock count wins — the status label is stale/incorrect data.
        var product = new Product { Stock = 10, IsBackorderable = false, InventoryStatus = "out_of_stock" };
        Assert.True(product.IsInStock);
    }
}
