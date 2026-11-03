namespace __SERVICE__.Application;

using __SERVICE__.Domain;

/// <summary>Comando tipado (handler explícito — sem mediator por padrão, ADR-0012).</summary>
public sealed record SetDraftSummaryCommand(DraftId Id, string Summary);

public sealed class SetDraftSummaryHandler(IDraftStore store)
{
    public async Task HandleAsync(SetDraftSummaryCommand command, CancellationToken cancellationToken)
    {
        var draft = await store.GetAsync(command.Id, cancellationToken)
                     ?? throw new InvalidOperationException($"Draft {command.Id} não encontrado");
        draft.SetSummary(command.Summary);
        await store.SaveAsync(draft, cancellationToken);
    }
}

/// <summary>Port de persistência — adapter fica em Infrastructure (DIP).</summary>
public interface IDraftStore
{
    Task<Draft?> GetAsync(DraftId id, CancellationToken cancellationToken);
    Task SaveAsync(Draft draft, CancellationToken cancellationToken);
}
