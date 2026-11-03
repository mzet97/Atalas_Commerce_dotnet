using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using Atlas.Control.Modules.Approvals;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Control.Host.Tests;

/// <summary>
/// Four-eyes REAL (TEST-002-008/TEST-000-007): Keycloak real (realm atlas, 127.0.0.1:8090),
/// Mongo real (estado próprio do Control), JWT real validado nas bordas, token HMAC despachado.
/// Setup via Admin API: dois clients de teste com service-accounts e roles distintas
/// (editor → catalog_editor; approver → approver) — quatro olhos de princípios distintos.
/// </summary>
public sealed class FourEyesFlowTests : IAsyncLifetime
{
    private const string Keycloak = "http://127.0.0.1:8090";
    private const string AdminSecret = "kc-admin-dev-only";
    private const string MongoConn = "mongodb://127.0.0.1:27017/?directConnection=true";
    private readonly string _dbName = $"atlas_control_{Guid.NewGuid():N}";

    private WebApplicationFactory<Program> _factory = default!;
    private HttpClient _client = default!;
    private string _editorToken = default!;
    private string _approverToken = default!;
    private string _editorSub = default!;
    private string _approverSub = default!;
    private string _nobodyToken = default!;
    private string _nobodySub = default!;

    public async Task InitializeAsync()
    {
        // hosts de roles de teste (idempotente): editor e approver
        var editorClient = await EnsureClientWithRolesAsync("atlas-editor-dev", ["catalog_editor", "approver"]);
        var nobodyClient = await EnsureClientWithRolesAsync("atlas-nobody-dev", []);
        var approverClient = await EnsureClientWithRolesAsync("atlas-approver-dev", ["approver"]);
        (_editorToken, _editorSub) = await ServiceAccountTokenAsync(editorClient.ClientId, editorClient.Secret);
        (_approverToken, _approverSub) = await ServiceAccountTokenAsync(approverClient.ClientId, approverClient.Secret);
        (_nobodyToken, _nobodySub) = await ServiceAccountTokenAsync(nobodyClient.ClientId, nobodyClient.Secret);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Mongo:Database", _dbName);
            b.UseSetting("Keycloak:Authority", $"{Keycloak}/realms/atlas");
            // owner FAKE no teste de fluxo do Control (o caminho gRPC→owner real é provado
            // nos testes do Catalog.Api — TEST-002-002; smoke cross-process no close do gate)
            b.ConfigureServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(ApprovalsModule.IOwner));
                services.Remove(descriptor);
                services.AddSingleton<ApprovalsModule.IOwner>(new FakeOwner());
            });
        });
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await new MongoClient(MongoConn).GetDatabase(_dbName).Client.DropDatabaseAsync(_dbName);
        _factory.Dispose();
    }

    // ---------- setup Keycloak ----------
    private static async Task<string> AdminTokenAsync()
    {
        var body = new Dictionary<string, string>
        {
            ["client_id"] = "admin-cli", ["username"] = "admin",
            ["password"] = AdminSecret, ["grant_type"] = "password",
        };
        var response = await new HttpClient().PostAsync(
            $"{Keycloak}/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(body));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    private static async Task<(string ClientId, string Secret)> EnsureClientWithRolesAsync(string clientId, string[] roles)
    {
        var http = new HttpClient();
        var admin = await AdminTokenAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin);

        var existing = await http.GetFromJsonAsync<JsonElement>($"{Keycloak}/admin/realms/atlas/clients?clientId={clientId}");
        string id = "";
        string secret = "";
        if (existing!.GetArrayLength() == 0)
        {
            var create = await http.PostAsJsonAsync($"{Keycloak}/admin/realms/atlas/clients", new
            {
                clientId,
                protocol = "openid-connect",
                publicClient = false,
                secret = $"{clientId}-secret",
                serviceAccountsEnabled = true,
                standardFlowEnabled = false,
                directAccessGrantsEnabled = true,
            });
            create.EnsureSuccessStatusCode();
            id = (await http.GetFromJsonAsync<JsonElement>($"{Keycloak}/admin/realms/atlas/clients?clientId={clientId}"))!
                [0].GetProperty("id").GetString()!;
        }
        else
        {
            id = existing[0].GetProperty("id").GetString()!;
            secret = $"{clientId}-secret"; // seguido no POST de criação deste setup (dev-only)
        }

        // service account user recebe os realm roles (criação é assíncrona — retry)
        var svcUser = "";
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var saResponse = await http.GetAsync($"{Keycloak}/admin/realms/atlas/clients/{id}/service-account-user");
            if (saResponse.IsSuccessStatusCode)
            {
                svcUser = (await saResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
                break;
            }
            await Task.Delay(1000);
        }
        Assert.NotEqual("", svcUser);
        var allRoles = await http.GetFromJsonAsync<JsonElement>($"{Keycloak}/admin/realms/atlas/roles");
        var roleIds = allRoles.EnumerateArray()
            .Where(r => roles.Contains(r.GetProperty("name").GetString()))
            .Select(r => new { id = r.GetProperty("id").GetString(), name = r.GetProperty("name").GetString() })
            .ToArray();
        await http.PostAsJsonAsync($"{Keycloak}/admin/realms/atlas/users/{svcUser}/role-mappings/realm", roleIds);
        return (clientId, secret);
    }

    private static async Task<(string Token, string Sub)> ServiceAccountTokenAsync(string clientId, string secret)
    {
        var body = new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["client_secret"] = secret, ["grant_type"] = "client_credentials",
        };
        var response = await new HttpClient().PostAsync(
            $"{Keycloak}/realms/atlas/protocol/openid-connect/token", new FormUrlEncodedContent(body));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("access_token").GetString()!;
        var payload = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(
            System.Convert.FromBase64String(Pad(token.Split('.')[1])));
        return (token, payload.GetProperty("sub").GetString()!);
    }

    private static string Pad(string b64) => b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');

    /// <summary>Owner fake: aceita sempre (validação real do token é do owner Catalog — já provada lá).</summary>
    private sealed class FakeOwner : ApprovalsModule.IOwner
    {
        public Task<ApprovalsModule.OwnerPublishResponse> PublishProductAsync(
            ApprovalsModule.OwnerPublishRequest request, CancellationToken ct) =>
            Task.FromResult(new ApprovalsModule.OwnerPublishResponse(true, Guid.NewGuid().ToString(), null));
    }

    // ---------- helpers de fluxo ----------
    private static HttpRequestMessage Post(string route, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<int> TotalAuditAsync()
    {
        var db = new MongoClient(MongoConn).GetDatabase(_dbName);
        return (int)await db.GetCollection<MongoDB.Bson.BsonDocument>("control.audit_chain").CountDocumentsAsync(new MongoDB.Bson.BsonDocument());
    }

    private async Task<int> AuditCountAsync(string decision)
    {
        var db = new MongoClient(MongoConn).GetDatabase(_dbName);
        var count = await db.GetCollection<MongoDB.Bson.BsonDocument>("control.audit_chain")
            .CountDocumentsAsync(new MongoDB.Bson.BsonDocument("Decision", decision));
        return (int)count;
    }

    [Fact]
    public async Task Four_eyes_completo_request_decide_dispatch_com_owner_fake()
    {
        var productId = Guid.NewGuid().ToString();

        // 1) editor solicita (role catalog_editor)
        var requestResponse = await _client.SendAsync(Post("/approvals/requests", new
        {
            productId, expectedVersion = 2, marketId = "BR",
            justification = "publicação aprovada no board", correlationId = "corr-fe-1",
        }, _editorToken));
        Assert.Equal(HttpStatusCode.Created, requestResponse.StatusCode);
        var approvalId = (await requestResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("approvalId").GetString()!;

        // binding do body: ProductId DEVE chegar ao agregado (bug M2 smoke: token com product_id vazio)
        var approvalDoc = await new MongoClient(MongoConn).GetDatabase(_dbName)
            .GetCollection<MongoDB.Bson.BsonDocument>("control.approvals")
            .Find(new MongoDB.Bson.BsonDocument("_id", approvalId)).SingleAsync();
        Assert.Equal(productId, approvalDoc["ProductId"].AsString);

        // 2) mesmo principal decide → 409 + audit DENIED_SELF_APPROVAL
        var self = await _client.SendAsync(Post($"/approvals/{approvalId}/decide", new
        {
            decision = "APPROVED", correlationId = "corr-fe-2",
        }, _editorToken));
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        var selfBody = await self.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(selfBody.GetProperty("audited").GetBoolean());
        var total = await TotalAuditAsync();
        var selfAudits = await AuditCountAsync("DENIED_SELF_APPROVAL");
        Assert.True(selfAudits == 1, $"selfAudits={selfAudits} total={total}");

        // 2b) principal SEM role approver → 403 auditado (policy antes do domínio)
        var forbidden = await _client.SendAsync(Post($"/approvals/{approvalId}/decide", new
        {
            decision = "APPROVED", correlationId = "corr-fe-2b",
        }, _nobodyToken));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var total2 = await TotalAuditAsync();
        var forbiddenAudit = await AuditCountAsync($"DENIED_403 POST /approvals/{approvalId}/decide");
        Assert.True(forbiddenAudit == 1, $"forbiddenAudit={forbiddenAudit} total={total2} body={await forbidden.Content.ReadAsStringAsync()}");

        // 3) approver distinto decide → token HMAC emitido
        var decide = await _client.SendAsync(Post($"/approvals/{approvalId}/decide", new
        {
            decision = "APPROVED", correlationId = "corr-fe-3",
        }, _approverToken));
        Assert.True(decide.StatusCode == HttpStatusCode.OK, $"decide={decide.StatusCode} body={await decide.Content.ReadAsStringAsync()}"); var token = (await decide.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("approvalToken").GetString()!; Assert.False(string.IsNullOrEmpty(token));

        // 4) 403 sem role: principal SEM nenhum papel tenta DISPATCH (policy approver) → negado + audit
        var denied = await _client.SendAsync(Post($"/approvals/{approvalId}/dispatch", new
        {
            correlationId = "corr-fe-4",
        }, _nobodyToken));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var deniedCount = await AuditCountAsync("DENIED_403 POST /approvals/" + approvalId + "/dispatch");
        Assert.Equal(1, deniedCount);

        // 5) approver despacha → owner fake aceita → Executed + audit EXECUTED
        var dispatch = await _client.SendAsync(Post($"/approvals/{approvalId}/dispatch", new
        {
            correlationId = "corr-fe-5",
        }, _approverToken));
        Assert.Equal(HttpStatusCode.OK, dispatch.StatusCode);
        var dispatchBody = await dispatch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Executed", dispatchBody.GetProperty("state").GetString());
        Assert.Equal(1, await AuditCountAsync("EXECUTED"));
    }
}
