using Atlas.Control.Modules.Kernel;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Atlas.Control.Host.Tests;

/// <summary>
/// SPEC-004/REQ-ADM-003: verificador da cadeia de auditoria como TESTE — append sequencial,
/// verificação válida, TAMPERING detectado (brokenAtSeq), e encadeamento após reinício do store.
/// </summary>
public sealed class AuditChainTests
{
    private const string MongoConn = "mongodb://127.0.0.1:27017/?directConnection=true";

    private static (AuditChainStore Store, string DbName) NewStore()
    {
        var dbName = $"atlas_audit_{Guid.NewGuid():N}";
        return (AuditChainStore.Create(MongoConn, dbName), dbName);
    }

    [Fact]
    public async Task Cadeia_append_e_verificação_válida()
    {
        var (store, dbName) = NewStore();
        try
        {
            for (var i = 1; i <= 5; i++)
                await store.AppendAsync($"actor-{i}", "test.action", "CREATED", $"detail-{i}", $"corr-{i}", CancellationToken.None);

            var (valid, count, brokenAt, reason) = await store.VerifyAsync(CancellationToken.None);
            Assert.True(valid, $"reason={reason}");
            Assert.Equal(5, count);
            Assert.Null(brokenAt);
        }
        finally
        {
            await new MongoClient(MongoConn).DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task Tampering_no_registro_quebra_a_cadeia_com_brokenAtSeq()
    {
        var (store, dbName) = NewStore();
        try
        {
            for (var i = 1; i <= 4; i++)
                await store.AppendAsync("actor", "inventory.adjust.dispatch", i == 2 ? "DENIED_NO_GRANT" : "EXECUTED", null, $"corr-{i}", CancellationToken.None);
            Assert.True((await store.VerifyAsync(CancellationToken.None)).Valid);

            // AUDITOR/ATACANTE altera retroativamente uma NEGÇÃO para parecer EXECUTED
            var db = new MongoClient(MongoConn).GetDatabase(dbName);
            var tamper = Builders<BsonDocument>.Update.Set("Decision", "EXECUTED");
            await db.GetCollection<BsonDocument>("control.audit_chain")
                .UpdateOneAsync(new BsonDocument("Seq", 2), tamper);

            var (valid, count, brokenAt, reason) = await store.VerifyAsync(CancellationToken.None);
            Assert.False(valid);                    // mutação retroativa É detectada
            Assert.Equal(2, brokenAt);              // exatamente no registro adulterado
            Assert.Contains("não confere", reason);
        }
        finally
        {
            await new MongoClient(MongoConn).DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task Cadeia_continua_após_recriar_o_store_nova_instância()
    {
        var (store, dbName) = NewStore();
        try
        {
            await store.AppendAsync("a1", "test.a", "CREATED", null, "c1", CancellationToken.None);
            // reinício do processo: nova instância do store sobre o MESMO db — encadeia no último hash
            var store2 = AuditChainStore.Create(MongoConn, dbName);
            await store2.AppendAsync("a2", "test.b", "CREATED", null, "c2", CancellationToken.None);

            var (valid, count, _, reason) = await store2.VerifyAsync(CancellationToken.None);
            Assert.True(valid, $"reason={reason}");
            Assert.Equal(2, count);
        }
        finally
        {
            await new MongoClient(MongoConn).DropDatabaseAsync(dbName);
        }
    }
}
