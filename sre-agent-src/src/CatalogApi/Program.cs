using CatalogApi.Models;
using CatalogApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<CosmosDbService>();
builder.Services.AddSingleton<OrderValidationService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Catalog API - Swagger UI";
});

// ---------------------------------------------------------------------------
// Cosmos DB initialization (non-blocking — app starts even if DB is unreachable)
// ---------------------------------------------------------------------------
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            var cosmosDb = app.Services.GetRequiredService<CosmosDbService>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await cosmosDb.InitializeAsync().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            app.Logger.LogWarning(
                "Cosmos DB initialization timed out during background startup. The app remains online and will retry on request path.");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex,
                "Cosmos DB initialization failed in background. The app remains online, but database requests may return errors.");
        }
    });
});

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

// Home page - quick status and navigation links for key API endpoints
app.MapGet("/", () =>
{
        const string html = """
<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Catalog API</title>
    <link rel="stylesheet"
          href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css"
          integrity="sha384-QWTKZyjpPEjISv5WaRU9OFeRpok6YctnYmDr5pNlyT2bRjXh0JMhjY6hW+ALEwIH"
          crossorigin="anonymous" />
</head>
<body class="bg-light">
    <div class="container py-4" style="max-width:760px">
        <div class="card shadow-sm">
            <div class="card-body">
                <h1 class="card-title h4 mb-1">Catalog API is running</h1>
                <p class="text-muted">Use the links below to test core endpoints.</p>
                <ul class="list-unstyled mb-2">
                    <li><a href="/health">GET /health</a> &ndash; service and Cosmos connectivity check</li>
                    <li><a href="/products">GET /products</a> &ndash; list all products</li>
                    <li>
                        <a href="/products/search">GET /products/search</a> &ndash; search products (in-stock only by default)
                        <br />
                        <span class="ms-3 text-muted small">
                            Query params: <code>keyword</code>, <code>category</code>, <code>inStockOnly</code> (bool, default <code>true</code>)
                        </span>
                        <br />
                        <span class="ms-3 text-muted small">
                            Backorderable items include <code class="text-warning-emphasis">"isBackorderable": true</code> &mdash;
                            display these with a
                            <span class="badge bg-warning text-dark">Backorder</span> badge.
                        </span>
                    </li>
                    <li><a href="/orders">GET /orders</a> &ndash; list orders</li>
                    <li><a href="/swagger">Swagger UI</a> &ndash; interactive API explorer</li>
                    <li><a href="/swagger/v1/swagger.json">OpenAPI JSON</a> &ndash; API schema</li>
                </ul>
                <p class="mb-1">Write endpoints:</p>
                <ul class="list-unstyled">
                    <li><code>POST /products</code></li>
                    <li><code>POST /orders</code></li>
                </ul>
            </div>
        </div>
    </div>
</body>
</html>
""";

        return Results.Content(html, "text/html");
});

// Health check — validates Cosmos DB connectivity
app.MapGet("/health", async (CosmosDbService db) =>
{
    var healthy = await db.CheckHealthAsync();
    return healthy
        ? Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow })
        : Results.Json(new { status = "unhealthy", timestamp = DateTime.UtcNow }, statusCode: 503);
});

// GET /products — list all products
app.MapGet("/products", async (CosmosDbService db) =>
{
    var products = await db.GetProductsAsync();
    return Results.Ok(products);
});

// GET /products/search — search in-stock products (keyword / category / inStockOnly)
// Must be registered BEFORE /products/{id} so the literal "search" segment is not
// mistaken for an {id} capture.
app.MapGet("/products/search", async (
    string? keyword,
    string? category,
    bool? inStockOnly,
    CosmosDbService db) =>
{
    var products = await db.SearchProductsAsync(keyword, category, inStockOnly ?? true);
    return Results.Ok(products);
}).WithName("SearchProducts");

// GET /products/{id} — get a single product by id
app.MapGet("/products/{id}", async (string id, CosmosDbService db) =>
{
    var product = await db.GetProductByIdAsync(id);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

// POST /products — create a new product
app.MapPost("/products", async (Product product, CosmosDbService db) =>
{
    var created = await db.CreateProductAsync(product);
    return Results.Created($"/products/{created.Id}", created);
});

// GET /orders — list all orders
app.MapGet("/orders", async (CosmosDbService db) =>
{
    var orders = await db.GetOrdersAsync();
    return Results.Ok(orders);
});

// GET /orders/{id} — get a single order by id
app.MapGet("/orders/{id}", async (string id, CosmosDbService db) =>
{
    var order = await db.GetOrderByIdAsync(id);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

// POST /orders — create a new order (can trigger CPU spike in strict mode)
app.MapPost("/orders", async (Order order, CosmosDbService db, OrderValidationService validator) =>
{
    var validation = validator.ValidateOrder(order);
    if (!validation.IsValid)
        return Results.BadRequest(new { error = validation.Error });

    var created = await db.CreateOrderAsync(order);
    return Results.Created($"/orders/{created.Id}", created);
});

app.Run();
