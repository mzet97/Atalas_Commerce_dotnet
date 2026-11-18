using Npgsql;

namespace Atlas.Workers.Projections;

/// <summary>
/// Schema das views M2 — CREATE IF NOT EXISTS no arranque do worker
/// (migrations formais EF Core chegam na SPEC-003+; ADR-0011).
/// </summary>
public static class ProjectionSchema
{
    public static async Task EnsureAsync(
        string pgConn, string catalogTable, string offersTable,
        string positionsTable, string processedTable, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(pgConn);
        await conn.OpenAsync(ct);
        // Least-privilege (homelab: app_service = CRUD, sem DDL): se as 4 tabelas
        // já existem (migração db_admin/SET ROLE app_owner), pula o DDL — um SELECT
        // que o role de runtime sempre pode fazer. Sem as tabelas, o CREATE abaixo
        // falha alto (42501), sinalizando migração pendente em vez de mascarar.
        var wanted = new[] { catalogTable, offersTable, positionsTable, processedTable };
        await using (var check = new NpgsqlCommand(
            "SELECT count(DISTINCT tablename) FROM pg_tables WHERE schemaname = ANY(current_schemas(false)) AND tablename = ANY(@names)", conn))
        {
            check.Parameters.AddWithValue("names", wanted);
            var existing = (long)(await check.ExecuteScalarAsync(ct))!;
            if (existing >= wanted.Length) return;
        }
        await using var cmd = new NpgsqlCommand($"""
            CREATE TABLE IF NOT EXISTS {catalogTable}(product_id uuid PRIMARY KEY, title text, market_id text, status text, source_version bigint, projected_at timestamptz, skus text[] DEFAULT ARRAY[]::text[]);
            ALTER TABLE {catalogTable} ADD COLUMN IF NOT EXISTS skus text[] DEFAULT ARRAY[]::text[];
            CREATE TABLE IF NOT EXISTS {offersTable}(offer_id uuid PRIMARY KEY, sku_id text, seller_id text, market_id text, channel text, currency text, price_minor bigint, status text, source_version bigint, projected_at timestamptz);
            CREATE TABLE IF NOT EXISTS {positionsTable}(position_key text PRIMARY KEY, sku_id text, on_hand int, reserved int, source_version bigint, projected_at timestamptz);
            CREATE TABLE IF NOT EXISTS {processedTable}(consumer_id text, event_id uuid, applied_at timestamptz DEFAULT now(), PRIMARY KEY(consumer_id, event_id));
            """, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
