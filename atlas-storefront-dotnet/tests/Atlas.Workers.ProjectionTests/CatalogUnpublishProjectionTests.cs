using System.Text.Json;
using Atlas.Workers.Projections;
using Npgsql;
using Xunit;

namespace Atlas.Workers.Projections.Tests;

/// <summary>
/// Despublicação na vitrine: Product.UnpublishedV1 remove a linha da busca.
/// Local-infra como os demais deste projeto (PG 127.0.0.1 atlas/atlas_dev_only);
/// tabelas efêmeras por execução (drop no Dispose).
/// </summary>
public sealed class CatalogUnpublishProjectionTests : IAsyncLifetime
{
    private const string PgConn = "Host=127.0.0.1;Username=atlas;Password=atlas_dev_only;Database=atlas_read";

    private readonly string _productsTable = $"catalog_product_search_it_{Guid.NewGuid():N}";
    private readonly string _processedTable = $"processed_events_it_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        await new NpgsqlCommand($"""
            CREATE TABLE {_productsTable}(
                product_id uuid PRIMARY KEY, title text, market_id text,
                status text, source_version bigint, projected_at timestamptz,
                skus text[] DEFAULT ARRAY[]::text[]);
            CREATE TABLE {_processedTable}(
                consumer_id text, event_id uuid, applied_at timestamptz DEFAULT now(),
                PRIMARY KEY(consumer_id, event_id));
            """, conn).ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        await new NpgsqlCommand($"DROP TABLE IF EXISTS {_productsTable}; DROP TABLE IF EXISTS {_processedTable};", conn).ExecuteNonQueryAsync();
    }

    private static DomainEventEnvelope Env(string type, Guid id, long version, string payloadJson) =>
        new(Guid.NewGuid(), type, id.ToString(), version, "corr-it",
            JsonDocument.Parse(payloadJson).RootElement.Clone());

    private async Task<long> CountAsync(Guid productId)
    {
        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        return (long)(await new NpgsqlCommand(
            $"SELECT count(*) FROM {_productsTable} WHERE product_id = @id", conn)
        { Parameters = { new NpgsqlParameter("id", productId) } }.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Unpublished_remove_produto_da_busca()
    {
        var productId = Guid.NewGuid();
        var projector = new CatalogProjector(PgConn, "catalog-search-it", _productsTable, _processedTable);

        var published = Env("Product.PublishedV1", productId, 3,
            $$"""{"product_id":"{{productId}}","market_id":"BR","title":"Tênis IT","sku_codes":["ATL-IT-1"]}""");
        Assert.Equal(1, await projector.ApplyBatchAsync([published], CancellationToken.None));
        Assert.Equal(1, await CountAsync(productId));

        var unpublished = Env("Product.UnpublishedV1", productId, 4,
            $$"""{"product_id":"{{productId}}","market_id":"BR","actor_sub":"op","reason":"fora de linha"}""");
        Assert.Equal(1, await projector.ApplyBatchAsync([unpublished], CancellationToken.None));
        Assert.Equal(0, await CountAsync(productId));
    }

    [Fact]
    public async Task Unpublished_antigo_nao_remove_projecao_nova()
    {
        var productId = Guid.NewGuid();
        var projector = new CatalogProjector(PgConn, "catalog-search-it", _productsTable, _processedTable);

        var published = Env("Product.PublishedV1", productId, 5,
            $$"""{"product_id":"{{productId}}","market_id":"BR","title":"Tênis IT","sku_codes":[]}""");
        Assert.Equal(1, await projector.ApplyBatchAsync([published], CancellationToken.None));

        // evento defasado (versão menor que a projetada): dedup já o viu? não — event_id novo,
        // mas o stale guard impede a remoção (INV-EVT-001/out-of-order).
        var stale = Env("Product.UnpublishedV1", productId, 3,
            $$"""{"product_id":"{{productId}}","market_id":"BR","actor_sub":"op","reason":"tardio"}""");
        await projector.ApplyBatchAsync([stale], CancellationToken.None);
        Assert.Equal(1, await CountAsync(productId));
    }
}
