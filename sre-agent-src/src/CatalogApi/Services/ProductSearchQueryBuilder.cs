namespace CatalogApi.Services;

/// <summary>
/// Builds parameterized Cosmos DB SQL queries for product search with query-level inventory filtering.
/// This class is pure (no I/O) so its logic can be tested without a live database.
/// </summary>
public static class ProductSearchQueryBuilder
{
    /// <summary>
    /// Builds a Cosmos DB SQL string and its named parameters for a product search.
    ///
    /// In-stock rule (applied as a SQL WHERE predicate, not in-memory):
    ///   A product is "purchasable" when ANY of the following hold:
    ///     • stock &gt; 0
    ///     • isBackorderable = true  (0-stock item that can still be ordered)
    ///     • inventoryStatus IN ('in_stock', 'preorder')  (explicit status overrides stock count)
    ///
    /// Products with stock = 0, isBackorderable = false, and no positive inventoryStatus
    /// are excluded from results by default.
    /// </summary>
    /// <param name="keyword">Optional keyword matched against name, description, and category.</param>
    /// <param name="category">Optional exact category filter (case-insensitive).</param>
    /// <param name="inStockOnly">
    ///   When <c>true</c> (the default) the inventory predicate is added to the query.
    ///   Pass <c>false</c> only to retrieve all products including out-of-stock ones.
    /// </param>
    /// <returns>
    ///   A tuple of the SQL string and a dictionary of parameter name → value pairs
    ///   ready to be applied via <c>QueryDefinition.WithParameter</c>.
    /// </returns>
    public static (string Sql, Dictionary<string, object> Parameters) Build(
        string? keyword = null,
        string? category = null,
        bool inStockOnly = true)
    {
        var conditions = new List<string>();
        var parameters = new Dictionary<string, object>(StringComparer.Ordinal);

        // ── Inventory filter (query-level — Cosmos DB does the filtering) ──────
        // Excludes products that are out of stock and NOT backorderable/preorderable.
        if (inStockOnly)
        {
            conditions.Add(
                "(c.stock > 0 " +
                "OR c.isBackorderable = true " +
                "OR c.inventoryStatus IN ('in_stock', 'preorder'))");
        }

        // ── Keyword filter ─────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            conditions.Add(
                "(CONTAINS(LOWER(c.name), @keyword) " +
                "OR CONTAINS(LOWER(c.description), @keyword) " +
                "OR CONTAINS(LOWER(c.category), @keyword))");
            parameters["@keyword"] = keyword.ToLowerInvariant();
        }

        // ── Category filter ────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(category))
        {
            conditions.Add("LOWER(c.category) = @category");
            parameters["@category"] = category.ToLowerInvariant();
        }

        var sql = conditions.Count > 0
            ? $"SELECT * FROM c WHERE {string.Join(" AND ", conditions)}"
            : "SELECT * FROM c";

        return (sql, parameters);
    }
}
