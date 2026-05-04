using CatalogApi.Services;
using Xunit;

namespace CatalogApi.Tests;

/// <summary>
/// Unit tests for <see cref="ProductSearchQueryBuilder.Build"/>.
///
/// Verifies that the correct SQL WHERE predicates are generated for every combination
/// of keyword, category, and inStockOnly without touching a live database.
/// </summary>
public class ProductSearchQueryBuilderTests
{
    // ── No filters ────────────────────────────────────────────────────────────

    [Fact]
    public void Build_NoFilters_InStockFalse_ReturnsSelectAll()
    {
        var (sql, parameters) = ProductSearchQueryBuilder.Build(inStockOnly: false);

        Assert.Equal("SELECT * FROM c", sql);
        Assert.Empty(parameters);
    }

    // ── In-stock filter only ──────────────────────────────────────────────────

    [Fact]
    public void Build_InStockOnly_ContainsInventoryPredicate()
    {
        var (sql, parameters) = ProductSearchQueryBuilder.Build(inStockOnly: true);

        Assert.Contains("WHERE", sql);
        Assert.Contains("c.stock > 0", sql);
        Assert.Contains("c.isBackorderable = true", sql);
        Assert.Contains("c.inventoryStatus IN ('in_stock', 'preorder')", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Build_InStockOnlyDefault_IsTrue()
    {
        // Calling Build() without arguments should default to inStockOnly = true.
        var (sql, _) = ProductSearchQueryBuilder.Build();

        Assert.Contains("c.stock > 0", sql);
    }

    // ── Keyword filter ────────────────────────────────────────────────────────

    [Fact]
    public void Build_WithKeyword_ContainsKeywordPredicate()
    {
        var (sql, parameters) = ProductSearchQueryBuilder.Build(keyword: "keyboard", inStockOnly: false);

        Assert.Contains("CONTAINS(LOWER(c.name), @keyword)", sql);
        Assert.Contains("CONTAINS(LOWER(c.description), @keyword)", sql);
        Assert.Contains("CONTAINS(LOWER(c.category), @keyword)", sql);
        Assert.Equal("keyboard", parameters["@keyword"]);
    }

    [Fact]
    public void Build_WithKeyword_NormalizesKeywordToLowerCase()
    {
        var (_, parameters) = ProductSearchQueryBuilder.Build(keyword: "KEYBOARD", inStockOnly: false);

        Assert.Equal("keyboard", parameters["@keyword"]);
    }

    [Fact]
    public void Build_WithWhitespaceKeyword_IgnoresKeyword()
    {
        var (sql, parameters) = ProductSearchQueryBuilder.Build(keyword: "   ", inStockOnly: false);

        Assert.Equal("SELECT * FROM c", sql);
        Assert.DoesNotContain("@keyword", parameters.Keys);
    }

    // ── Category filter ───────────────────────────────────────────────────────

    [Fact]
    public void Build_WithCategory_ContainsCategoryPredicate()
    {
        var (sql, parameters) = ProductSearchQueryBuilder.Build(category: "Electronics", inStockOnly: false);

        Assert.Contains("LOWER(c.category) = @category", sql);
        Assert.Equal("electronics", parameters["@category"]);
    }

    [Fact]
    public void Build_WithCategory_NormalizesCategoryToLowerCase()
    {
        var (_, parameters) = ProductSearchQueryBuilder.Build(category: "FURNITURE", inStockOnly: false);

        Assert.Equal("furniture", parameters["@category"]);
    }

    // ── Combined filters ──────────────────────────────────────────────────────

    [Fact]
    public void Build_InStockAndKeyword_ContainsBothPredicates()
    {
        var (sql, parameters) = ProductSearchQueryBuilder.Build(keyword: "desk", inStockOnly: true);

        Assert.Contains("c.stock > 0", sql);
        Assert.Contains("CONTAINS(LOWER(c.name), @keyword)", sql);
        Assert.Equal("desk", parameters["@keyword"]);
    }

    [Fact]
    public void Build_InStockAndCategory_ContainsBothPredicates()
    {
        var (sql, parameters) = ProductSearchQueryBuilder.Build(category: "Furniture", inStockOnly: true);

        Assert.Contains("c.stock > 0", sql);
        Assert.Contains("LOWER(c.category) = @category", sql);
        Assert.Equal("furniture", parameters["@category"]);
    }

    [Fact]
    public void Build_AllFilters_ContainsAllPredicates()
    {
        var (sql, parameters) = ProductSearchQueryBuilder.Build(
            keyword: "stand",
            category: "Furniture",
            inStockOnly: true);

        Assert.Contains("c.stock > 0", sql);
        Assert.Contains("CONTAINS(LOWER(c.name), @keyword)", sql);
        Assert.Contains("LOWER(c.category) = @category", sql);
        Assert.Equal("stand", parameters["@keyword"]);
        Assert.Equal("furniture", parameters["@category"]);
    }

    // ── AND-chaining ──────────────────────────────────────────────────────────

    [Fact]
    public void Build_MultipleFilters_JoinedWithAnd()
    {
        var (sql, _) = ProductSearchQueryBuilder.Build(keyword: "lamp", inStockOnly: true);

        // The two condition blocks must be combined with AND
        var whereIndex = sql.IndexOf("WHERE", StringComparison.Ordinal);
        Assert.True(whereIndex >= 0);
        var afterWhere = sql[(whereIndex + 5)..];
        Assert.Contains(" AND ", afterWhere);
    }
}
