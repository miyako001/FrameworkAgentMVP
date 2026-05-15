using Microsoft.Extensions.AI;

namespace FrameAgentWordFill.Services;

/// <summary>
/// 将 MultiEngineLLMService 包装为标准 IChatClient。
/// 降级顺序：Copilot SDK → Azure OpenAI → OpenAI → 本地规则。
/// </summary>
public sealed class FallbackChatClient : IChatClient
{
    private readonly MultiEngineLLMService _multiEngine;
    private readonly ILogger<FallbackChatClient> _logger;

    public FallbackChatClient(MultiEngineLLMService multiEngine, ILogger<FallbackChatClient> logger)
    {
        _multiEngine = multiEngine;
        _logger = logger;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 将 messages 拼合为单一 prompt（保留历史上下文）
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var prompt = BuildPromptFromMessages(messageList);
        var (response, engine) = await _multiEngine.CallWithFallbackAsync(prompt, cancellationToken);

        _logger.LogDebug("FallbackChatClient 使用引擎: {Engine}", engine);

        return new ChatResponse(
            new ChatMessage(ChatRole.Assistant, response)
        );
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("流式输出暂不支持，请使用 GetResponseAsync。");

    public ChatClientMetadata Metadata => new("FallbackChatClient", null, null);
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    private static string BuildPromptFromMessages(IEnumerable<ChatMessage> messages)
    {
        // system prompt 置顶，其余按顺序追加
        var parts = messages.Select(m => $"[{m.Role}]\n{m.Text}");
        return string.Join("\n\n", parts);
    }
}
