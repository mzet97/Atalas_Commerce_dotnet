using Atlas.Control.Modules.Contracts;

namespace Atlas.Control.Host;

/// <summary>Adaptador que conecta o contrato de módulos ao pipeline HTTP do host.</summary>
internal sealed class ModuleEndpoints(IEndpointRouteBuilder routes) : IModuleEndpoints
{
    public void MapGet(string route, Delegate handler) => routes.MapGet(route, handler);
    public void MapPost(string route, Delegate handler) => routes.MapPost(route, handler);
    public void MapPost(string route, string policy, Delegate handler) =>
        routes.MapPost(route, handler).RequireAuthorization(policy);
    public void MapDelete(string route, string policy, Delegate handler) =>
        routes.MapDelete(route, handler).RequireAuthorization(policy);
}
