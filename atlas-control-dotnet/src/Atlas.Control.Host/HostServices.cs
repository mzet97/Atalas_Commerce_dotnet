using System.Security.Claims;
using System.Text.Json;
using Atlas.Control.Modules.Approvals;
using Atlas.Control.Modules.InventoryOperations;
using Atlas.Control.Modules.Kernel;

namespace Atlas.Control.Host;

/// <summary>Sink de auditoria com HASH ENCADEADO (SPEC-004/REQ-ADM-003) — todo 403 também entra na cadeia.</summary>
public sealed class ControlAuditSink(AuditChainStore store)
{
    public Task AppendAsync(string actorSub, string action, string decision, string? detail, CancellationToken ct) =>
        store.AppendAsync(actorSub, action, decision, detail, Guid.NewGuid().ToString(), ct);
}

/// <summary>Despacho real ao Inventory owner por gRPC (SPEC-004: adjust four-eyes + head version).</summary>
public sealed class GrpcInventoryOwnerDispatcher(string ownerUrl) : IInventoryStockOwner
{
    public async Task<(bool Ok, long PositionVersion, string Error)> AdjustWithApprovalAsync(
        string requestId, string warehouseId, string skuId, int delta, string reason,
        string actorSub, string approvalToken, string correlationId, CancellationToken ct)
    {
        try
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(ownerUrl);
            var client = new Atlas.Contracts.Inventory.V1.InventoryOwnerService.InventoryOwnerServiceClient(channel);
            var response = await client.AdjustStockAsync(new Atlas.Contracts.Inventory.V1.AdjustStockRequest
            {
                RequestId = requestId,
                ActorSub = actorSub,
                ApprovalToken = approvalToken,
                WarehouseId = warehouseId,
                SkuId = skuId,
                Delta = delta,
                Reason = reason,
                CorrelationId = correlationId,
            }, cancellationToken: ct);
            return (response.Accepted, response.PositionVersion, response.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, 0, $"owner recusou ({ex.StatusCode}): {ex.Status.Detail}");
        }
    }

    public async Task<(bool Found, long Version)> GetHeadVersionAsync(string warehouseId, string skuId, CancellationToken ct)
    {
        try
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(ownerUrl);
            var client = new Atlas.Contracts.Inventory.V1.InventoryOwnerService.InventoryOwnerServiceClient(channel);
            var response = await client.GetStockVersionAsync(new Atlas.Contracts.Inventory.V1.GetStockVersionRequest
            {
                WarehouseId = warehouseId,
                SkuId = skuId,
            }, cancellationToken: ct);
            return (response.Found, response.Version);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            Console.Error.WriteLine($"[inventory-owner] head version indisponível: {ex.StatusCode}");
            return (false, 0);
        }
    }
}

/// <summary>Expande realm_access.roles (formato Keycloak) em claims de role.</summary>
public sealed class KeycloakRolesClaimsTransformation : Microsoft.AspNetCore.Authentication.IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true }) return Task.FromResult(principal);
        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (string.IsNullOrEmpty(realmAccess)) return Task.FromResult(principal);
        try
        {
            var roles = JsonDocument.Parse(realmAccess).RootElement.GetProperty("roles").EnumerateArray()
                .Select(r => r.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (roles.Count == 0) return Task.FromResult(principal);
            var identity = (ClaimsIdentity)principal.Identity;
            foreach (var role in roles) identity.AddClaim(new Claim(ClaimTypes.Role, role!));
        }
        catch (JsonException) { /* token sem realm_access legível — segue sem roles */ }
        return Task.FromResult(principal);
    }
}

/// <summary>Despacho real ao Catalog owner por gRPC (contract SDK; URL por config).</summary>
public sealed class GrpcCatalogOwnerDispatcher(string ownerUrl) : ApprovalsModule.IOwner
{
    public async Task<ApprovalsModule.OwnerPublishResponse> PublishProductAsync(
        ApprovalsModule.OwnerPublishRequest request, CancellationToken ct)
    {
        try
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(ownerUrl);
            var client = new Atlas.Contracts.Catalog.V1.CatalogOwnerService.CatalogOwnerServiceClient(channel);
            var response = await client.PublishProductAsync(new Atlas.Contracts.Catalog.V1.PublishProductRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                ApprovalToken = request.ApprovalToken,
                ProductId = request.ProductId,
                ExpectedVersion = request.ExpectedVersion,
                MarketId = request.MarketId,
            }, cancellationToken: ct);
            return new ApprovalsModule.OwnerPublishResponse(response.Accepted, response.EventId, response.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            // Falha do owner (ex.: four-eyes FailedPrecondition) vira FAILED auditável, não crash do Control
            return new ApprovalsModule.OwnerPublishResponse(false, null, ex.Status.Detail);
        }
    }
}
