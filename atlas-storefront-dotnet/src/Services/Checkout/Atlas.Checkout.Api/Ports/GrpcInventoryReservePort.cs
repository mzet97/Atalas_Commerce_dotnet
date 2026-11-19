using Atlas.Contracts.Inventory.V1;
using Atlas.Checkout.Application;

namespace Atlas.Checkout.Api.Ports;

/// <summary>Port real de reserva via gRPC ao Inventory owner (h2c local; mTLS na SPEC-004+).</summary>
public sealed class GrpcInventoryReservePort(string ownerUrl) : IInventoryReservePort
{
    public async Task<string?> ReserveAsync(string checkoutId, IReadOnlyList<(string SkuId, int Quantity)> lines, CancellationToken ct)
    {
        try
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(ownerUrl);
            var client = new InventoryOwnerService.InventoryOwnerServiceClient(channel);
            var request = new ReserveStockRequest { RequestId = checkoutId, TtlSeconds = 900 };
            request.Lines.AddRange(lines.Select(l => new ReserveLine { WarehouseId = Guid.Empty.ToString(), SkuId = l.SkuId, Quantity = l.Quantity }));
            var response = await client.ReserveStockAsync(request, cancellationToken: ct);
            return response.Granted ? response.ReservationId : null;
        }
        catch (global::Grpc.Core.RpcException)
        {
            return null; // conservador: indisponibilidade ≠ reserva
        }
    }

    public async Task<bool> ReleaseAsync(string reservationId, string reason, CancellationToken ct)
    {
        try
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(ownerUrl);
            var client = new InventoryOwnerService.InventoryOwnerServiceClient(channel);
            var response = await client.ReleaseReservationAsync(
                new ReleaseReservationRequest { ReservationId = reservationId, RequestId = $"{reason}:{reservationId}" },
                cancellationToken: ct);
            return response.Accepted;
        }
        catch (global::Grpc.Core.RpcException)
        {
            return false; // reconciliador/reaper tentam de novo — nunca presumir
        }
    }

    public async Task<bool> CommitAsync(string reservationId, string orderId, CancellationToken ct)
    {
        try
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(ownerUrl);
            var client = new InventoryOwnerService.InventoryOwnerServiceClient(channel);
            var response = await client.ConfirmReservationAsync(
                new ConfirmReservationRequest { ReservationId = reservationId, OrderId = orderId },
                cancellationToken: ct);
            return response.Accepted;
        }
        catch (global::Grpc.Core.RpcException)
        {
            return false; // reconciliador tenta de novo — nunca presumir venda
        }
    }
}
