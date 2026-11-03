using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Atlas.Control.Host.Tests;

public class HostHealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task Health_responde_200()
    {
        var response = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Módulos_são_compostos_no_host()
    {
        var client = _factory.CreateClient();
        var catalog = await client.GetFromJsonAsync<Status>("/modules/catalog-operations/status");
        var approvals = await client.GetFromJsonAsync<Status>("/modules/approvals/status");
        Assert.NotNull(catalog);
        Assert.NotNull(approvals);
        Assert.Equal("catalog-operations", catalog.Module);
        Assert.Equal("approvals", approvals.Module);
    }

    private sealed record Status(string Module);
}
