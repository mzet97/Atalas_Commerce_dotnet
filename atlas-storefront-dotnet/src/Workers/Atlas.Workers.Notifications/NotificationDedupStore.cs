namespace Atlas.Workers.Notifications;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

/// <summary>
/// Dedup de notificações por notification_key (SPEC-006 req.3; aceite: nunca duplicada):
/// índice ÚNICO no replica set — dois workers disputam o mesmo evento e exatamente um
/// ganha o envio (11000 aqui é fluxo normal: claim duplicado → False).
/// </summary>
public sealed class NotificationDedupStore
{
    public static NotificationDedupStore Create(string connectionString, string databaseName)
    {
        var store = new NotificationDedupStore(new MongoClient(connectionString).GetDatabase(databaseName));
        store.Sent().Indexes.CreateOne(new CreateIndexModel<SentDoc>(
            Builders<SentDoc>.IndexKeys.Ascending(d => d.NotificationKey),
            new CreateIndexOptions { Unique = true }));
        return store;
    }

    private readonly IMongoDatabase _db;
    private NotificationDedupStore(IMongoDatabase db) => _db = db;

    private IMongoCollection<SentDoc> Sent() => _db.GetCollection<SentDoc>("notifications.sent");

    /// <summary>True = este worker GANHOU o envio; False = duplicado (já enviado antes).</summary>
    public async Task<bool> TryClaimAsync(string notificationKey, string channel, string eventId, CancellationToken ct)
    {
        try
        {
            await Sent().InsertOneAsync(new SentDoc
            {
                NotificationKey = notificationKey,
                Channel = channel,
                EventId = eventId,
                SentAtUtc = DateTimeOffset.UtcNow,
            }, new InsertOneOptions(), ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            return false;
        }
    }

    public async Task<long> CountSentAsync(CancellationToken ct) =>
        await Sent().CountDocumentsAsync(FilterDefinition<SentDoc>.Empty, cancellationToken: ct);

    public sealed class SentDoc
    {
        [BsonId] [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string NotificationKey { get; set; } = default!;
        public string Channel { get; set; } = default!;
        public string EventId { get; set; } = default!;
        public DateTimeOffset SentAtUtc { get; set; }
    }
}
