using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Atlas.Contracts.Inventory.V1;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Inventory.Api.Tests;

/// <summary>
/// UC003-T01 pelo CAMINHO REAL: 10 chamadas gRPC concorrentes de ReserveStock
/// (canal HTTP/2 in-memory sobre o host) contra o MongoDB replica set —
/// exatamente 1Granted, oversell=0, e Confirm/Release pelos RPCs do contrato.
/// </summary>
public sealed class InventoryOwnerGrpcTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InventoryOwnerGrpcTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
            b.UseSetting("Mongo:Database", $"atlas_invgrpc_{Guid.NewGuid():N}"));
    }

    private async Task<(Guid Wh, string DbName)> SeedAsync(int onHand)
    {
        var dbName = _factory.Services.GetRequiredService<IConfiguration>()["Mongo:Database"]!;
        var db = new MongoClient("mongodb://127.0.0.1:27017/?directConnection=true").GetDatabase(dbName);
        var wh = Guid.NewGuid();
        var key = $"{wh:N}:sku-race";
        await db.GetCollection<MongoDB.Bson.BsonDocument>("inventory.positions").InsertOneAsync(new MongoDB.Bson.BsonDocument
        {
            ["_id"] = key,
            ["OnHand"] = onHand,
            ["Reserved"] = 0,
            ["SafetyStock"] = 0,
            ["Version"] = 1,
        });
        return (wh, dbName);
    }

    [Fact]
    public async Task Última_unidade_via_grpc_concorrente_permite_exatamente_uma()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost",
            new GrpcChannelOptions { HttpHandler = _factory.Server.CreateHandler() });
        var client = new InventoryOwnerService.InventoryOwnerServiceClient(channel);

        var (wh, dbName) = await SeedAsync(onHand: 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            client.ReserveStockAsync(new ReserveStockRequest
            {
                RequestId = $"race-{i}",
                TtlSeconds = 900,
                Lines = { new ReserveLine { WarehouseId = wh.ToString(), SkuId = "sku-race", Quantity = 1 } },
            }).ResponseAsync));

        Assert.Equal(1, results.Count(r => r.Granted));
        Assert.Equal(9, results.Count(r => !r.Granted && r.Denied.Single().Cause == "OUT_OF_STOCK"));

        // estado no Mongo: reserved=1, version=2
        var db = new MongoClient("mongodb://127.0.0.1:27017/?directConnection=true").GetDatabase(dbName);
        var doc = await db.GetCollection<MongoDB.Bson.BsonDocument>("inventory.positions")
            .Find(new MongoDB.Bson.BsonDocument("_id", $"{wh:N}:sku-race")).SingleAsync();
        Assert.Equal(1, doc["OnHand"].ToInt32());
        Assert.Equal(1, doc["Reserved"].ToInt32());
        Assert.Equal(2, doc["Version"].ToInt32());

        // Confirm pelo RPC do contrato
        var winner = results.First(r => r.Granted);
        var confirm = await client.ConfirmReservationAsync(new ConfirmReservationRequest
        {
            RequestId = "confirm-1",
            ReservationId = winner.ReservationId,
            OrderId = "order-race-1",
        });
        Assert.True(confirm.Accepted);
    }

    [Fact]
    public async Task Receive_e_adjust_via_grpc_com_guardas_de_domínio()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost",
            new GrpcChannelOptions { HttpHandler = _factory.Server.CreateHandler() });
        var client = new InventoryOwnerService.InventoryOwnerServiceClient(channel);
        var wh = Guid.NewGuid();

        // receive em posição inexistente → NotFound (seed obrigatória do warehouse/sku)
        var nf = await Assert.ThrowsAsync<RpcException>(() => client.ReceiveStockAsync(new ReceiveStockRequest
        {
            RequestId = "rcv-0", WarehouseId = wh.ToString(), SkuId = "sku-x", Quantity = 5, ActorSub = "op",
        }).ResponseAsync);
        Assert.Equal(StatusCode.NotFound, nf.StatusCode);

        await SeedPositionAsync(wh, "sku-x");
        var receive = await client.ReceiveStockAsync(new ReceiveStockRequest
        {
            RequestId = "rcv-1", WarehouseId = wh.ToString(), SkuId = "sku-x", Quantity = 7, ActorSub = "op-1",
        });
        Assert.True(receive.Accepted);

        // SPEC-004: adjust é operação de risco — SEM token → PermissionDenied (owner revalida)
        var noToken = await Assert.ThrowsAsync<RpcException>(() => client.AdjustStockAsync(new AdjustStockRequest
        {
            RequestId = "adj-0", WarehouseId = wh.ToString(), SkuId = "sku-x", Delta = -1, Reason = "sem token", ActorSub = "op-1",
        }).ResponseAsync);
        Assert.Equal(StatusCode.PermissionDenied, noToken.StatusCode);

        var token = Atlas.Inventory.Application.StockApprovalToken.Issue(
            new Atlas.Inventory.Application.StockApprovalToken.Payload(
                Sub: "approver-1", Action: "inventory.adjust",
                Subject: Atlas.Inventory.Application.StockApprovalToken.SubjectFor(wh.ToString(), "sku-x"),
                ExpUnixSeconds: DateTimeOffset.UtcNow.AddSeconds(120).ToUnixTimeSeconds(),
                Jti: Guid.NewGuid().ToString()),
            secret: "atlas-dev-approval-secret");

        var adjust = await client.AdjustStockAsync(new AdjustStockRequest
        {
            RequestId = "adj-1", WarehouseId = wh.ToString(), SkuId = "sku-x", Delta = -3, Reason = "avaria",
            ActorSub = "op-1", ApprovalToken = token,
        });
        Assert.True(adjust.Accepted);

        // token com subject DIVERGENTE (outra sku) → PermissionDenied
        var wrongSubject = Atlas.Inventory.Application.StockApprovalToken.Issue(
            new Atlas.Inventory.Application.StockApprovalToken.Payload(
                Sub: "approver-1", Action: "inventory.adjust",
                Subject: Atlas.Inventory.Application.StockApprovalToken.SubjectFor(wh.ToString(), "sku-OUTRA"),
                ExpUnixSeconds: DateTimeOffset.UtcNow.AddSeconds(120).ToUnixTimeSeconds(),
                Jti: Guid.NewGuid().ToString()),
            secret: "atlas-dev-approval-secret");
        var mismatch = await Assert.ThrowsAsync<RpcException>(() => client.AdjustStockAsync(new AdjustStockRequest
        {
            RequestId = "adj-x", WarehouseId = wh.ToString(), SkuId = "sku-x", Delta = -1, Reason = "subject errado",
            ActorSub = "op-1", ApprovalToken = wrongSubject,
        }).ResponseAsync);
        Assert.Equal(StatusCode.PermissionDenied, mismatch.StatusCode);

        // ajuste negativo além do disponível → FailedPrecondition (domínio)
        var over = await Assert.ThrowsAsync<RpcException>(() => client.AdjustStockAsync(new AdjustStockRequest
        {
            RequestId = "adj-2", WarehouseId = wh.ToString(), SkuId = "sku-x", Delta = -10, Reason = "inventário",
            ActorSub = "op-1", ApprovalToken = token,
        }).ResponseAsync);
        Assert.Equal(StatusCode.FailedPrecondition, over.StatusCode);

        // SPEC-004: head version no owner (lag CQRS) — Version avançou com os ajustes
        var head = await client.GetStockVersionAsync(new GetStockVersionRequest
        { WarehouseId = wh.ToString(), SkuId = "sku-x" });
        Assert.True(head.Found);
        Assert.True(head.Version >= 2); // seed(1) + adjust
    }

    private async Task SeedPositionAsync(Guid wh, string sku)
    {
        var dbName = _factory.Services.GetRequiredService<IConfiguration>()["Mongo:Database"]!;
        var db = new MongoClient("mongodb://127.0.0.1:27017/?directConnection=true").GetDatabase(dbName);
        await db.GetCollection<MongoDB.Bson.BsonDocument>("inventory.positions").InsertOneAsync(new MongoDB.Bson.BsonDocument
        {
            ["_id"] = $"{wh:N}:{sku}",
            ["OnHand"] = 5, ["Reserved"] = 0, ["SafetyStock"] = 0, ["Version"] = 1,
        });
    }
}
