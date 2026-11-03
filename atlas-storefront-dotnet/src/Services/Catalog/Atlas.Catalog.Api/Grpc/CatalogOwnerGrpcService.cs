using Atlas.Catalog.Application;
using Microsoft.Extensions.Configuration;
using Atlas.Catalog.Domain;
using Atlas.Contracts.Catalog.V1;
using DomainLocalizedText = Atlas.Catalog.Domain.LocalizedText;
using ProtoLocalizedText = Atlas.Contracts.Catalog.V1.LocalizedText;
using Grpc.Core;
using UpsertDraftRequest = Atlas.Contracts.Catalog.V1.UpsertDraftRequest;
using UpsertDraftResponse = Atlas.Contracts.Catalog.V1.UpsertDraftResponse;
using PublishProductRequest = Atlas.Contracts.Catalog.V1.PublishProductRequest;
using PublishProductResponse = Atlas.Contracts.Catalog.V1.PublishProductResponse;
using GetProductRequest = Atlas.Contracts.Catalog.V1.GetProductRequest;
using GetProductResponse = Atlas.Contracts.Catalog.V1.GetProductResponse;
using SubmitForApprovalRequest = Atlas.Contracts.Catalog.V1.SubmitForApprovalRequest;
using SubmitForApprovalResponse = Atlas.Contracts.Catalog.V1.SubmitForApprovalResponse;

namespace Atlas.Catalog.Api.Grpc;

/// <summary>
/// Owner gRPC do Catalog (M2): recebe comandos do Control/Admin já autenticados
/// (identidade de workload) e REVALIDA políticas no domínio antes de persistir —
/// a aprovação do Control nunca é bypass (ADR-0001, INV-SEC-001).
/// approval_token: presença obrigatória no M2; validação criptográfica chega com a
/// fatia four-eyes do Control (ADR-0001/0007 — nota registrada na SPEC-002).
/// </summary>
public sealed class CatalogOwnerGrpcService(
    UpsertDraftHandler upsertHandler,
    PublishProductHandler publishHandler,
    ICatalogStore store) : CatalogOwnerService.CatalogOwnerServiceBase
{
    public override async Task<UpsertDraftResponse> UpsertDraft(UpsertDraftRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.ActorSub == "")
            throw new RpcException(new Status(StatusCode.InvalidArgument, "request_id e actor_sub obrigatórios"));

        var result = await upsertHandler.HandleAsync(new UpsertDraftCommand(
            RequestId: request.RequestId,
            ActorSub: request.ActorSub,
            MarketId: string.IsNullOrWhiteSpace(request.CategoryId) ? "BR" : request.CategoryId, // market vira campo próprio no proto v2 (nota)
            Titles: [.. request.Titles.Select(t => (t.Locale, t.Text))],
            SkuCodes: [.. request.Skus.Select(s => s.SkuCode)]), context.CancellationToken);

        return result.Status switch
        {
            UpsertDraftStatus.Rejected => throw new RpcException(new Status(StatusCode.InvalidArgument, result.Reason ?? "rejeitado")),
            _ => new UpsertDraftResponse
            {
                ProductId = result.ProductId.Value.ToString(),
                AggregateVersion = result.Version,
                Accepted = true,
            },
        };
    }

    public override async Task<SubmitForApprovalResponse> SubmitForApproval(SubmitForApprovalRequest request, ServerCallContext context)
    {
        var id = ParseId(request.ProductId);
        var outcome = await store.SubmitForApprovalAsync(id, request.ExpectedVersion, context.CancellationToken);
        return new SubmitForApprovalResponse
        {
            Accepted = outcome.Accepted,
            AggregateVersion = outcome.Version,
            Reason = outcome.Reason ?? "",
        };
    }

    public override async Task<PublishProductResponse> PublishProduct(PublishProductRequest request, ServerCallContext context)
    {
        // Four-eyes (INV-ADMIN-001): token HMAC emitido pelo Control na decisão do approver;
        // owner valida assinatura, exp, product_id e expected_version — payload diverso é rejeitado.
        var secret = context.GetHttpContext().RequestServices
            .GetRequiredService<IConfiguration>()["Approval:TokenSecret"] ?? "atlas-dev-approval-secret";
        var approvedBy = ApprovalToken.TryValidate(
            request.ApprovalToken, secret, request.ProductId, request.ExpectedVersion,
            DateTimeOffset.UtcNow, out var tokenFailure);
        if (approvedBy is null)
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                $"approval_token rejeitado (four-eyes): {tokenFailure}"));

        var id = ParseId(request.ProductId);
        var outcome = await publishHandler.HandleAsync(
            id, request.ExpectedVersion, $"approval:{approvedBy.Sub}",
            request.MarketId, request.RequestId, context.CancellationToken);

        return new PublishProductResponse
        {
            Accepted = outcome.Accepted,
            AggregateVersion = outcome.Version,
            EventId = outcome.EventId ?? "",
            Reason = outcome.Reason ?? "",
        };
    }

    public override async Task<GetProductResponse> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        var draft = await store.GetAsync(new ProductId(Guid.Parse(request.ProductId)), context.CancellationToken);
        if (draft is null) throw new RpcException(new Status(StatusCode.NotFound, "produto inexistente"));
        var response = new GetProductResponse
        {
            ProductId = draft.Id.Value.ToString(),
            Status = draft.Status.ToString(),
            AggregateVersion = draft.Version,
        };
        response.Titles.AddRange(draft.Titles.Select(t => new ProtoLocalizedText { Locale = t.Locale.Value, Text = t.Text }));
        response.Skus.AddRange(draft.Skus.Select(s => new SkuView { SkuId = s.Id.Value.ToString(), SkuCode = s.Code }));
        return response;
    }

    private static ProductId ParseId(string raw)
    {
        if (!Guid.TryParse(raw, out var guid))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"product_id inválido: '{raw}'"));
        return new ProductId(guid);
    }
}
