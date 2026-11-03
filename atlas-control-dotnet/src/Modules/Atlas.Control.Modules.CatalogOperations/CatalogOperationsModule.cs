using Atlas.Control.Modules.Contracts;
using Results = Microsoft.AspNetCore.Http.Results;

namespace Atlas.Control.Modules.CatalogOperations;

/// <summary>
/// Módulo de operações de catálogo (UC-005): autentica/autoriza o operador, registra
/// intenção/aprovação e comanda o Catalog owner por gRPC (chega na SPEC-002).
/// M1: consulta de saúde/status do Catalog owner via HTTP para o smoke de trace
/// cross-process (TEST-001-004) — propagação W3C automática pelo HttpClient instrumentado.
/// </summary>
public sealed class CatalogOperationsModule(IHttpClientFactory httpFactory) : IAtlasModule
{
    public string Name => "catalog-operations";

    public void Configure(IModuleEndpoints endpoints) =>
        endpoints.MapGet("/modules/catalog-operations/status", async (CancellationToken ct) =>
        {
            var baseAddress = Environment.GetEnvironmentVariable("CATALOG_BASE_URL") ?? "http://127.0.0.1:5219";
            var client = httpFactory.CreateClient("catalog");
        client.BaseAddress = new Uri(baseAddress);
            try
            {
                using var response = await client.GetAsync("/", ct);
                var upstream = await response.Content.ReadAsStringAsync(ct);
                return Results.Ok(new
                {
                    module = Name,
                    milestone = "M1",
                    catalogOwner = new { reachable = response.IsSuccessStatusCode, upstream },
                });
            }
            catch (HttpRequestException ex)
            {
                return Results.Ok(new { module = Name, catalogOwner = new { reachable = false, error = ex.Message } });
            }
        });
}
