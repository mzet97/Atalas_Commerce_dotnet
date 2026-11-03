namespace __SERVICE__.Domain;

/// <summary>Identidade forte do agregado raiz deste serviço.</summary>
public readonly record struct DraftId(Guid Value)
{
    public static DraftId New() => new(Guid.NewGuid());
    public bool IsValid => Value != Guid.Empty;
}

/// <summary>
/// Agregado inicial: substitua pelo agregado real do seu bounded context,
/// mantendo invariantes no domínio (SPEC do serviço correspondente).
/// </summary>
public sealed class Draft(DraftId id)
{
    public DraftId Id { get; } = id;
    public string? Summary { get; private set; }

    public static Draft Create() => new(DraftId.New());

    public void SetSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Resumo não pode ser vazio", nameof(summary));
        Summary = summary;
    }
}
