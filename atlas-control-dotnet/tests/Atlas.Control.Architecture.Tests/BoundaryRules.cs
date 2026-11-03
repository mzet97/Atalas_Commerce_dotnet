using System.Reflection;
using Xunit;

namespace Atlas.Control.Architecture.Tests;

/// <summary>
/// Regras de fronteira do monólito Control (mestre §18.4; ADR-0001/0019 — abordagem por
/// reflection até validação de NetArchTest/ArchUnitNET no M1 final).
/// </summary>
public class BoundaryRules
{
    private static readonly string[] ModuleAssemblies =
    [
        "Atlas.Control.Modules.CatalogOperations",
        "Atlas.Control.Modules.Approvals",
        "Atlas.Control.Modules.InventoryOperations",
    ];

    private static readonly string[] AllowedSharedAssemblies =
    [
        "Atlas.Control.Module.Abstractions",
        "Atlas.Control.Modules.Kernel", // approvals/grants/audit são SHARED KERNEL do monólito (SPEC-004)
    ];

    /// <summary>Módulos não referenciam uns aos outros (grafo DAG; host é o único compositor).</summary>
    [Fact]
    public void Módulos_não_referenciam_uns_aos_outros()
    {
        foreach (var moduleName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(moduleName);
            var refs = assembly.GetReferencedAssemblies().Select(r => r.Name).ToHashSet();
            foreach (var other in ModuleAssemblies.Where(m => m != moduleName))
                Assert.DoesNotContain(other, refs);
        }
    }

    /// <summary>Módulos dependem apenas do contrato de módulos (e da BCL) — não do host.</summary>
    [Fact]
    public void Módulos_não_dependem_do_host()
    {
        foreach (var moduleName in ModuleAssemblies)
        {
            var refs = Assembly.Load(moduleName).GetReferencedAssemblies().Select(r => r.Name);
            Assert.DoesNotContain("Atlas.Control.Host", refs);
        }
    }

    /// <summary>
    /// REFINAMENTO (SPEC-002/M2, four-eyes — ADR-0001): o Control agora mantém ESTADO PRÓPRIO
    /// (approvals/audit) em Mongo DEDICADO, então MongoDB.Driver é permitido. O que continua
    /// PROIBIDO no Control: brokers/caches dos owners (Confluent.Kafka, StackExchange.Redis) e
    /// qualquer infraestrutura dos serviços A (garantida por não haver ProjectReference).
    /// </summary>
    /// <summary>Kernel é compartilhado; módulos NÃO podem referenciar outro MÓDULO além do kernel.</summary>
    [Fact]
    public void Módulos_referenciam_apenas_abstrações_ou_kernel()
    {
        foreach (var moduleName in ModuleAssemblies)
        {
            var refs = Assembly.Load(moduleName).GetReferencedAssemblies()
                .Select(r => r.Name).Where(n => n is not null).ToHashSet();
            var forbidden = refs.Where(r => r!.StartsWith("Atlas.Control.Modules.", StringComparison.Ordinal) && !AllowedSharedAssemblies.Contains(r)).ToList();
            Assert.True(forbidden.Count == 0, $"{moduleName} referencia módulos fora do kernel: {string.Join(", ", forbidden)}");
        }
    }

    [Theory]
    [InlineData("Atlas.Control.Host")]
    [InlineData("Atlas.Control.Modules.CatalogOperations")]
    [InlineData("Atlas.Control.Modules.Approvals")]
    [InlineData("Atlas.Control.Modules.InventoryOperations")]
    public void Nenhuma_referência_a_infra_dos_owners(string assemblyName)
    {
        var refs = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(r => r.Name);
        Assert.All(refs, r => Assert.False(
            r is "Confluent.Kafka" or "StackExchange.Redis",
            $"{assemblyName} referencia infra proibida: {r}"));
    }
}
