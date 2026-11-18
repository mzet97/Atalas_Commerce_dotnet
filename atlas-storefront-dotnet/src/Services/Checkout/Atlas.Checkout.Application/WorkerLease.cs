namespace Atlas.Checkout.Application;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;


/// <summary>
/// Lease distribuído (SPEC-004+): apenas UM host executa reconciliador/recovery por vez.
/// TTL curto com heartbeat — crash do holder libera a lease naturalmente (expiração).
/// </summary>
public interface IWorkerLease
{
    Task<bool> TryAcquireAsync(string leaseName, string holder, TimeSpan ttl, CancellationToken ct);
}

public sealed class MongoWorkerLease(IMongoDatabase database) : IWorkerLease
{
    private readonly IMongoDatabase _db = database;

    private IMongoCollection<LeaseDoc> Leases() => _db.GetCollection<LeaseDoc>("worker.leases");

    public async Task<bool> TryAcquireAsync(string leaseName, string holder, TimeSpan ttl, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // 1) REGRESSÃO cluster .51: lease vencida de OUTRO holder travava para sempre —
        // o CAS antigo exigia Holder == solicitante. Takeover: quem chegar primeiro numa
        // lease vencida assume (CAS atômico: só um ModifiedCount == 1).
        var takeOver = await Leases().UpdateOneAsync(
            Builders<LeaseDoc>.Filter.And(
                Builders<LeaseDoc>.Filter.Eq(l => l.Name, leaseName),
                Builders<LeaseDoc>.Filter.Lt(l => l.ExpiresAtUtc, now)),
            Builders<LeaseDoc>.Update
                .Set(l => l.Holder, holder)
                .Set(l => l.ExpiresAtUtc, now.Add(ttl)),
            cancellationToken: ct);
        if (takeOver.ModifiedCount == 1)
        {
            Console.WriteLine($"[lease] '{leaseName}' assumida por '{holder}' (takeover de lease vencida)");
            return true;
        }

        // 2) primeira aquisição — índice único garante um holder; 11000 = outro host segurando
        try
        {
            await Leases().InsertOneAsync(new LeaseDoc
            {
                Name = leaseName, Holder = holder, ExpiresAtUtc = now.Add(ttl),
            }, new InsertOneOptions(), ct);
            Console.WriteLine($"[lease] '{leaseName}' criada por '{holder}'");
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            // já existe e válida: heartbeat se somos o holder, senão negado
            var result = await Leases().UpdateOneAsync(
                Builders<LeaseDoc>.Filter.And(
                    Builders<LeaseDoc>.Filter.Eq(l => l.Name, leaseName),
                    Builders<LeaseDoc>.Filter.Eq(l => l.Holder, holder)),
                Builders<LeaseDoc>.Update.Set(l => l.ExpiresAtUtc, now.Add(ttl)),
                cancellationToken: ct);
            return result.ModifiedCount == 1;
        }
    }

    public sealed class LeaseDoc
    {
        // NOTA: ExpiresAtUtc é DateTime (NUNCA DateTimeOffset): o driver MongoDB v3
        // serializa DateTimeOffset como DOCUMENTO {DateTime,Ticks,Offset}, quebrando
        // comparações $lt/$gt no servidor (takeover nunca disparava — provado em cluster).
        // DateTime UTC vira BSON datetime e o CAS volta a funcionar.
        [BsonId] [BsonRepresentation(BsonType.String)]
        public string Name { get; set; } = default!;
        public string Holder { get; set; } = default!;
        public DateTime ExpiresAtUtc { get; set; }
    }
}
