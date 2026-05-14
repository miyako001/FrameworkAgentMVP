using FrameAgentWordFill.Models.AIExtraction;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// 图片 OCR 解析器占位实现。
/// </summary>
public sealed class ImageOcrParser
{
    private readonly ILogger<ImageOcrParser> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ImageOcrParser(
        ILogger<ImageOcrParser> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<ParsedDocumentContent> ParseAsync(string filePath)
    {
        var result = new ParsedDocumentContent { FileType = "Image" };
        var endpoint = _configuration["AzureVision:Endpoint"];
        var apiKey = _configuration["AzureVision:ApiKey"];
        var language = _configuration["AzureVision:Language"] ?? "zh-Hans";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("图片 OCR 未配置 AzureVision，文件 {Path} 将走降级路径", filePath);
            result.ParseQuality = 0;
            result.Warnings.Add("当前环境未配置 Azure Vision OCR，图片文本无法自动识别");
            result.Warnings.Add("请在 appsettings 中配置 AzureVision:Endpoint 和 AzureVision:ApiKey");
            return result;
        }

        if (!File.Exists(filePath))
        {
            result.ParseQuality = 0;
            result.Warnings.Add("图片文件不存在");
            return result;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{endpoint.TrimEnd('/')}/vision/v3.2/ocr?language={language}&detectOrientation=true";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

            var bytes = await File.ReadAllBytesAsync(filePath);
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Azure OCR 请求失败: {Status}, {Body}", response.StatusCode, errorBody);
                result.ParseQuality = 10;
                result.Warnings.Add($"Azure OCR 调用失败: {(int)response.StatusCode}");
                return result;
            }

            var json = await response.Content.ReadAsStringAsync();
            result.PlainText = ParseOcrText(json);

            if (string.IsNullOrWhiteSpace(result.PlainText))
            {
                result.ParseQuality = 30;
                result.Warnings.Add("OCR 未识别到文本");
            }
            else
            {
                result.ParseQuality = 80;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图片 OCR 解析失败");
            result.ParseQuality = 10;
            result.Warnings.Add($"图片 OCR 解析失败：{ex.Message}");
            return result;
        }
    }

    private static string ParseOcrText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("regions", out var regions) || regions.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach (var region in regions.EnumerateArray())
        {
            if (!region.TryGetProperty("lines", out var lineArray) || lineArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var line in lineArray.EnumerateArray())
            {
                if (!line.TryGetProperty("words", out var words) || words.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var wordTexts = new List<string>();
                foreach (var word in words.EnumerateArray())
                {
                    if (word.TryGetProperty("text", out var textNode))
                    {
                        var text = textNode.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            wordTexts.Add(text);
                        }
                    }
                }

                if (wordTexts.Count > 0)
                {
                    lines.Add(string.Join(" ", wordTexts));
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
