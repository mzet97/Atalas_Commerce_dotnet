using Results = Microsoft.AspNetCore.Http.Results;

namespace __MODULE__;

/// <summary>Contrato local (substituir pelo Atlas.Control.Modules.Contracts no repo Control).</summary>
public interface IAtlasModule
{
    string Name { get; }
    void Configure(IModuleEndpoints endpoints);
}

public interface IModuleEndpoints
{
    void MapGet(string route, Delegate handler);
    void MapPost(string route, Delegate handler);
}

/// <summary>Módulo gerado — não referencie outros módulos (DAG guardado por teste de arquitetura).</summary>
public sealed class MyModule : IAtlasModule
{
    public string Name => "my-module";

    public void Configure(IModuleEndpoints endpoints) =>
        endpoints.MapGet("/modules/my-module/status", () =>
            Results.Ok(new { module = Name, generated = true }));
}
