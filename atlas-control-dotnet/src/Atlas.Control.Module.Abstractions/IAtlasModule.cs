namespace Atlas.Control.Modules.Contracts;

/// <summary>
/// Contrato in-process de um módulo do monólito Control (references/dotnet 01 §2).
/// Módulos não referenciam uns aos outros; o host compõe o grafo (DAG — guardado por teste de arquitetura).
/// </summary>
public interface IAtlasModule
{
    string Name { get; }
    void Configure(IModuleEndpoints endpoints);
}

/// <summary>Pontos de extensão que o host oferece ao módulo registrar suas rotas.</summary>
public interface IModuleEndpoints
{
    void MapGet(string route, Delegate handler);
    void MapPost(string route, Delegate handler);
    /// <summary>Rota protegida por policy de autorização (roles do realm — ADR-0007).</summary>
    void MapPost(string route, string policy, Delegate handler);
    /// <summary>Rota DELETE protegida por policy (SPEC-004: revogação de grants).</summary>
    void MapDelete(string route, string policy, Delegate handler);
}
