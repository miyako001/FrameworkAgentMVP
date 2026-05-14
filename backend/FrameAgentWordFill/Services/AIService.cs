using GitHub.Copilot.SDK;
using System.Text;
using System.Text.Json;

namespace FrameAgentWordFill.Services;

public sealed class AIService : IAsyncDisposable
{
    private readonly CopilotClient? _copilotClient;
    private readonly ILogger<AIService> _logger;
    private readonly string _model;
    private readonly TimeSpan _requestTimeout;
    private readonly bool _isInitialized;

    public AIService(IConfiguration configuration, ILogger<AIService> logger)
    {
        _logger = logger;
        _model = configuration["GitHubCopilot:Model"] ?? string.Empty;
        _requestTimeout = TimeSpan.FromSeconds(
            configuration.GetValue<int?>("GitHubCopilot:RequestTimeoutSeconds") ?? 90
        );
        
        try
        {
            // 尝试创建 GitHub Copilot 客户端
            var copilotOptions = new CopilotClientOptions
            {
                AutoStart = true,
                LogLevel = "info"
            };
            
            _copilotClient = new CopilotClient(copilotOptions);
            _copilotClient.StartAsync().GetAwaiter().GetResult();
            _isInitialized = true;
            
            var modelForLog = string.IsNullOrWhiteSpace(_model) ? "auto(default)" : _model;
            _logger.LogInformation("AIService initialized with GitHub Copilot SDK (Model: {Model})", modelForLog);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize GitHub Copilot SDK. AI features will use fallback mode.");
            _isInitialized = false;
        }
    }

    public async Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isInitialized || _copilotClient == null)
            {
                _logger.LogWarning("Copilot client is not initialized");
                throw new InvalidOperationException(
                    "GitHub Copilot SDK 未初始化。请确认已安装并登录 GitHub CLI/Copilot，并重启后端服务。"
                );
            }

            var authStatus = await _copilotClient.GetAuthStatusAsync(cancellationToken);
            var authStatusJson = JsonSerializer.Serialize(authStatus);
            if (authStatusJson.Contains("\"signedIn\":false", StringComparison.OrdinalIgnoreCase) ||
                authStatusJson.Contains("\"authenticated\":false", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Copilot auth status indicates unauthenticated: {AuthStatus}", authStatusJson);
                throw new InvalidOperationException("GitHub Copilot 未登录或无授权。请先执行 gh auth login，并确认 Copilot 权限已开通。");
            }

            await using var session = await _copilotClient.CreateSessionAsync(
                BuildSessionConfig(_model),
                cancellationToken
            );

            AssistantMessageEvent? finalEvent;
            try
            {
                finalEvent = await session.SendAndWaitAsync(
                    new MessageOptions { Prompt = prompt },
                    _requestTimeout,
                    cancellationToken
                );
            }
            catch
            {
                throw;
            }

            var text = ExtractAssistantText(finalEvent);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            throw new InvalidOperationException("LLM 返回为空，请检查模型可用性和账号权限。");
        }
        catch (Exception ex) when (IsModelUnavailableException(ex))
        {
            if (_copilotClient == null)
            {
                throw;
            }

            _logger.LogWarning(
                ex,
                "Configured model {Model} is unavailable. Retrying with Copilot default model.",
                _model
            );

            await using var fallbackSession = await _copilotClient.CreateSessionAsync(
                BuildSessionConfig(null),
                cancellationToken
            );

            var finalEvent = await fallbackSession.SendAndWaitAsync(
                new MessageOptions { Prompt = prompt },
                _requestTimeout,
                cancellationToken
            );

            var text = ExtractAssistantText(finalEvent);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("LLM 返回为空，请检查 Copilot 账号权限和可用模型。");
            }

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AI completion");
            throw;
        }
    }

    public async Task<string> TestConnectionAsync()
    {
        return await GetCompletionAsync("请回复：你好，FrameAgent！并附带当前模型名称。", CancellationToken.None);
    }

    private static SessionConfig BuildSessionConfig(string? model)
    {
        var config = new SessionConfig
        {
            Streaming = false,
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        if (!string.IsNullOrWhiteSpace(model))
        {
            config.Model = model;
        }

        return config;
    }

    private bool IsModelUnavailableException(Exception ex)
    {
        if (string.IsNullOrWhiteSpace(_model))
        {
            return false;
        }

        var errorText = ex.ToString();
        return errorText.Contains("not available", StringComparison.OrdinalIgnoreCase)
            && errorText.Contains(_model, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractAssistantText(AssistantMessageEvent? finalEvent)
    {
        if (finalEvent?.Data == null)
        {
            return string.Empty;
        }

        // SDK payload has evolved quickly in beta versions; serialize then extract all text-like fields defensively.
        var dataJson = JsonSerializer.Serialize(finalEvent.Data);
        using var doc = JsonDocument.Parse(dataJson);
        var builder = new StringBuilder();
        CollectText(doc.RootElement, builder);

        return builder.ToString().Trim();
    }

    private static void CollectText(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        if (property.NameEquals("text") ||
                            property.NameEquals("content") ||
                            property.NameEquals("reasoningText") ||
                            property.NameEquals("result"))
                        {
                            var value = property.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                builder.AppendLine(value);
                            }
                        }
                    }

                    CollectText(property.Value, builder);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectText(item, builder);
                }
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_copilotClient != null)
        {
            await _copilotClient.StopAsync();
        }
    }
}
