namespace __AGENT__;

/// <summary>
/// Esqueleto de módulo de agente MAF (SPEC-007). Regras não negociáveis já aplicadas:
/// (1) tools somente leitura com allowlist em código; (2) nenhuma tool de pagamento/
/// estoque/preço/IAM; (3) mutação de carrinho somente via consent artifact fora do agente
/// (ADR-0009/0015); (4) provider atrás de IChatClient (Ollama local — decisão do proprietário).
/// O uso concreto das APIs do MAF 1.26.0 (AIAgent/ChatClientAgent/WorkflowBuilder) só entra
/// após o smoke de API do M7 — nunca codar contra API não verificada (mestre §9.1).
/// </summary>
public static class AgentModule
{
    public static readonly string[] AllowedTools =
    [
        "search_catalog",
        "get_product_details",
        "get_current_offer",
        "get_inventory_hint",
        "compare_offers",
        "estimate_shipping",
        "get_customer_cart",
        "propose_cart_change",
    ];

    public static bool IsToolAllowed(string toolName) => AllowedTools.Contains(toolName);
}
