using Atlas.Catalog.Application;
using Microsoft.Extensions.Configuration;
using Atlas.Catalog.Domain;
using Atlas.Contracts.Catalog.V1;
using DomainLocalizedText = Atlas.Catalog.Domain.LocalizedText;
using ProtoLocalizedText = Atlas.Contracts.Catalog.V1.LocalizedText;
using Grpc.Core;
using UpsertDraftRequest = Atlas.Contracts.Catalog.V1.UpsertDraftRequest;
using UpsertDraftResponse = Atlas.Contracts.Catalog.V1.UpsertDraftResponse;
using ListDraftsRequest = Atlas.Contracts.Catalog.V1.ListDraftsRequest;
using ListDraftsResponse = Atlas.Contracts.Catalog.V1.ListDraftsResponse;
using DraftView = Atlas.Contracts.Catalog.V1.DraftView;
using UpdateDraftRequest = Atlas.Contracts.Catalog.V1.UpdateDraftRequest;
using UpdateDraftResponse = Atlas.Contracts.Catalog.V1.UpdateDraftResponse;
using DeleteDraftRequest = Atlas.Contracts.Catalog.V1.DeleteDraftRequest;
using DeleteDraftResponse = Atlas.Contracts.Catalog.V1.DeleteDraftResponse;
using UnpublishProductRequest = Atlas.Contracts.Catalog.V1.UnpublishProductRequest;
using UnpublishProductResponse = Atlas.Contracts.Catalog.V1.UnpublishProductResponse;
using RequestImageUploadRequest = Atlas.Contracts.Catalog.V1.RequestImageUploadRequest;
using RequestImageUploadResponse = Atlas.Contracts.Catalog.V1.RequestImageUploadResponse;
using AttachImageRequest = Atlas.Contracts.Catalog.V1.AttachImageRequest;
using AttachImageResponse = Atlas.Contracts.Catalog.V1.AttachImageResponse;
using DetachImageRequest = Atlas.Contracts.Catalog.V1.DetachImageRequest;
using DetachImageResponse = Atlas.Contracts.Catalog.V1.DetachImageResponse;
using ImageView = Atlas.Contracts.Catalog.V1.ImageView;
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
    UpdateDraftHandler updateHandler,
    PublishProductHandler publishHandler,
    RequestImageUploadHandler uploadHandler,
    AttachImageHandler attachHandler,
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
        response.Images.AddRange(draft.Images.Select(i => new ImageView { Key = i.Key, ContentType = i.ContentType, SizeBytes = i.SizeBytes }));
        return response;
    }

    public override async Task<ListDraftsResponse> ListDrafts(ListDraftsRequest request, ServerCallContext context)
    {
        var (items, total) = await store.ListDraftsAsync(
            string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
            string.IsNullOrWhiteSpace(request.MarketId) ? null : request.MarketId,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search,
            request.Page <= 0 ? 1 : request.Page,
            request.PageSize is <= 0 or > 200 ? 50 : request.PageSize,
            context.CancellationToken);
        var response = new ListDraftsResponse { Total = total };
        response.Items.AddRange(items.Select(i =>
        {
            var view = new DraftView
            {
                ProductId = i.Id.ToString(),
                Status = i.Status,
                AggregateVersion = i.Version,
                MarketId = i.MarketId,
                SkuCount = i.SkuCount,
                CreatedBy = i.CreatedBy,
            };
            view.Titles.AddRange(i.Titles.Select(t => new ProtoLocalizedText { Locale = t.Locale, Text = t.Title }));
            view.Images.AddRange((i.Images ?? []).Select(m => new ImageView { Key = m.Key, ContentType = m.ContentType, SizeBytes = m.SizeBytes }));
            return view;
        }));
        return response;
    }

    public override async Task<RequestImageUploadResponse> RequestImageUpload(RequestImageUploadRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId) || string.IsNullOrWhiteSpace(request.ActorSub))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "product_id e actor_sub obrigatórios"));
        if (string.IsNullOrWhiteSpace(request.ContentType) || request.ContentLength <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "content_type e content_length obrigatórios"));
        var (ok, url, key, expires, reason) = await uploadHandler.HandleAsync(
            ParseId(request.ProductId), request.ActorSub, request.ContentType, request.ContentLength, context.CancellationToken);
        return ok
            ? new RequestImageUploadResponse { Accepted = true, UploadUrl = url!, ObjectKey = key!, ExpiresAtUtc = expires!.Value.ToString("o") }
            : new RequestImageUploadResponse { Accepted = false, Reason = reason ?? "upload rejeitado" };
    }

    public override async Task<AttachImageResponse> AttachImage(AttachImageRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId) || string.IsNullOrWhiteSpace(request.ObjectKey))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "product_id e object_key obrigatórios"));
        var outcome = await attachHandler.HandleAsync(
            ParseId(request.ProductId), request.ExpectedVersion, request.ActorSub,
            request.ObjectKey, request.RequestId, context.CancellationToken);
        return outcome.Accepted
            ? new AttachImageResponse { Accepted = true, AggregateVersion = outcome.Version }
            : outcome.Reason == "produto inexistente"
                ? throw new RpcException(new Status(StatusCode.NotFound, outcome.Reason))
                : new AttachImageResponse { Accepted = false, AggregateVersion = outcome.Version, Reason = outcome.Reason ?? "" };
    }

    public override async Task<DetachImageResponse> DetachImage(DetachImageRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId) || string.IsNullOrWhiteSpace(request.ObjectKey))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "product_id e object_key obrigatórios"));
        var outcome = await store.DetachImageAsync(
            ParseId(request.ProductId), request.ExpectedVersion, request.ObjectKey,
            request.ActorSub, request.RequestId, context.CancellationToken);
        return outcome.Accepted
            ? new DetachImageResponse { Accepted = true, AggregateVersion = outcome.Version }
            : outcome.Reason == "produto inexistente"
                ? throw new RpcException(new Status(StatusCode.NotFound, outcome.Reason))
                : new DetachImageResponse { Accepted = false, AggregateVersion = outcome.Version, Reason = outcome.Reason ?? "" };
    }

    public override async Task<UpdateDraftResponse> UpdateDraft(UpdateDraftRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId) || string.IsNullOrWhiteSpace(request.ActorSub))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "product_id e actor_sub obrigatórios"));
        if (request.Titles.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ao menos um título é obrigatório"));
        var outcome = await updateHandler.HandleAsync(new UpdateDraftCommand(
            ParseId(request.ProductId),
            request.ExpectedVersion,
            request.ActorSub,
            [.. request.Titles.Select(t => (t.Locale, t.Text))],
            new Dictionary<string, string>(request.Attributes)), context.CancellationToken);
        return outcome.Accepted
            ? new UpdateDraftResponse { ProductId = request.ProductId, AggregateVersion = outcome.Version, Accepted = true }
            : outcome.Reason == "produto inexistente"
                ? throw new RpcException(new Status(StatusCode.NotFound, outcome.Reason))
                : new UpdateDraftResponse { ProductId = request.ProductId, AggregateVersion = outcome.Version, Accepted = false, Reason = outcome.Reason ?? "" };
    }

    public override async Task<DeleteDraftResponse> DeleteDraft(DeleteDraftRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "product_id obrigatório"));
        var (accepted, reason) = await store.DeleteDraftAsync(ParseId(request.ProductId), context.CancellationToken);
        return accepted
            ? new DeleteDraftResponse { Accepted = true }
            : new DeleteDraftResponse { Accepted = false, Reason = reason ?? "" };
    }

    public override async Task<UnpublishProductResponse> UnpublishProduct(UnpublishProductRequest request, ServerCallContext context)
    {
        // Four-eyes igual ao PublishProduct: token HMAC do Control, revalidado no owner.
        var secret = context.GetHttpContext().RequestServices
            .GetRequiredService<IConfiguration>()["Approval:TokenSecret"] ?? "atlas-dev-approval-secret";
        var approvedBy = ApprovalToken.TryValidate(
            request.ApprovalToken, secret, request.ProductId, request.ExpectedVersion,
            DateTimeOffset.UtcNow, out var tokenFailure);
        if (approvedBy is null)
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                $"approval_token rejeitado (four-eyes): {tokenFailure}"));

        var id = ParseId(request.ProductId);
        var outcome = await store.UnpublishInTransactionAsync(
            id, request.ExpectedVersion, approvedBy.Sub, request.Reason,
            request.RequestId, context.CancellationToken);
        return outcome.Accepted
            ? new UnpublishProductResponse { Accepted = true, AggregateVersion = outcome.Version, EventId = outcome.EventId ?? "" }
            : new UnpublishProductResponse { Accepted = false, AggregateVersion = outcome.Version, Reason = outcome.Reason ?? "" };
    }

    private static ProductId ParseId(string raw)
    {
        if (!Guid.TryParse(raw, out var guid))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"product_id inválido: '{raw}'"));
        return new ProductId(guid);
    }
}
