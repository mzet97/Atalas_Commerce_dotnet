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

    public Task<bool> ReleaseAsync(string reservationId, string reason, CancellationToken ct)
    {
        // release via RPC específico chega com o fluxo de cancelamento da SPEC-006
        return Task.FromResult(true);
    }
}
