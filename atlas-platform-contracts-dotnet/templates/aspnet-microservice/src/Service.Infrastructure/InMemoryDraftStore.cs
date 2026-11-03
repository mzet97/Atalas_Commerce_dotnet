namespace __SERVICE__.Infrastructure;

using System.Collections.Concurrent;
using __SERVICE__.Application;
using __SERVICE__.Domain;

/// <summary>
/// Adapter inicial em memória para desenvolvimento — trocar pelo adapter real
/// (MongoDB + outbox transacional) na spec do serviço (ADR-0002/0003).
/// </summary>
public sealed class InMemoryDraftStore : IDraftStore
{
    private readonly ConcurrentDictionary<DraftId, Draft> _drafts = [];

    public Task<Draft?> GetAsync(DraftId id, CancellationToken cancellationToken) =>
        Task.FromResult(_drafts.TryGetValue(id, out var d) ? d : null);

    public Task SaveAsync(Draft draft, CancellationToken cancellationToken)
    {
        _drafts[draft.Id] = draft;
        return Task.CompletedTask;
    }
}
