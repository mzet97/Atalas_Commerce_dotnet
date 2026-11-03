using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("Atlas.Gateway").AddOtlpExporter());
builder.Services.AddHealthChecks();

// BFF: leitura agregada das PROJEÇÕES para a vitrine (INV-CQRS-001: hint nunca confirma saldo)
var pgConn = builder.Configuration["Pg:ConnectionString"] ?? "Host=127.0.0.1;Username=atlas;Password=atlas_dev_only;Database=atlas_read";
var checkoutUrl = builder.Configuration["Checkout:Url"] ?? "http://127.0.0.1:5297";
var catalogUrl = builder.Configuration["Catalog:Url"] ?? "http://127.0.0.1:5299";
var inventoryUrl = builder.Configuration["Inventory:Url"] ?? "http://127.0.0.1:5291";

// borda HTTP dos services (registros ANTES do Build — coleção fica read-only depois)
builder.Services.AddHttpClient("checkout", c => c.BaseAddress = new Uri(checkoutUrl));
builder.Services.AddHttpClient("catalog-owner", c => c.BaseAddress = new Uri(catalogUrl));
builder.Services.AddHttpClient("inventory-owner", c => c.BaseAddress = new Uri(inventoryUrl));

var app = builder.Build();
app.MapHealthChecks("/health");

// ---- M2 (compat): lista simples de produtos publicados ----
app.MapGet("/api/catalog/products", async (CancellationToken ct) =>
{
    await using var conn = new NpgsqlConnection(pgConn);
    await conn.OpenAsync(ct);
    var products = new List<object>();
    await using (var cmd = new NpgsqlCommand(
        "SELECT product_id, title, market_id, status, source_version FROM catalog_product_search ORDER BY projected_at DESC LIMIT 50", conn))
    await using (var reader = await cmd.ExecuteReaderAsync(ct))
    {
        while (await reader.ReadAsync(ct))
        {
            products.Add(new
            {
                productId = reader.GetGuid(0),
                title = reader.GetString(1),
                marketId = reader.GetString(2),
                status = reader.GetString(3),
                sourceVersion = reader.GetInt64(4),
            });
        }
    }
    return Results.Ok(new { products, projection = "catalog_product_search", count = products.Count });
});

// ---- M5: busca com facets (req. UC-001) ----
app.MapGet("/api/catalog/search", async (string? q, string? market, string? sku, int? page, int? pageSize, CancellationToken ct) =>
{
    var pageEff = Math.Max(page ?? 1, 1);
    var sizeEff = Math.Clamp(pageSize ?? 12, 1, 50);
    await using var conn = new NpgsqlConnection(pgConn);
    await conn.OpenAsync(ct);
    var where = "WHERE status = 'Published' AND (CAST(@q AS text) IS NULL OR title ILIKE '%' || CAST(@q AS text) || '%') AND (CAST(@market AS text) IS NULL OR market_id = CAST(@market AS text)) AND (CAST(@sku AS text) IS NULL OR @sku = ANY(skus))";
    long total;
    var facets = new Dictionary<string, long>();
    await using (var countCmd = new NpgsqlCommand($"SELECT count(*) FROM catalog_product_search {where}", conn))
    {
        countCmd.Parameters.AddWithValue("q", (object?)q ?? DBNull.Value);
        countCmd.Parameters.AddWithValue("market", (object?)market ?? DBNull.Value);
        countCmd.Parameters.AddWithValue("sku", (object?)sku ?? DBNull.Value);
        total = (long)(await countCmd.ExecuteScalarAsync(ct))!;
    }
    await using (var facetCmd = new NpgsqlCommand(
        "SELECT market_id, count(*) FROM catalog_product_search WHERE status = 'Published' AND (CAST(@q AS text) IS NULL OR title ILIKE '%' || CAST(@q AS text) || '%') GROUP BY market_id", conn))
    {
        facetCmd.Parameters.AddWithValue("q", (object?)q ?? DBNull.Value);
        await using var fr = await facetCmd.ExecuteReaderAsync(ct);
        while (await fr.ReadAsync(ct)) facets[fr.GetString(0)] = fr.GetInt64(1);
    }
    var items = new List<object>();
    await using (var cmd = new NpgsqlCommand(
        $"SELECT product_id, title, market_id, status, source_version FROM catalog_product_search {where} ORDER BY projected_at DESC LIMIT @limit OFFSET @offset", conn))
    {
        cmd.Parameters.AddWithValue("q", (object?)q ?? DBNull.Value);
        cmd.Parameters.AddWithValue("market", (object?)market ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sku", (object?)sku ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", sizeEff);
        cmd.Parameters.AddWithValue("offset", (pageEff - 1) * sizeEff);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            items.Add(new { productId = r.GetGuid(0), title = r.GetString(1), marketId = r.GetString(2), status = r.GetString(3), sourceVersion = r.GetInt64(4) });
    }
    return Results.Ok(new { items, total, page = pageEff, pageSize = sizeEff, facets });
});

// ---- M5: PDP com ofertas do market + hint de disponibilidade (UC-001/002) ----
app.MapGet("/api/catalog/products/{productId:guid}", async (Guid productId, CancellationToken ct) =>
{
    await using var conn = new NpgsqlConnection(pgConn);
    await conn.OpenAsync(ct);
    string title, marketId, status;
    long version;
    string[] skus;
    await using (var cmd = new NpgsqlCommand(
        "SELECT title, market_id, status, source_version, skus FROM catalog_product_search WHERE product_id = @id", conn))
    {
        cmd.Parameters.AddWithValue("id", productId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return Results.NotFound();
        title = r.GetString(0); marketId = r.GetString(1); status = r.GetString(2); version = r.GetInt64(3);
        skus = r.IsDBNull(4) ? [] : r.GetFieldValue<string[]>(4);
    }
    var offers = new List<object>();
    await using (var ocmd = new NpgsqlCommand(
        "SELECT sku_id, market_id, currency, price_minor, status FROM available_offer_view " +
        "WHERE status = 'Active' AND sku_id = ANY(@skus) AND market_id = @market ORDER BY price_minor", conn))
    {
        ocmd.Parameters.AddWithValue("skus", skus);
        ocmd.Parameters.AddWithValue("market", marketId);
        await using var or_ = await ocmd.ExecuteReaderAsync(ct);
        while (await or_.ReadAsync(ct))
            offers.Add(new
            {
                skuId = or_.GetString(0),
                marketId = or_.GetString(1),
                currency = or_.GetString(2),
                priceMinor = or_.GetInt64(3),
                status = or_.GetString(4),
            });
    }
    var availability = new List<object>();
    await using (var icmd = new NpgsqlCommand(
        "SELECT sku_id, sum(on_hand - reserved) AS available FROM inventory_admin_view WHERE sku_id = ANY(@skus) GROUP BY sku_id", conn))
    {
        icmd.Parameters.AddWithValue("skus", skus);
        await using var ir = await icmd.ExecuteReaderAsync(ct);
        while (await ir.ReadAsync(ct))
            availability.Add(new { skuId = ir.GetString(0), availableHint = ir.IsDBNull(1) ? 0 : ir.GetInt64(1) });
    }
    // INV-CQRS-001: hint NUNCA confirma saldo — o checkout reserva no owner
    return Results.Ok(new { productId, title, marketId, status, sourceVersion = version, skus, offers, availabilityHint = availability });
});

// ---- M5: forwarder explícito /api/checkout/** → Checkout.Api (borda única da vitrine) ----
app.Map("/api/checkout/{**catch-all}", async (HttpContext http, IHttpClientFactory factory) =>
{
    // buffer explícito: payloads pequenos (dev); a borda lê o corpo ANTES do forward
    var body = http.Request.ContentLength is > 0
        ? await new StreamReader(http.Request.Body).ReadToEndAsync()
        : null;
    using var upstream = new HttpRequestMessage(new HttpMethod(http.Request.Method),
        checkoutUrl + http.Request.Path.Value!.Replace("/api/checkout", "") + http.Request.QueryString);
    if (body is not null)
        upstream.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    using var downstream = await factory.CreateClient("checkout").SendAsync(upstream);
    http.Response.StatusCode = (int)downstream.StatusCode;
    http.Response.ContentType = downstream.Content.Headers.ContentType?.ToString() ?? "application/json";
    await http.Response.WriteAsync(await downstream.Content.ReadAsStringAsync());
});

app.Run();

public partial class Program;
