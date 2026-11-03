namespace Atlas.ShoppingAssistant.Api.Fake;

using Microsoft.Extensions.AI;

/// <summary>
/// IChatClient FAKE determinístico p/ CI (SPEC-007: CI roda fake; Ollama é o gate real).
/// Script de respostas fixas — CI valida allowlist/fallback sem depender de modelo.
/// </summary>
public sealed class ScriptedChatClient(params Func<ChatOptions?, ChatResponse>[] script) : IChatClient
{
    private int _idx;
    public List<ChatMessage> Requests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.AddRange(messages);
        var step = script[Math.Min(_idx, script.Length - 1)];
        _idx++;
        return Task.FromResult(step(options));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return GetStreamingImpl(messages, options, cancellationToken);
    }

    private async IAsyncEnumerable<ChatResponseUpdate> GetStreamingImpl(
        IEnumerable<ChatMessage> messages, ChatOptions? options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var response = await GetResponseAsync(messages, options, ct);
        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }
    public void Dispose() { }

    public static Func<ChatOptions?, ChatResponse> Text(string text) => _ =>
        new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
}
