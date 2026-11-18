namespace Atlas.Catalog.Infrastructure;

using System.Text.Json;
using Atlas.Catalog.Application;
using Atlas.Catalog.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

/// <summary>
/// Adapter MongoDB do Catalog (ADR-0002/0003): upsert de rascunho e publicação com
/// transação de replica set (aggregate + outbox Product.PublishedV1 na MESMA escrita).
/// Publicação exige estado PendingApproval e version esperado (concorrência otimista).
/// </summary>
public sealed class MongoCatalogStore : ICatalogStore
{
    private static readonly bool _guid = RegisterGuid();
    private static bool RegisterGuid()
    {
        try { BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); }
        catch (MongoDB.Bson.BsonSerializationException) { }
        return true;
    }

    private readonly IMongoDatabase _db;

    public MongoCatalogStore(IMongoDatabase db) => _db = db;

    public static MongoCatalogStore Create(string connectionString, string databaseName) =>
        new(new MongoClient(connectionString).GetDatabase(databaseName));

    private IMongoCollection<ProductDoc> Products() => _db.GetCollection<ProductDoc>("catalog.products");
    private IMongoCollection<OutboxDoc> Outbox() => _db.GetCollection<OutboxDoc>("catalog.outbox");

    public async Task<UpsertDraftResult> UpsertDraftInTransactionAsync(UpsertDraftCommand command, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var existing = await Products().Find(s, p => p.RequestId == command.RequestId).FirstOrDefaultAsync(cancellationToken: token);
            if (existing is not null)
                return new UpsertDraftResult(UpsertDraftStatus.Updated, new ProductId(existing.Id), existing.Version, "idempotente por request_id");

            var draft = ProductDraft.Create();
            foreach (var (locale, title) in command.Titles)
                draft.WithTitle(locale, title);
            foreach (var code in command.SkuCodes)
                draft.AddSku(code, new Dictionary<string, string>());

            await Products().InsertOneAsync(s, ProductDoc.From(draft, command.RequestId, command.MarketId, command.ActorSub), cancellationToken: token);
            return new UpsertDraftResult(UpsertDraftStatus.Created, draft.Id, Version: 1); // versão PERSISTIDA no insert
        }, cancellationToken: ct);
    }

    public async Task<PublishOutcome> SubmitForApprovalAsync(ProductId productId, long expectedVersion, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var doc = await Products().Find(s, p => p.Id == productId.Value).FirstOrDefaultAsync(cancellationToken: token);
            if (doc is null) return new PublishOutcome(false, 0, null, "produto inexistente");
            if (doc.Version != expectedVersion)
                return new PublishOutcome(false, doc.Version, null, "conflito de versão");
            var draft = doc.ToDomain();
            try { draft.SubmitForApproval(); }
            catch (InvalidOperationException ex) { return new PublishOutcome(false, doc.Version, null, ex.Message); }
            var result = await Products().UpdateOneAsync(s,
                p => p.Id == productId.Value && p.Version == expectedVersion,
                Builders<ProductDoc>.Update.Set(p => p.Status, ProductStatus.PendingApproval).Inc(p => p.Version, 1),
                cancellationToken: token);
            return result.ModifiedCount == 1
                ? new PublishOutcome(true, doc.Version + 1, null, null)
                : new PublishOutcome(false, doc.Version, null, "conflito de concorrência");
        }, cancellationToken: ct);
    }

    public async Task<PublishOutcome> PublishInTransactionAsync(
        ProductId productId, long expectedVersion, string approvedBy, string marketId, string correlationId, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var doc = await Products().Find(s, p => p.Id == productId.Value).FirstOrDefaultAsync(cancellationToken: token);
            if (doc is null) return new PublishOutcome(false, 0, null, "produto inexistente");
            if (doc.Version != expectedVersion)
                return new PublishOutcome(false, doc.Version, null, $"conflito de versão (esperado {expectedVersion}, atual {doc.Version})");

            // Revalida as invariantes no DOMÍNIO (estado PendingApproval + título + SKU) — owner é o perímetro.
            var draft = doc.ToDomain();
            ProductPublishedV1 @event;
            try
            {
                @event = draft.Publish(approvedBy, DateTimeOffset.UtcNow);
            }
            catch (InvalidOperationException ex)
            {
                return new PublishOutcome(false, doc.Version, null, ex.Message);
            }

            var update = Builders<ProductDoc>.Update
                .Set(p => p.Status, ProductStatus.Published)
                .Set(p => p.ApprovedBy, approvedBy)
                .Inc(p => p.Version, 1);
            var result = await Products().UpdateOneAsync(s, p => p.Id == productId.Value && p.Version == expectedVersion,
                update, cancellationToken: token);
            if (result.ModifiedCount != 1)
                return new PublishOutcome(false, doc.Version, null, "conflito de concorrência na publicação");

            var eventId = Guid.NewGuid();
            await Outbox().InsertOneAsync(s, OutboxDoc.DomainEvent(
                eventId, "Product.PublishedV1", productId.Value.ToString(), correlationId,
                new BsonDocument
                {
                    ["product_id"] = productId.Value.ToString(),
                    ["market_id"] = marketId,
                    ["approved_by"] = approvedBy,
                    ["sku_codes"] = new BsonArray(@event.SkuCodes),
                    ["title"] = doc.ToDomain().TitleFor(new Locale("pt-BR"))?.Text
                                ?? doc.ToDomain().Titles.FirstOrDefault()?.Text ?? "",
                    ["aggregate_version"] = @event.AggregateVersion,
                    ["image_keys"] = new BsonArray(doc.Images.Select(i => i.Key)),
                }, @event.AggregateVersion), cancellationToken: token);

            return new PublishOutcome(true, @event.AggregateVersion, eventId.ToString(), null);
        }, cancellationToken: ct);
    }

    /// <summary>MarketId do rascunho (persistido no doc — uso dev/seed do publish direto).</summary>
    public async Task<string?> GetMarketIdAsync(ProductId productId, CancellationToken ct)
    {
        var doc = await Products().Find(p => p.Id == productId.Value).FirstOrDefaultAsync(ct);
        return doc?.MarketId;
    }

    public async Task<ProductDraft?> GetAsync(ProductId productId, CancellationToken ct)
    {
        var doc = await Products().Find(p => p.Id == productId.Value).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    /// <summary>Grade de produtos do Control (leitura paginada; filtros opcionais).</summary>
    public async Task<(IReadOnlyList<CatalogDraftSummary> Items, long Total)> ListDraftsAsync(
        string? status, string? market, string? search, int page, int pageSize, CancellationToken ct)
    {
        var filter = Builders<ProductDoc>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProductStatus>(status, true, out var st))
            filter &= Builders<ProductDoc>.Filter.Eq(d => d.Status, st);
        if (!string.IsNullOrWhiteSpace(market))
            filter &= Builders<ProductDoc>.Filter.Eq(d => d.MarketId, market);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var rx = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search), "i");
            var titleMatch = Builders<ProductDoc.TitleDoc>.Filter.Regex(t => t.Text, rx);
            var skuMatch = Builders<ProductDoc.SkuDoc>.Filter.Regex(s => s.Code, rx);
            filter &= Builders<ProductDoc>.Filter.Or(
                Builders<ProductDoc>.Filter.ElemMatch(d => d.Titles, titleMatch),
                Builders<ProductDoc>.Filter.ElemMatch(d => d.Skus, skuMatch));
        }
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await Products().CountDocumentsAsync(filter, cancellationToken: ct);
        var docs = await Products().Find(filter)
            .SortByDescending(d => d.CreatedAtUtc).Skip((page - 1) * pageSize).Limit(pageSize)
            .ToListAsync(ct);
        return ([.. docs.Select(d => new CatalogDraftSummary(
            d.Id, d.Status.ToString(), d.Version, d.MarketId,
            [.. d.Titles.Select(t => (t.Locale, t.Text))], d.Skus.Count, d.CreatedBy,
            [.. (d.Images ?? []).Select(i => (i.Key, i.ContentType, i.SizeBytes))]))], total);
    }

    /// <summary>Edição de rascunho com CAS (títulos/atributos; SKUs continuam append-only).</summary>
    public async Task<PublishOutcome> UpdateDraftAsync(
        ProductId productId, long expectedVersion, IReadOnlyList<(string Locale, string Title)> titles,
        IReadOnlyDictionary<string, string> attributes, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var doc = await Products().Find(s, p => p.Id == productId.Value).FirstOrDefaultAsync(cancellationToken: token);
            if (doc is null) return new PublishOutcome(false, 0, null, "produto inexistente");
            if (doc.Version != expectedVersion)
                return new PublishOutcome(false, doc.Version, null, $"conflito de versão (esperado {expectedVersion}, atual {doc.Version})");
            var draft = doc.ToDomain();
            try
            {
                draft.UpdateTitles(titles).UpdateAttributes(attributes);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return new PublishOutcome(false, doc.Version, null, ex.Message);
            }
            var fresh = ProductDoc.From(draft, doc.RequestId, doc.MarketId, doc.CreatedBy);
            fresh.CreatedAtUtc = doc.CreatedAtUtc;
            fresh.Version = draft.Version; // From() ancora em 1 (insert); no update preserva o CAS
            var result = await Products().ReplaceOneAsync(s,
                p => p.Id == productId.Value && p.Version == expectedVersion, fresh, cancellationToken: token);
            return result.ModifiedCount == 1
                ? new PublishOutcome(true, draft.Version, null, null)
                : new PublishOutcome(false, doc.Version, null, "conflito de concorrência");
        }, cancellationToken: ct);
    }

    /// <summary>Remoção física de rascunho nunca publicado (Draft apenas — CAS por status).</summary>
    public async Task<(bool Accepted, string? Reason)> DeleteDraftAsync(ProductId productId, CancellationToken ct)
    {
        var result = await Products().DeleteOneAsync(
            p => p.Id == productId.Value && p.Status == ProductStatus.Draft, ct);
        return result.DeletedCount == 1
            ? (true, null)
            : (false, "produto inexistente ou fora de rascunho (só Draft apaga; publicado despublica)");
    }

    /// <summary>Despublicação com outbox na mesma transação (Published → Unpublished + evento).</summary>
    public async Task<PublishOutcome> UnpublishInTransactionAsync(
        ProductId productId, long expectedVersion, string actorSub, string reason, string correlationId, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var doc = await Products().Find(s, p => p.Id == productId.Value).FirstOrDefaultAsync(cancellationToken: token);
            if (doc is null) return new PublishOutcome(false, 0, null, "produto inexistente");
            if (doc.Version != expectedVersion)
                return new PublishOutcome(false, doc.Version, null, $"conflito de versão (esperado {expectedVersion}, atual {doc.Version})");
            var draft = doc.ToDomain();
            ProductUnpublishedV1 @event;
            try
            {
                @event = draft.Unpublish(actorSub, reason, DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return new PublishOutcome(false, doc.Version, null, ex.Message);
            }
            var result = await Products().UpdateOneAsync(s,
                p => p.Id == productId.Value && p.Version == expectedVersion,
                Builders<ProductDoc>.Update.Set(p => p.Status, ProductStatus.Unpublished).Inc(p => p.Version, 1),
                cancellationToken: token);
            if (result.ModifiedCount != 1)
                return new PublishOutcome(false, doc.Version, null, "conflito de concorrência");
            var eventId = Guid.NewGuid();
            await Outbox().InsertOneAsync(s, OutboxDoc.DomainEvent(
                eventId, "Product.UnpublishedV1", productId.Value.ToString(), correlationId,
                new BsonDocument
                {
                    ["product_id"] = productId.Value.ToString(),
                    ["market_id"] = doc.MarketId,
                    ["actor_sub"] = actorSub,
                    ["reason"] = reason,
                    ["aggregate_version"] = @event.AggregateVersion,
                }, @event.AggregateVersion), cancellationToken: token);
            return new PublishOutcome(true, @event.AggregateVersion, eventId.ToString(), null);
        }, cancellationToken: ct);
    }

    /// <summary>Anexa imagem validada (HEAD) com CAS; em Published emite
    /// Product.ImagesUpdatedV1 na mesma transação para a projeção.</summary>
    public async Task<PublishOutcome> AttachImageAsync(
        ProductId productId, long expectedVersion, string objectKey, string contentType, long sizeBytes,
        string actorSub, string correlationId, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var doc = await Products().Find(s, p => p.Id == productId.Value).FirstOrDefaultAsync(cancellationToken: token);
            if (doc is null) return new PublishOutcome(false, 0, null, "produto inexistente");
            if (doc.Version != expectedVersion)
                return new PublishOutcome(false, doc.Version, null, $"conflito de versão (esperado {expectedVersion}, atual {doc.Version})");
            var draft = doc.ToDomain();
            try
            {
                draft.AttachImage(objectKey, contentType, sizeBytes, DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return new PublishOutcome(false, doc.Version, null, ex.Message);
            }
            var fresh = ProductDoc.From(draft, doc.RequestId, doc.MarketId, doc.CreatedBy);
            fresh.CreatedAtUtc = doc.CreatedAtUtc;
            fresh.Version = draft.Version;
            var result = await Products().ReplaceOneAsync(s,
                p => p.Id == productId.Value && p.Version == expectedVersion, fresh, cancellationToken: token);
            if (result.ModifiedCount != 1)
                return new PublishOutcome(false, doc.Version, null, "conflito de concorrência");
            string? eventId = null;
            if (draft.Status == ProductStatus.Published)
                eventId = await EmitImagesUpdatedAsync(s, productId, draft, correlationId, token);
            return new PublishOutcome(true, draft.Version, eventId, null);
        }, cancellationToken: ct);
    }

    /// <summary>Desanexa imagem com CAS; em Published emite Product.ImagesUpdatedV1.</summary>
    public async Task<PublishOutcome> DetachImageAsync(
        ProductId productId, long expectedVersion, string objectKey, string actorSub, string correlationId, CancellationToken ct)
    {
        using var session = await _db.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, token) =>
        {
            var doc = await Products().Find(s, p => p.Id == productId.Value).FirstOrDefaultAsync(cancellationToken: token);
            if (doc is null) return new PublishOutcome(false, 0, null, "produto inexistente");
            if (doc.Version != expectedVersion)
                return new PublishOutcome(false, doc.Version, null, $"conflito de versão (esperado {expectedVersion}, atual {doc.Version})");
            var draft = doc.ToDomain();
            try
            {
                draft.DetachImage(objectKey);
            }
            catch (InvalidOperationException ex)
            {
                return new PublishOutcome(false, doc.Version, null, ex.Message);
            }
            var fresh = ProductDoc.From(draft, doc.RequestId, doc.MarketId, doc.CreatedBy);
            fresh.CreatedAtUtc = doc.CreatedAtUtc;
            fresh.Version = draft.Version;
            var result = await Products().ReplaceOneAsync(s,
                p => p.Id == productId.Value && p.Version == expectedVersion, fresh, cancellationToken: token);
            if (result.ModifiedCount != 1)
                return new PublishOutcome(false, doc.Version, null, "conflito de concorrência");
            string? eventId = null;
            if (draft.Status == ProductStatus.Published)
                eventId = await EmitImagesUpdatedAsync(s, productId, draft, correlationId, token);
            return new PublishOutcome(true, draft.Version, eventId, null);
        }, cancellationToken: ct);
    }

    private async Task<string> EmitImagesUpdatedAsync(
        IClientSessionHandle s, ProductId productId, ProductDraft draft, string correlationId, CancellationToken token)
    {
        var eventId = Guid.NewGuid();
        await Outbox().InsertOneAsync(s, OutboxDoc.DomainEvent(
            eventId, "Product.ImagesUpdatedV1", productId.Value.ToString(), correlationId,
            new BsonDocument
            {
                ["product_id"] = productId.Value.ToString(),
                ["market_id"] = string.Empty, // projeção preserva market da linha existente
                ["image_keys"] = new BsonArray(draft.Images.Select(i => i.Key)),
                ["aggregate_version"] = draft.Version,
            }, draft.Version), cancellationToken: token);
        return eventId.ToString();
    }

    // ---- documentos ----
    public sealed class ProductDoc
    {
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public Guid Id { get; set; }
        public string RequestId { get; set; } = default!;
        public string MarketId { get; set; } = default!;
        public string CreatedBy { get; set; } = default!;
        public string? ApprovedBy { get; set; }
        public ProductStatus Status { get; set; }
        public long Version { get; set; } = 1;
        public List<TitleDoc> Titles { get; set; } = [];
        public List<SkuDoc> Skus { get; set; } = [];
        public Dictionary<string, string> Attributes { get; set; } = new();
        public List<ImageDoc> Images { get; set; } = [];
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public sealed record TitleDoc(string Locale, string Text);
        public sealed record SkuDoc(string SkuId, string Code);
        public sealed record ImageDoc(string Key, string ContentType, long SizeBytes, DateTimeOffset UploadedAtUtc);

        public static ProductDoc From(ProductDraft d, string requestId, string marketId, string createdBy) => new()
        {
            Id = d.Id.Value,
            RequestId = requestId,
            MarketId = marketId,
            CreatedBy = createdBy,
            Status = d.Status,
            Version = 1, // insert inicial = 1ª versão PERSISTIDA (increments de construção não contam)
            Titles = [.. d.Titles.Select(t => new TitleDoc(t.Locale.Value, t.Text))],
            Skus = [.. d.Skus.Select(s => new SkuDoc(s.Id.Value.ToString(), s.Code))],
            Attributes = new(d.Attributes),
            Images = [.. d.Images.Select(i => new ImageDoc(i.Key, i.ContentType, i.SizeBytes, i.UploadedAtUtc))],
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        public ProductDraft ToDomain() => ProductDraft.Hydrate(
            new ProductId(Id),
            Status,
            Version,
            [.. Titles.Select(t => (t.Locale, t.Text))],
            [.. Skus.Select(s => (new SkuId(Guid.Parse(s.SkuId)), s.Code))],
            new Dictionary<string, string>(Attributes ?? new Dictionary<string, string>()),
            [.. (Images ?? []).Select(i => new ProductImage(i.Key, i.ContentType, i.SizeBytes, i.UploadedAtUtc))]);
    }

    public sealed class OutboxDoc
    {
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public Guid Id { get; set; }
        public string EventType { get; set; } = default!;
        public string AggregateId { get; set; } = default!;
        public long AggregateVersion { get; set; } = 1;
        public string Topic { get; set; } = "atlas.catalog.v1";
        public bool Published { get; set; }
        public string CorrelationId { get; set; } = default!;
        public BsonDocument Payload { get; set; } = default!;
        public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public static OutboxDoc DomainEvent(Guid eventId, string eventType, string aggregateId, string correlationId, BsonDocument payload, long aggregateVersion = 1) => new()
        {
            Id = eventId, EventType = eventType, AggregateId = aggregateId, CorrelationId = correlationId, Payload = payload, AggregateVersion = aggregateVersion,
        };
    }
}
