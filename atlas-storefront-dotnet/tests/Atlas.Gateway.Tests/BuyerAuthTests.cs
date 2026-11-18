using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Atlas.Gateway.Tests;

/// <summary>
/// Fatia 9 (login buyer): BFF valida JWT (Keycloak em produção; HMAC de teste aqui)
/// e propaga o subject autenticado — nunca auto-assertado — ao owner de checkout.
/// TDD: falha até /api/me + injeção X-Authenticated-Subject existirem.
/// </summary>
public sealed class BuyerAuthTests
{
    private const string TestKey = "fatia9-chave-hmac-teste-com-32-bytes!!";

    private static string IssueToken(string sub, string? username = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", sub),
                new Claim("preferred_username", username ?? sub),
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
                SecurityAlgorithms.HmacSha256),
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static (WebApplicationFactory<Program> Factory, CaptureHandler Stub) BuildFactory()
    {
        var stub = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Auth:Mode", "TestHmac");
            b.UseSetting("Auth:TestSigningKey", TestKey);
            b.ConfigureTestServices(s => s.Configure<HttpClientFactoryOptions>("checkout",
                o => o.HttpMessageHandlerBuilderActions.Add(h => h.PrimaryHandler = stub)));
        });
        return (factory, stub);
    }

    [Fact]
    public async Task Me_sem_token_retorna_401()
    {
        var (factory, _) = BuildFactory();
        var res = await factory.CreateClient().GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_com_token_valido_retorna_sub()
    {
        var (factory, _) = BuildFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueToken("buyer-123"));
        var res = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("buyer-123", body.RootElement.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task Checkout_forwarder_injeta_subject_autenticado()
    {
        var (factory, stub) = BuildFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueToken("buyer-123"));
        var res = await client.PostAsync("/api/checkout/quotes",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(stub.LastRequest);
        Assert.True(stub.LastRequest!.Headers.Contains("X-Authenticated-Subject"));
        Assert.Equal("buyer-123", stub.LastRequest.Headers.GetValues("X-Authenticated-Subject").Single());
    }

    [Fact]
    public async Task Checkout_forwarder_anonimo_mantem_passthrough_sem_subject()
    {
        var (factory, stub) = BuildFactory();
        var res = await factory.CreateClient().PostAsync("/api/checkout/quotes",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(stub.LastRequest);
        Assert.False(stub.LastRequest!.Headers.Contains("X-Authenticated-Subject"));
    }

    [Fact]
    public async Task Checkout_forwarder_token_invalido_retorna_401()
    {
        var (factory, stub) = BuildFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token-lixo");
        var res = await client.PostAsync("/api/checkout/quotes",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Null(stub.LastRequest);
    }
}
