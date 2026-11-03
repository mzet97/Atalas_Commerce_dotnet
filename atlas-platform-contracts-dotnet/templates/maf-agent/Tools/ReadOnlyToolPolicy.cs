namespace __AGENT__.Tools;

using __AGENT__;

/// <summary>Política de tools read-only — guardrail em código, não em prompt (ADR-0015).</summary>
public static class ReadOnlyToolPolicy
{
    /// <summary>Mutation tools proibidas por design (INV-AI-001); negação deve ser testável sem modelo.</summary>
    public static readonly string[] ForbiddenTools =
    [
        "capture_payment", "authorize_refund", "adjust_stock", "modify_price",
        "create_promotion", "grant_role", "submit_checkout_payment",
    ];

    public static bool IsAuthorized(string toolName) =>
        AgentModule.IsToolAllowed(toolName) && !ForbiddenTools.Contains(toolName);
}
