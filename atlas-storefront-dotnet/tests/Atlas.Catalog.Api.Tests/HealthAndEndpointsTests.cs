using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Atlas.Catalog.Api.Tests;

public class HealthAndEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthAndEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_responde_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Root_identifica_o_serviço()
    {
        var client = _factory.CreateClient();
        var body = await client.GetFromJsonAsync<RootResponse>("/");
        Assert.NotNull(body);
        Assert.Equal("Atlas.Catalog.Api", body.Service);
    }

    [Fact]
    public async Task Draft_de_demonstração_retorna_domínio_mínimo()
    {
        var client = _factory.CreateClient();
        var body = await client.GetFromJsonAsync<DraftResponse>($"/catalog/drafts/{Guid.NewGuid()}");
        Assert.NotNull(body);
        Assert.Equal("Draft", body.Status);
        Assert.NotEmpty(body.Titles);
    }

    private sealed record RootResponse(string Service, string Milestone, string Status);
    private sealed record DraftResponse(Guid Id, string Status, object[] Titles);
}
