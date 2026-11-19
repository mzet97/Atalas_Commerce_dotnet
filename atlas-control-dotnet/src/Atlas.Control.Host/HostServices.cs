using System.Security.Claims;
using System.Text.Json;
using Atlas.Control.Modules.Approvals;
using CatalogOps = Atlas.Control.Modules.CatalogOperations;
using OffersOps = Atlas.Control.Modules.OffersOperations;
using OrdersOps = Atlas.Control.Modules.Orders;
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
    public async Task<(bool Ok, long PositionVersion, string Error)> ReceiveWithApprovalAsync(
        string requestId, string warehouseId, string skuId, int quantity, string reason,
        string actorSub, string approvalToken, string correlationId, CancellationToken ct)
    {
        try
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(ownerUrl);
            var client = new Atlas.Contracts.Inventory.V1.InventoryOwnerService.InventoryOwnerServiceClient(channel);
            var response = await client.ReceiveStockAsync(new Atlas.Contracts.Inventory.V1.ReceiveStockRequest
            {
                RequestId = requestId,
                ActorSub = actorSub,
                ApprovalToken = approvalToken,
                WarehouseId = warehouseId,
                SkuId = skuId,
                Quantity = quantity,
                Reason = reason,
                CorrelationId = correlationId,
            }, cancellationToken: ct);
            return (response.Accepted, response.PositionVersion, "");
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, 0, $"owner recusou ({ex.StatusCode}): {ex.Status.Detail}");
        }
    }

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

/// <summary>Adapter gRPC ao Catalog owner (canal por config; erros viram resultado, nunca throw).</summary>
public sealed class GrpcCatalogOwnerCatalog(string ownerUrl) : CatalogOps.ICatalogOwnerCatalog
{
    private Atlas.Contracts.Catalog.V1.CatalogOwnerService.CatalogOwnerServiceClient NewClient()
    {
        var channel = Grpc.Net.Client.GrpcChannel.ForAddress(ownerUrl);
        return new Atlas.Contracts.Catalog.V1.CatalogOwnerService.CatalogOwnerServiceClient(channel);
    }

    public async Task<(IReadOnlyList<CatalogOps.CatalogProductRow> Items, long Total)> ListAsync(
        string? status, string? market, string? search, int page, int pageSize, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().ListDraftsAsync(new Atlas.Contracts.Catalog.V1.ListDraftsRequest
            {
                Status = status ?? "", MarketId = market ?? "", Search = search ?? "",
                Page = page, PageSize = pageSize,
            }, cancellationToken: ct);
            return ([.. res.Items.Select(i => new CatalogOps.CatalogProductRow(
                i.ProductId, i.Status, i.AggregateVersion, i.MarketId,
                [.. i.Titles.Select(t => new CatalogOps.CatalogTitleDto(t.Locale, t.Text))], i.SkuCount, i.CreatedBy,
                [.. i.Images.Select(m => new CatalogOps.CatalogImageDto(m.Key, m.ContentType, m.SizeBytes))]))], res.Total);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            Console.Error.WriteLine($"[catalog-owner] lista indisponível ({ex.StatusCode})");
            return ([], 0);
        }
    }

    public async Task<CatalogOps.CatalogProductDetail?> GetAsync(string productId, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().GetProductAsync(
                new Atlas.Contracts.Catalog.V1.GetProductRequest { ProductId = productId }, cancellationToken: ct);
            return new CatalogOps.CatalogProductDetail(res.ProductId, res.Status, res.AggregateVersion,
                [.. res.Titles.Select(t => new CatalogOps.CatalogTitleDto(t.Locale, t.Text))],
                [.. res.Skus.Select(s => new CatalogOps.CatalogSkuDto(s.SkuId, s.SkuCode))],
                [.. res.Images.Select(m => new CatalogOps.CatalogImageDto(m.Key, m.ContentType, m.SizeBytes))]);
        }
        catch (global::Grpc.Core.RpcException ex) when (ex.StatusCode == global::Grpc.Core.StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<(bool Accepted, string ProductId, long Version, string? Reason)> UpsertAsync(
        string requestId, string actorSub, string marketId,
        IReadOnlyList<CatalogOps.CatalogTitleDto> titles, IReadOnlyList<string> skuCodes, CancellationToken ct)
    {
        try
        {
            var req = new Atlas.Contracts.Catalog.V1.UpsertDraftRequest
            {
                RequestId = requestId, ActorSub = actorSub, CategoryId = marketId, CorrelationId = requestId,
            };
            req.Titles.AddRange(titles.Select(t => new Atlas.Contracts.Catalog.V1.LocalizedText { Locale = t.Locale, Text = t.Title }));
            req.Skus.AddRange(skuCodes.Select(c => new Atlas.Contracts.Catalog.V1.SkuDraft { SkuCode = c }));
            var res = await NewClient().UpsertDraftAsync(req, cancellationToken: ct);
            return (res.Accepted, res.ProductId, res.AggregateVersion, res.Reason is "" ? null : res.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, "", 0, ex.Status.Detail);
        }
    }

    public async Task<(bool Accepted, long Version, string? Reason)> UpdateAsync(
        string productId, long expectedVersion, string actorSub,
        IReadOnlyList<CatalogOps.CatalogTitleDto> titles, IReadOnlyDictionary<string, string> attributes, CancellationToken ct)
    {
        try
        {
            var req = new Atlas.Contracts.Catalog.V1.UpdateDraftRequest
            {
                RequestId = $"upd-{Guid.NewGuid():N}", ActorSub = actorSub,
                ProductId = productId, ExpectedVersion = expectedVersion,
            };
            req.Titles.AddRange(titles.Select(t => new Atlas.Contracts.Catalog.V1.LocalizedText { Locale = t.Locale, Text = t.Title }));
            foreach (var kv in attributes) req.Attributes[kv.Key] = kv.Value;
            var res = await NewClient().UpdateDraftAsync(req, cancellationToken: ct);
            return (res.Accepted, res.AggregateVersion, res.Reason is "" ? null : res.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, 0, ex.Status.Detail);
        }
    }

    public async Task<(bool Accepted, string? Reason)> DeleteAsync(string productId, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().DeleteDraftAsync(
                new Atlas.Contracts.Catalog.V1.DeleteDraftRequest { ProductId = productId }, cancellationToken: ct);
            return (res.Accepted, res.Reason is "" ? null : res.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, ex.Status.Detail);
        }
    }

    public async Task<(bool Accepted, long Version, string? Reason)> SubmitAsync(
        string requestId, string actorSub, string productId, long expectedVersion, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().SubmitForApprovalAsync(
                new Atlas.Contracts.Catalog.V1.SubmitForApprovalRequest
                { RequestId = requestId, ActorSub = actorSub, ProductId = productId, ExpectedVersion = expectedVersion },
                cancellationToken: ct);
            return (res.Accepted, res.AggregateVersion, res.Reason is "" ? null : res.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, 0, ex.Status.Detail);
        }
    }

    public async Task<(bool Accepted, long Version, string? Reason, string? EventId)> UnpublishAsync(
        string requestId, string actorSub, string productId, long expectedVersion,
        string approvalToken, string reason, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().UnpublishProductAsync(
                new Atlas.Contracts.Catalog.V1.UnpublishProductRequest
                {
                    RequestId = requestId, ApprovalToken = approvalToken, ProductId = productId,
                    ExpectedVersion = expectedVersion, Reason = reason,
                }, cancellationToken: ct);
            return (res.Accepted, res.AggregateVersion,
                res.Reason is "" ? null : res.Reason,
                res.EventId is "" ? null : res.EventId);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, 0, ex.Status.Detail, null);
        }
    }

    public async Task<(bool Found, long Version)> GetHeadVersionAsync(string productId, CancellationToken ct)
    {
        var detail = await GetAsync(productId, ct);
        return detail is null ? (false, 0) : (true, detail.Version);
    }

    public async Task<(bool Accepted, string? UploadUrl, string? ObjectKey, string? ExpiresAtUtc, string? Reason)> RequestImageUploadAsync(
        string productId, string actorSub, string contentType, long contentLength, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().RequestImageUploadAsync(
                new Atlas.Contracts.Catalog.V1.RequestImageUploadRequest
                {
                    RequestId = $"img-{Guid.NewGuid():N}", ActorSub = actorSub, ProductId = productId,
                    ContentType = contentType, ContentLength = contentLength,
                }, cancellationToken: ct);
            return (res.Accepted,
                res.UploadUrl is "" ? null : res.UploadUrl,
                res.ObjectKey is "" ? null : res.ObjectKey,
                res.ExpiresAtUtc is "" ? null : res.ExpiresAtUtc,
                res.Reason is "" ? null : res.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, null, null, null, ex.Status.Detail);
        }
    }

    public async Task<(bool Accepted, long Version, string? Reason)> AttachImageAsync(
        string productId, long expectedVersion, string actorSub, string objectKey, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().AttachImageAsync(
                new Atlas.Contracts.Catalog.V1.AttachImageRequest
                {
                    RequestId = $"img-{Guid.NewGuid():N}", ActorSub = actorSub, ProductId = productId,
                    ExpectedVersion = expectedVersion, ObjectKey = objectKey,
                }, cancellationToken: ct);
            return (res.Accepted, res.AggregateVersion, res.Reason is "" ? null : res.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, 0, ex.Status.Detail);
        }
    }

    public async Task<(bool Accepted, long Version, string? Reason)> DetachImageAsync(
        string productId, long expectedVersion, string actorSub, string objectKey, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().DetachImageAsync(
                new Atlas.Contracts.Catalog.V1.DetachImageRequest
                {
                    RequestId = $"img-{Guid.NewGuid():N}", ActorSub = actorSub, ProductId = productId,
                    ExpectedVersion = expectedVersion, ObjectKey = objectKey,
                }, cancellationToken: ct);
            return (res.Accepted, res.AggregateVersion, res.Reason is "" ? null : res.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, 0, ex.Status.Detail);
        }
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

/// <summary>Adapter gRPC ao Offers owner (erros viram resultado, nunca throw).</summary>
public sealed class GrpcOffersOwnerCatalog(string ownerUrl) : OffersOps.IOffersOwnerCatalog
{
    private Atlas.Contracts.Offers.V1.OffersOwnerService.OffersOwnerServiceClient NewClient()
    {
        var channel = Grpc.Net.Client.GrpcChannel.ForAddress(ownerUrl);
        return new Atlas.Contracts.Offers.V1.OffersOwnerService.OffersOwnerServiceClient(channel);
    }

    public async Task<(IReadOnlyList<OffersOps.OfferRowDto> Items, long Total)> ListAsync(
        string? marketId, string? skuId, int page, int pageSize, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().ListActiveOffersAsync(new Atlas.Contracts.Offers.V1.ListActiveOffersRequest
            {
                MarketId = marketId ?? "", SkuId = skuId ?? "",
                Page = page, PageSize = pageSize,
            }, cancellationToken: ct);
            return ([.. res.Items.Select(i => new OffersOps.OfferRowDto(
                i.OfferId, i.SkuId, i.SellerId, i.MarketId, i.Channel,
                i.Currency, i.PriceMinor, i.ListPriceMinor, i.AggregateVersion, i.ValidTo))], res.Total);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            Console.Error.WriteLine($"[offers-owner] lista indisponível ({ex.StatusCode})");
            return ([], 0);
        }
    }

    public async Task<(bool Accepted, string OfferId, long Version, string? Reason)> PublishAsync(
        string requestId, string actorSub, string skuId, string sellerId, string marketId,
        string channel, string currency, long priceMinor, long listPriceMinor, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().PublishOfferAsync(new Atlas.Contracts.Offers.V1.PublishOfferRequest
            {
                RequestId = requestId, ActorSub = actorSub, SkuId = skuId, SellerId = sellerId,
                MarketId = marketId, Channel = channel, Currency = currency,
                PriceMinor = priceMinor, ListPriceMinor = listPriceMinor,
            }, cancellationToken: ct);
            return (res.Accepted, res.OfferId, res.AggregateVersion, res.Reason is "" ? null : res.Reason);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, "", 0, ex.Status.Detail);
        }
    }

    public async Task<(bool Accepted, long Version, string? Reason, string? EventId)> UpdatePriceAsync(
        string requestId, string actorSub, string offerId, long expectedVersion, long priceMinor, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().UpdateOfferPriceAsync(new Atlas.Contracts.Offers.V1.UpdateOfferPriceRequest
            {
                RequestId = requestId, ActorSub = actorSub, OfferId = offerId,
                ExpectedVersion = expectedVersion, PriceMinor = priceMinor, CorrelationId = requestId,
            }, cancellationToken: ct);
            return (res.Accepted, res.AggregateVersion,
                res.Reason is "" ? null : res.Reason,
                res.EventId is "" ? null : res.EventId);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, 0, ex.Status.Detail, null);
        }
    }

    public async Task<(bool Accepted, long Version, string? Reason, string? EventId)> DeactivateAsync(
        string requestId, string actorSub, string offerId, long expectedVersion, CancellationToken ct)
    {
        try
        {
            var res = await NewClient().DeactivateOfferAsync(new Atlas.Contracts.Offers.V1.DeactivateOfferRequest
            {
                RequestId = requestId, ActorSub = actorSub, OfferId = offerId,
                ExpectedVersion = expectedVersion, CorrelationId = requestId,
            }, cancellationToken: ct);
            return (res.Accepted, res.AggregateVersion,
                res.Reason is "" ? null : res.Reason,
                res.EventId is "" ? null : res.EventId);
        }
        catch (global::Grpc.Core.RpcException ex)
        {
            return (false, 0, ex.Status.Detail, null);
        }
    }
}

/// <summary>Adapter HTTP ao Fulfillment owner (transições revalidadas no owner).</summary>
public sealed class HttpFulfillmentOrders(HttpClient http) : OrdersOps.IFulfillmentOrders
{
    private sealed record FulfillmentDetail(bool Found, string State, string Tracking, string Steps, string? Reason);

    public async Task<(bool Found, string State, string Tracking, string Steps, string? Reason)> GetAsync(string orderId, CancellationToken ct)
    {
        using var res = await http.GetAsync($"/admin/orders/{orderId}", ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return (false, "", "", "", "inexistente");
        res.EnsureSuccessStatusCode();
        var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var j = doc.RootElement;
        return (true, j.GetProperty("state").GetString()!, j.TryGetProperty("trackingCode", out var t) ? t.GetString()! : "",
            j.TryGetProperty("steps", out var s) ? s.ToString() : "", null);
    }

    public async Task<(bool Accepted, string State, string? Reason)> StartAsync(string orderId, CancellationToken ct)
    {
        try
        {
            using var res = await http.PostAsync($"/admin/orders/{orderId}/start", null, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var errDoc = System.Text.Json.JsonDocument.Parse(body);
                return (false, "", errDoc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : res.StatusCode.ToString());
            }
            var okDoc = System.Text.Json.JsonDocument.Parse(body);
            return (true, okDoc.RootElement.GetProperty("state").GetString()!, null);
        }
        catch (global::System.Net.Http.HttpRequestException ex)
        {
            return (false, "", ex.Message);
        }
    }

    public async Task<(bool Accepted, string State, string? Reason)> AdvanceAsync(string orderId, string targetState, CancellationToken ct)
    {
        using var res = await http.PostAsync($"/admin/orders/{orderId}/advance?to={Uri.EscapeDataString(targetState)}", null, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.NotFound)
            return (false, "", System.Text.Json.JsonDocument.Parse(body).RootElement.TryGetProperty("reason", out var r) ? r.GetString() : res.StatusCode.ToString());
        res.EnsureSuccessStatusCode();
        var j = System.Text.Json.JsonDocument.Parse(body).RootElement;
        return (true, j.GetProperty("state").GetString()!, null);
    }

    public async Task<(bool Accepted, string State, string? Reason)> CancelAsync(string orderId, CancellationToken ct)
    {
        using var res = await http.PostAsync($"/admin/orders/{orderId}/cancel", null, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.NotFound)
            return (false, "", System.Text.Json.JsonDocument.Parse(body).RootElement.TryGetProperty("reason", out var r) ? r.GetString() : res.StatusCode.ToString());
        res.EnsureSuccessStatusCode();
        var j = System.Text.Json.JsonDocument.Parse(body).RootElement;
        return (true, j.GetProperty("state").GetString()!, null);
    }
}

/// <summary>Adapter HTTP ao Payments owner (lookup read-only de intent por pedido).</summary>
public sealed class HttpPaymentsIntents(HttpClient http) : Atlas.Control.Modules.Payments.IPaymentsIntents
{
    public async Task<object?> GetByOrderAsync(string orderId, CancellationToken ct)
    {
        using var res = await http.GetAsync($"/intents/by-order/{orderId}", ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement.Clone();
    }
}
