using Atlas.Contracts.Inventory.V1;
using Atlas.Inventory.Application;
using Atlas.Inventory.Domain;
using Grpc.Core;
using MongoInventoryStore = Atlas.Inventory.Infrastructure.MongoInventoryStore;
using InventoryConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Atlas.Inventory.Api.Grpc;

/// <summary>
/// Owner gRPC do Inventory (M2): comandos idempotentes revalidados no domínio/store
/// (CAS condicional no Mongo — INV-STOCK-001/003). Aprovações acima de limite entram
/// com a fatia four-eyes do Control (approval_token no proto).
/// </summary>
public sealed class InventoryOwnerGrpcService(MongoInventoryStore store, InventoryConfiguration config)
    : InventoryOwnerService.InventoryOwnerServiceBase
{
    private string TokenSecret => config["Approval:TokenSecret"] ?? "atlas-dev-approval-secret";

    public override async Task<ReceiveStockResponse> ReceiveStock(ReceiveStockRequest request, ServerCallContext context)
    {
        var (key, position) = await EnsurePositionAsync(request.WarehouseId, request.SkuId, context);
        position.Receive(request.Quantity);
        await store.SavePositionAsync(key, position, context.CancellationToken);
        return await LedgerResponseAsync(key, request.RequestId, context.CancellationToken);
    }

    public override async Task<AdjustStockResponse> AdjustStock(AdjustStockRequest request, ServerCallContext context)
    {
        var key = ParseKey(request.WarehouseId, request.SkuId);
        // INV-SEC-001/INV-ADMIN-001: ajuste é operação de risco — o owner REVALIDA o
        // approval_token four-eyes (assinatura HMAC, exp, action, subject); UI/Control
        // não são perímetro. Token v2 com jti (uso único enforcement no Control).
        var subject = StockApprovalToken.SubjectFor(request.WarehouseId, request.SkuId);
        var approval = StockApprovalToken.TryValidate(
            request.ApprovalToken, TokenSecret, StockApprovalToken.AdjustAction, subject,
            DateTimeOffset.UtcNow, out var failure);
        if (approval is null)
            throw new RpcException(new Status(StatusCode.PermissionDenied, $"approval_token rejeitado: {failure}"));
        var outcome = await store.AdjustStockInTransactionAsync(
            key, request.Delta, request.Reason ?? "adjust", request.CorrelationId ?? request.RequestId,
            request.ActorSub ?? approval.Sub, context.CancellationToken);
        if (outcome is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"posição inexistente: {key}"));
        if (outcome.Value.Ok is false)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "ajuste inviabiliza o saldo disponível"));
        return new AdjustStockResponse { Accepted = true, MovementId = request.RequestId, PositionVersion = outcome.Value.NewVersion };
    }

    public override async Task<ReserveStockResponse> ReserveStock(ReserveStockRequest request, ServerCallContext context)
    {
        var handler = new ReserveStockHandler(store);
        var result = await handler.HandleAsync(new ReserveStockCommand(
            IdempotencyKey: request.RequestId,
            Lines: [.. request.Lines.Select(l => new ReserveLineCommand(
                Guid.Parse(l.WarehouseId), l.SkuId, l.Quantity))],
            Ttl: TimeSpan.FromSeconds(Math.Max(request.TtlSeconds, 60)),
            CorrelationId: request.RequestId,
            ActorSub: "inventory-owner-grpc"), context.CancellationToken);

        var response = new ReserveStockResponse { Granted = result.Status == ReserveStockStatus.Granted };
        if (result.Reservation is not null) response.ReservationId = result.Reservation.Id.ToString();
        response.Denied.AddRange(result.DeniedSkus.Select(s => new DeniedLine { SkuId = s, Cause = "OUT_OF_STOCK" }));
        return response;
    }

    public override async Task<ConfirmReservationResponse> ConfirmReservation(ConfirmReservationRequest request, ServerCallContext context)
    {
        var ok = await store.ConfirmReservationAsync(Guid.Parse(request.ReservationId), request.OrderId, context.CancellationToken);
        return new ConfirmReservationResponse { Accepted = ok, State = ok ? "Confirmed" : "Invalid", Reason = ok ? "" : "reserva não está Active" };
    }

    public override async Task<ReleaseReservationResponse> ReleaseReservation(ReleaseReservationRequest request, ServerCallContext context)
    {
        var ok = await store.ReleaseReservationAsync(
            Guid.Parse(request.ReservationId), reason: "release-via-grpc", correlationId: request.RequestId, actorSub: "grpc", context.CancellationToken);
        return new ReleaseReservationResponse { Accepted = ok, State = ok ? "Released" : "Invalid", Reason = ok ? "" : "reserva inexistente ou terminal" };
    }

    /// <summary>Head version da posição no OWNER (SPEC-004: lag CQRS real do Control).</summary>
    public override async Task<GetStockVersionResponse> GetStockVersion(
        GetStockVersionRequest request, ServerCallContext context)
    {
        var key = ParseKey(request.WarehouseId, request.SkuId);
        var position = await store.LoadPositionAsync(key, context.CancellationToken);
        return position is null
            ? new GetStockVersionResponse { Found = false, Version = 0 }
            : new GetStockVersionResponse { Found = true, Version = position.Version };
    }

    private static StockKey ParseKey(string warehouseId, string skuId) =>
        StockKey.Create(Guid.Parse(warehouseId), skuId);

    private async Task<(StockKey Key, StockPosition Position)> EnsurePositionAsync(
        string warehouseId, string skuId, ServerCallContext context)
    {
        if (!Guid.TryParse(warehouseId, out var wh))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "warehouse_id inválido"));
        var key = StockKey.Create(wh, skuId);
        var position = await store.LoadPositionAsync(key, context.CancellationToken);
        if (position is null) throw new RpcException(new Status(StatusCode.NotFound, $"posição inexistente: {key}"));
        return (key, position);
    }

    private async Task<ReceiveStockResponse> LedgerResponseAsync(StockKey key, string requestId, CancellationToken ct)
    {
        var position = await store.LoadPositionAsync(key, ct);
        return new ReceiveStockResponse { Accepted = true, MovementId = requestId, PositionVersion = position?.Version ?? 0 };
    }
}
