namespace Atlas.Catalog.Domain;

/// <summary>
/// Ciclo operacional do produto (CRUD do Control): edição de rascunho, exclusão de
/// rascunho nunca publicado e despublicação de produto ao vivo (INV-ADMIN-001: sair
/// da vitrine exige four-eyes no Control; o owner só executa com token válido).
/// Regra de concorrência: toda mutação incrementa Version (CAS no store).
/// Edição é permitida em Draft/PendingApproval/Unpublished — nunca em Published
/// (snapshot ao vivo; para corrigir, despublique → edite → resubmeta → publique).
/// </summary>
public sealed partial class ProductDraft
{
    /// <summary>Substitui os títulos localizados (validação de locale por item).</summary>
    public ProductDraft UpdateTitles(IEnumerable<(string Locale, string Title)> titles)
    {
        EnsureEditable();
        var list = titles.ToList();
        if (list.Count == 0) throw new ArgumentException("Ao menos um título é obrigatório", nameof(titles));
        _titles.Clear();
        foreach (var (locale, title) in list)
        {
            var lt = LocalizedText.Create(locale, title);
            _titles[lt.Locale.Value] = lt;
        }
        Version++;
        return this;
    }

    /// <summary>Mescla atributos (chaves informadas sobrescrevem; demais preservados).</summary>
    public ProductDraft UpdateAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        EnsureEditable();
        foreach (var kv in attributes)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) throw new ArgumentException("Chave de atributo obrigatória");
            _attributes[kv.Key] = kv.Value;
        }
        Version++;
        return this;
    }

    /// <summary>Guarda de remoção: só rascunho nunca publicado some sem rastro (sem evento).</summary>
    public void EnsureDeletable()
    {
        if (Status != ProductStatus.Draft)
            throw new InvalidOperationException($"Remoção direta só de rascunho (estado atual: {Status}); publicado despublica, pendente rejeita primeiro");
    }

    /// <summary>Tira o produto da venda (Published → Unpublished) com evento para as projeções.</summary>
    public ProductUnpublishedV1 Unpublish(string actorSub, string reason, DateTimeOffset occurredAtUtc)
    {
        if (Status != ProductStatus.Published)
            throw new InvalidOperationException($"Despublicação exige produto publicado (estado atual: {Status})");
        if (string.IsNullOrWhiteSpace(actorSub)) throw new ArgumentException("Ator obrigatório", nameof(actorSub));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Motivo obrigatório (auditoria)", nameof(reason));
        Status = ProductStatus.Unpublished;
        Version++;
        return new ProductUnpublishedV1(Id, actorSub, reason, Version, occurredAtUtc);
    }

    private void EnsureEditable()
    {
        if (Status == ProductStatus.Published)
            throw new InvalidOperationException("Produto publicado é imutável: despublique antes de editar (INV-CQRS-001)");
    }
}

/// <summary>Evento de domínio: produto saiu da venda — projeções removem das views.</summary>
public sealed record ProductUnpublishedV1(
    ProductId ProductId,
    string ActorSub,
    string Reason,
    long AggregateVersion,
    DateTimeOffset OccurredAtUtc);
