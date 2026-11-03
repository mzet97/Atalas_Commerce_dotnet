using Atlas.Control.Host;
using Atlas.Control.Modules.Contracts;
using Atlas.Control.Modules.Approvals;
using Atlas.Control.Modules.InventoryOperations;
using Atlas.Control.Modules.Payments;
using Atlas.Control.Modules.Kernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("Atlas.Control").AddOtlpExporter()); // OTLP via env
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

// ---------- IAM (ADR-0007): JWT real do Keycloak, realm atlas ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Keycloak:Authority"] ?? "http://127.0.0.1:8090/realms/atlas";
        o.RequireHttpsMetadata = false; // dev on-prem; produção TLS (M8)
        o.MapInboundClaims = false; // claims originais (sub, realm_access) — mapeamento default quebrava o sub
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = o.Authority,
            ValidateAudience = false, // Keycloak: azp no lugar de aud; roles validadas por policy
            ValidateLifetime = true,
            NameClaimType = "sub",
        };
    });

// realm_access.roles (formato Keycloak) → ClaimTypes.Role (para RequireRole nas policies)
builder.Services.AddSingleton<Microsoft.AspNetCore.Authentication.IClaimsTransformation, KeycloakRolesClaimsTransformation>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("role:catalog_editor", p => p.RequireRole("catalog_editor"));
    options.AddPolicy("role:approver", p => p.RequireRole("approver"));
    options.AddPolicy("role:inventory_operator", p => p.RequireRole("inventory_operator"));
    options.AddPolicy("role:iam_admin", p => p.RequireRole("iam_admin"));
    options.AddPolicy("role:auditor", p => p.RequireRole("auditor"));
});

// ---------- estado próprio do Control (ADR-0001): approvals/audit em Mongo dedicado ----------
var mongoConn = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017/?directConnection=true";
var mongoDb = builder.Configuration["Mongo:Database"] ?? "atlas_control";
builder.Services.AddSingleton(_ => ApprovalsStore.Create(mongoConn, mongoDb));
builder.Services.AddSingleton(_ => OperationApprovalsStore.Create(mongoConn, mongoDb));
builder.Services.AddSingleton(_ => AuditChainStore.Create(mongoConn, mongoDb));
builder.Services.AddSingleton(_ => new OperationsOverview(new MongoDB.Driver.MongoClient(mongoConn)));
builder.Services.AddSingleton<ControlAuditSink>();

// ---------- owner dispatcher (gRPC real; URL configurável p/ ambientes) ----------
builder.Services.AddSingleton<ApprovalsModule.IOwner>(sp =>
    new GrpcCatalogOwnerDispatcher(sp.GetRequiredService<IConfiguration>()["CatalogOwner:Url"] ?? "http://127.0.0.1:5219"));

builder.Services.AddHttpClient("catalog");
builder.Services.AddSingleton<IAtlasModule>(sp => new Atlas.Control.Modules.CatalogOperations.CatalogOperationsModule(
    sp.GetRequiredService<IHttpClientFactory>()));
builder.Services.AddSingleton<IInventoryStockOwner>(sp =>
    new GrpcInventoryOwnerDispatcher(sp.GetRequiredService<IConfiguration>()["InventoryOwner:Url"] ?? "http://127.0.0.1:5292"));
builder.Services.AddSingleton<IAtlasModule>(sp => new RefundApprovalsModule(
    sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IAtlasModule>(sp => new InventoryOperationsModule(
    sp.GetRequiredService<OperationApprovalsStore>(),
    sp.GetRequiredService<AuditChainStore>(),
    sp.GetRequiredService<IInventoryStockOwner>(),
    sp.GetRequiredService<OperationsOverview>(),
    sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IAtlasModule>(sp => new ApprovalsModule(
    sp.GetRequiredService<ApprovalsStore>(),
    sp.GetRequiredService<AuditChainStore>(),
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<ApprovalsModule.IOwner>()));
var app = builder.Build();

app.MapOpenApi();
app.MapHealthChecks("/health");

app.UseAuthentication();

// 403 AUDITADO (INV-SEC-001 / REQ-ADM-003): ANTES do UseAuthorization — o middleware de
// autorização faz short-circuit no Forbid (não chama next), então só middlewares ANTERIORES veem o 403.
app.Use(async (context, next) =>
{
    await next(context);
    if (context.Response.StatusCode == StatusCodes.Status403Forbidden)
    {
        var sub = context.User.FindFirst("sub")?.Value ?? context.User.Identity?.Name ?? "anonymous";
        var route = context.Request.Path.Value ?? "?";
        try
        {
            await context.RequestServices.GetRequiredService<ControlAuditSink>()
                .AppendAsync(sub, "http.denied", $"DENIED_403 {context.Request.Method} {route}", null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText("/tmp/audit-mw.log", $"ERRO: {ex.GetType().Name}: {ex.Message}\n");
        }
    }
});

app.UseAuthorization();

var moduleApi = app.MapGroup("");
foreach (var module in app.Services.GetServices<IAtlasModule>())
    module.Configure(new ModuleEndpoints(moduleApi));

app.MapGet("/", () => Results.Ok(new { service = "Atlas.Control.Host", style = "modular-monolith" }));

app.Run();

public partial class Program; // para Mvc.Testing
