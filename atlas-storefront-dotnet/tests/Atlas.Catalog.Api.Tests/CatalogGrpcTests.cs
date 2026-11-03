using Atlas.Catalog.Application;
using Atlas.Contracts.Catalog.V1;
using global::Grpc.Core;
using global::Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Catalog.Api.Tests;

/// <summary>
/// TEST-002-002 via gRPC REAL (canal in-memory HTTP/2 sobre WebApplicationFactory):
/// fluxo UC-005 completo pelos caminhos do contrato — UpsertDraft → SubmitForApproval →
/// PublishProduct (com approval_token) → GetProduct. Mongo isolado por execução.
/// </summary>
public sealed class CatalogGrpcTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CatalogGrpcTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
            b.UseSetting("Mongo:Database", $"atlas_grpc_{Guid.NewGuid():N}"));
    }

    [Fact]
    public async Task Fluxo_uc005_completo_por_grpc()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost",
            new GrpcChannelOptions { HttpHandler = _factory.Server.CreateHandler() });
        var client = new CatalogOwnerService.CatalogOwnerServiceClient(channel);

        // 1) UpsertDraft
        var upsert = await client.UpsertDraftAsync(new UpsertDraftRequest
        {
            RequestId = "grpc-req-1",
            ActorSub = "catalog-editor-1",
            CategoryId = "BR",
            Titles = { new LocalizedText { Locale = "pt-BR", Text = "Produto gRPC" } },
            Skus = { new SkuDraft { SkuCode = "GRPC-1" } },
        });
        Assert.True(upsert.Accepted);
        Assert.Equal(1, upsert.AggregateVersion); // versão persistida no insert

        // 2) publish sem submit → token VÁLIDO negado pelo DOMÍNIO (owner revalida estado)
        var tokenDraft = ApprovalToken.Issue(
            new ApprovalToken.Payload("approver-1", upsert.ProductId, upsert.AggregateVersion,
                DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()), "atlas-dev-approval-secret");
        var direto = await client.PublishProductAsync(new PublishProductRequest
        {
            RequestId = "grpc-pub-0",
            ApprovalToken = tokenDraft,
            ProductId = upsert.ProductId,
            ExpectedVersion = upsert.AggregateVersion,
            MarketId = "BR",
        });
        Assert.False(direto.Accepted);
        Assert.Contains("aprovação", direto.Reason);

        // 3) SubmitForApproval
        var submit = await client.SubmitForApprovalAsync(new SubmitForApprovalRequest
        {
            RequestId = "grpc-sub-1",
            ActorSub = "approver-1",
            ProductId = upsert.ProductId,
            ExpectedVersion = upsert.AggregateVersion,
        });
        Assert.True(submit.Accepted);

        // 4) publish SEM token → bloqueado na borda do owner (FailedPrecondition)
        var withoutToken = await Assert.ThrowsAsync<global::Grpc.Core.RpcException>(() =>
            client.PublishProductAsync(new PublishProductRequest
            {
                RequestId = "grpc-pub-nt",
                ApprovalToken = "",
                ProductId = upsert.ProductId,
                ExpectedVersion = submit.AggregateVersion,
                MarketId = "BR",
            }).ResponseAsync);
        Assert.Equal(global::Grpc.Core.StatusCode.FailedPrecondition, withoutToken.StatusCode);

        // 5) publish com version stale → conflito
        var stale = await client.PublishProductAsync(new PublishProductRequest
        {
            RequestId = "grpc-pub-stale",
            ApprovalToken = ApprovalToken.Issue(
                new ApprovalToken.Payload("approver-1", upsert.ProductId, upsert.AggregateVersion,
                    DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()), "atlas-dev-approval-secret"),
            ProductId = upsert.ProductId,
            ExpectedVersion = upsert.AggregateVersion, // já avançou no submit
            MarketId = "BR",
        });
        Assert.False(stale.Accepted);

        // 6) publish correto + evento no outbox
        var publish = await client.PublishProductAsync(new PublishProductRequest
        {
            RequestId = "grpc-pub-1",
            ApprovalToken = ApprovalToken.Issue(
                new ApprovalToken.Payload("approver-1", upsert.ProductId, submit.AggregateVersion,
                    DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()), "atlas-dev-approval-secret"),
            ProductId = upsert.ProductId,
            ExpectedVersion = submit.AggregateVersion,
            MarketId = "BR",
        });
        Assert.True(publish.Accepted, publish.Reason);
        Assert.False(string.IsNullOrEmpty(publish.EventId));

        var outbox = new MongoClient("mongodb://127.0.0.1:27017/?directConnection=true")
            .GetDatabase(_factory.Services.GetRequiredService<IConfiguration>()["Mongo:Database"])
            .GetCollection<MongoDB.Bson.BsonDocument>("catalog.outbox");
        Assert.Equal(1, (int)await outbox.CountDocumentsAsync(FilterDefinition<MongoDB.Bson.BsonDocument>.Empty));

        // 7) GetProduct reflete estado publicado
        var read = await client.GetProductAsync(new GetProductRequest { ProductId = upsert.ProductId, Locale = "pt-BR" });
        Assert.Equal("Published", read.Status);
        Assert.Single(read.Skus);
    }
}

/// <summary>Four-eyes (INV-ADMIN-001): token HMAC validado pelo owner — payload diverso/expirado/adulterado rejeitados.</summary>
public sealed class ApprovalTokenValidationTests
{
    private const string Secret = "atlas-dev-approval-secret";

    [Fact]
    public async Task Token_válido_publica_e_payload_diverso_é_rejeitado()
    {
        var dbName = $"atlas_tok_{Guid.NewGuid():N}";
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Mongo:Database", dbName);
            b.UseSetting("Approval:TokenSecret", Secret);
        });
        using var channel = GrpcChannel.ForAddress("http://localhost",
            new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() });
        var client = new CatalogOwnerService.CatalogOwnerServiceClient(channel);

        var upsert = await client.UpsertDraftAsync(new UpsertDraftRequest
        {
            RequestId = "tok-1", ActorSub = "editor",
            Titles = { new LocalizedText { Locale = "pt-BR", Text = "Token test" } },
            Skus = { new SkuDraft { SkuCode = "TOK-1" } },
        });
        await client.SubmitForApprovalAsync(new SubmitForApprovalRequest
        { RequestId = "tok-s", ActorSub = "editor", ProductId = upsert.ProductId, ExpectedVersion = upsert.AggregateVersion });

        var productId = Guid.Parse(upsert.ProductId);
        var token = ApprovalToken.Issue(
            new ApprovalToken.Payload("approver-1", upsert.ProductId, upsert.AggregateVersion + 1,
                DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()), Secret);

        // token para OUTRO product_id → rejeitado (AI-003 análogo)
        var outraOferta = Guid.NewGuid();
        var tokenAlien = ApprovalToken.Issue(
            new ApprovalToken.Payload("approver-1", outraOferta.ToString(), upsert.AggregateVersion + 1,
                DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()), Secret);
        var alienCall = await Assert.ThrowsAsync<global::Grpc.Core.RpcException>(() =>
            client.PublishProductAsync(new PublishProductRequest
            {
                RequestId = "tok-alien", ApprovalToken = tokenAlien, ProductId = upsert.ProductId,
                ExpectedVersion = upsert.AggregateVersion + 1, MarketId = "BR",
            }).ResponseAsync);
        Assert.Contains("product_id", alienCall.Status.Detail);

        // token adulterado (assinatura inválida) → FailedPrecondition
        var tampered = token[..^4] + "AAAA";
        var tamperedCall = await Assert.ThrowsAsync<global::Grpc.Core.RpcException>(() =>
            client.PublishProductAsync(new PublishProductRequest
            {
                RequestId = "tok-tamper", ApprovalToken = tampered, ProductId = upsert.ProductId,
                ExpectedVersion = upsert.AggregateVersion + 1, MarketId = "BR",
            }).ResponseAsync);
        Assert.Equal(global::Grpc.Core.StatusCode.FailedPrecondition, tamperedCall.StatusCode);

        // token expirado → FailedPrecondition
        var expired = ApprovalToken.Issue(
            new ApprovalToken.Payload("approver-1", upsert.ProductId, upsert.AggregateVersion + 1,
                DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()), Secret);
        var expiredCall = await Assert.ThrowsAsync<global::Grpc.Core.RpcException>(() =>
            client.PublishProductAsync(new PublishProductRequest
            {
                RequestId = "tok-exp", ApprovalToken = expired, ProductId = upsert.ProductId,
                ExpectedVersion = upsert.AggregateVersion + 1, MarketId = "BR",
            }).ResponseAsync);
        Assert.Equal(global::Grpc.Core.StatusCode.FailedPrecondition, expiredCall.StatusCode);

        // token VÁLIDO (payload correspondente) → publica
        var ok = await client.PublishProductAsync(new PublishProductRequest
        {
            RequestId = "tok-ok", ApprovalToken = token, ProductId = upsert.ProductId,
            ExpectedVersion = upsert.AggregateVersion + 1, MarketId = "BR",
        });
        Assert.True(ok.Accepted, ok.Reason);
    }
}
