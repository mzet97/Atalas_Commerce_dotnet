using System.Text.Json;
using Atlas.Checkout.Api;
using Atlas.Checkout.Application;
using Atlas.Checkout.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Atlas.Checkout.Api.Tests;

/// <summary>
/// E2E do fluxo Cart→Quote→Checkout no Checkout.Api com infraestrutura real
/// (replica set Mongo, db isolada por execução).
/// </summary>
public sealed class CheckoutFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CheckoutFlowTests(WebApplicationFactory<Program> factory)
    {
        var dbName = $"atlas_flow_{Guid.NewGuid():N}";
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Mongo:Database", dbName);
            b.UseSetting("Mongo:CheckoutDatabase", dbName);
            b.UseSetting("Mongo:OrdersDatabase", dbName);
            b.UseSetting("Mongo:PaymentsDatabase", dbName);
        });
    }

    private HttpClient Client => _factory.CreateClient();

    [Fact]
    public async Task Emite_quote_vigente_e_consulta_retorna_valid()
    {
        var response = await Client.PostAsJsonAsync("/quotes", new
        {
            cartId = "cart-flow",
            currency = "BRL",
            ttlSeconds = 600,
            lines = new[] { new { skuId = "sku-1", quantity = 2, unitPriceMinor = 2500L } },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var quoteId = body.GetProperty("quoteId").GetString()!;
        Assert.Equal(5000, body.GetProperty("totalMinor").GetInt64());

        var check = await Client.GetAsync($"/quotes/{quoteId}");
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        var checkBody = await check.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(checkBody.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task Quote_inexistente_retorna_404()
    {
        var response = await Client.GetAsync($"/quotes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Quote_com_ttl_zero_é_rejeitada_pela_rota()
    {
        var response = await Client.PostAsJsonAsync("/quotes", new
        {
            cartId = "c-ttl0",
            currency = "BRL",
            ttlSeconds = 0,
            lines = new[] { new { skuId = "sku-1", quantity = 1, unitPriceMinor = 100L } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_sem_quote_encontrada_rejeita_com_409()
    {
        var request = new
        {
            idempotencyKey = "chk-no-quote",
            customerId = "c1",
            quoteId = Guid.NewGuid().ToString(), // quote inexistente
            lines = new[] { new { skuId = "sku-1", quantity = 1, unitPriceMinor = 100L } },
            totalMinor = 100,
            currency = "BRL",
            shipping = new { country = "BR", postalCode = "000", line1 = "x", city = "y" },
        };
        var response = await Client.PostAsJsonAsync("/checkout", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("quote", body.GetProperty("reason").GetString()!.ToLowerInvariant());
    }
}
