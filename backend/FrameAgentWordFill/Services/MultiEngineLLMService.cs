using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FrameAgentWordFill.Services;

/// <summary>
/// 多引擎 LLM 调用服务：Copilot -> Azure OpenAI -> OpenAI。
/// </summary>
public sealed class MultiEngineLLMService
{
    private readonly AIService _aiService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MultiEngineLLMService> _logger;

    public MultiEngineLLMService(
        AIService aiService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MultiEngineLLMService> logger)
    {
        _aiService = aiService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(string Response, string Engine)> CallWithFallbackAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            var copilot = await _aiService.GetCompletionAsync(prompt, ct);
            if (!string.IsNullOrWhiteSpace(copilot))
            {
                return (copilot, "CopilotSDK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Copilot 引擎不可用，准备降级到 Azure OpenAI");
        }

        try
        {
            var azureResponse = await CallAzureOpenAiAsync(prompt, ct);
            if (!string.IsNullOrWhiteSpace(azureResponse))
            {
                return (azureResponse, "AzureOpenAI");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI 引擎不可用，准备降级到 OpenAI");
        }

        try
        {
            var openAiResponse = await CallOpenAiAsync(prompt, ct);
            if (!string.IsNullOrWhiteSpace(openAiResponse))
            {
                return (openAiResponse, "OpenAI");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI 引擎不可用，降级到本地规则");
        }

        return (string.Empty, "LocalRules");
    }

    private async Task<string> CallAzureOpenAiAsync(string prompt, CancellationToken ct)
    {
        var endpoint = _configuration["AzureOpenAI:Endpoint"];
        var apiKey = _configuration["AzureOpenAI:ApiKey"];
        var deployment = _configuration["AzureOpenAI:DeploymentName"];
        var apiVersion = _configuration["AzureOpenAI:ApiVersion"] ?? "2024-06-01";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
        {
            throw new InvalidOperationException("Azure OpenAI 配置不完整");
        }

        var client = _httpClientFactory.CreateClient();
        var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("api-key", apiKey);

        var payload = new
        {
            messages = new[]
            {
                new { role = "system", content = "You are a precise extractor." },
                new { role = "user", content = prompt }
            },
            temperature = 0.1
        };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private async Task<string> CallOpenAiAsync(string prompt, CancellationToken ct)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        var model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        var baseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API Key 未配置");
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "You are a precise extractor." },
                new { role = "user", content = prompt }
            },
            temperature = 0.1
        };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
