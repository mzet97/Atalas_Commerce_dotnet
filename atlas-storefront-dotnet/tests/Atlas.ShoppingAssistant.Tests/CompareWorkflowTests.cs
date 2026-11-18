using System.Net;
using System.Text;
using System.Text.Json;
using Atlas.ShoppingAssistant.Api.Metrics;
using Atlas.ShoppingAssistant.Api.Tools;
using Atlas.ShoppingAssistant.Api.Workflows;
using Xunit;

namespace Atlas.ShoppingAssistant.Tests;

/// <summary>
/// F2 (SPEC-007+): grafo MAF Workflows REAL (Microsoft.Agents.AI.Workflows 1.26.0,
/// mesma versão pinada do MAF) — search → details+hint → rank → propose.
/// Sem LLM: executores chamam as tools read-only via HTTP stub; teste 100% local.
/// TDD: falha até CompareShoppingWorkflow existir com este contrato.
/// </summary>
public sealed class CompareWorkflowTests
{
    private sealed class CannedHandler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(respond(request), Encoding.UTF8, "application/json")
            });
    }

    private static CatalogTools BuildTools(AssistantMetrics? metrics = null)
    {
        string Respond(HttpRequestMessage req)
        {
            var path = req.RequestUri!.PathAndQuery;
            if (path.StartsWith("/api/catalog/products/", StringComparison.Ordinal))
            {
                var id = path.Split('/').Last();
                // id "p2" é o mais barato (9000 minor); "p1" custa 15000
                var price = id == "p2" ? 9000 : 15000;
                return JsonSerializer.Serialize(new
                {
                    title = id == "p2" ? "Câmera Atlas Pro X200" : "Câmera Atlas Lite",
                    marketId = "BR",
                    status = "Published",
                    source_version = 3L,
                    skus = new[] { id == "p2" ? "SKU-CAMERA-BR" : "SKU-LITE-BR" },
                    offers = new[] { new { priceMinor = price, currency = "BRL" } }
                });
            }
            return JsonSerializer.Serialize(new
            {
                items = new[]
                {
                    new { title = "Câmera Atlas Lite", marketId = "BR", productId = "p1" },
                    new { title = "Câmera Atlas Pro X200", marketId = "BR", productId = "p2" },
                },
                total = 2L,
                page = 1,
                pageSize = 12,
                facets = new { BR = 2L }
            });
        }
        var http = new HttpClient(new CannedHandler(Respond)) { BaseAddress = new Uri("http://stub/") };
        return new CatalogTools(http, "http://stub", metrics);
    }

    [Fact]
    public async Task Compare_retorna_ranking_com_menor_preco_primeiro()
    {
        var workflow = new CompareShoppingWorkflow(BuildTools());
        var result = await workflow.CompareAsync("câmera", "BR", CancellationToken.None);

        Assert.Contains("Câmera Atlas Pro X200", result);
        Assert.Contains("Câmera Atlas Lite", result);
        var cheap = result.IndexOf("Câmera Atlas Pro X200", StringComparison.Ordinal);
        var expensive = result.IndexOf("Câmera Atlas Lite", StringComparison.Ordinal);
        Assert.True(cheap >= 0 && expensive >= 0 && cheap < expensive,
            "o mais barato (Pro X200, 9000) deve vir antes do Lite (15000)");
    }

    [Fact]
    public async Task Compare_emite_proposta_inerte_para_o_mais_barato()
    {
        var workflow = new CompareShoppingWorkflow(BuildTools());
        var result = await workflow.CompareAsync("câmera", "BR", CancellationToken.None);

        Assert.Contains("PROPOSTA INERTE", result);
        Assert.Contains("SKU-CAMERA-BR", result);
    }

    [Fact]
    public async Task Compare_sem_produtos_retorna_mensagem_honesta()
    {
        var http = new HttpClient(new CannedHandler(_ => JsonSerializer.Serialize(new
        {
            items = Array.Empty<object>(),
            total = 0L,
            page = 1,
            pageSize = 12,
            facets = new { }
        })))
        { BaseAddress = new Uri("http://stub/") };
        var workflow = new CompareShoppingWorkflow(new CatalogTools(http, "http://stub"));

        var result = await workflow.CompareAsync("inexistente", "BR", CancellationToken.None);

        Assert.Contains("nenhum produto", result, StringComparison.OrdinalIgnoreCase);
    }
}
