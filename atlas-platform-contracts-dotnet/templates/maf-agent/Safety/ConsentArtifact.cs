namespace __AGENT__.Safety;

/// <summary>
/// Consent artifact (ADR-0009): subject + nonce + TTL + hash canônico do payload.
/// Emitido SÓ por ação explícita em UI confiável; validado no servidor em ApplyApprovedCartChange.
/// </summary>
public sealed record ConsentArtifact(
    string Subject,
    string Nonce,
    DateTimeOffset ExpiresAtUtc,
    string PayloadHash)
{
    public bool IsValid(string subject, string payloadHash, DateTimeOffset nowUtc) =>
        Subject == subject
        && PayloadHash == payloadHash
        && nowUtc < ExpiresAtUtc;
}
