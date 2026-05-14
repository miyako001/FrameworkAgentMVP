using FrameAgentWordFill.Agents;
using FrameAgentWordFill.Models.AIExtraction;
using FrameAgentWordFill.Models.Templates;
using FrameAgentWordFill.Services;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// AI 批量字段提取工具。
/// </summary>
public sealed class AIBatchExtractor
{
    private readonly MultiEngineLLMService _multiEngineLlmService;
    private readonly ILogger<AIBatchExtractor> _logger;

    public AIBatchExtractor(MultiEngineLLMService multiEngineLlmService, ILogger<AIBatchExtractor> logger)
    {
        _multiEngineLlmService = multiEngineLlmService;
        _logger = logger;
    }

    public async Task<(List<AIExtractedField> Fields, string EngineUsed)> ExtractFieldsAsync(
        ParsedDocumentContent documentContent,
        List<Field> templateFields,
        CancellationToken ct = default)
    {
        if (templateFields.Count == 0)
        {
            return (new List<AIExtractedField>(), "LocalRules");
        }

        var fieldList = string.Join('\n', templateFields.Select(f =>
            $"- {f.Name}（类型:{f.FieldType}，{(f.Required ? "必填" : "可选")}）"));

        var prompt = AIExtractionAgentConfig.BatchExtractionPrompt
            .Replace("{DOCUMENT_CONTENT}", BuildDocumentSummary(documentContent, 9000))
            .Replace("{FIELD_LIST}", fieldList);

        var (response, engine) = await _multiEngineLlmService.CallWithFallbackAsync(prompt, ct);

        if (string.IsNullOrWhiteSpace(response))
        {
            return (new List<AIExtractedField>(), engine);
        }

        try
        {
            var jsonText = ExtractJson(response);
            var extractionResponse = JsonSerializer.Deserialize<BatchExtractionResponse>(
                jsonText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            var fields = extractionResponse?.Fields ?? new List<AIExtractedField>();
            foreach (var item in fields)
            {
                item.Confidence = Math.Clamp(item.Confidence, 0, 100);
                if (string.IsNullOrWhiteSpace(item.MatchMethod))
                {
                    item.MatchMethod = "Semantic";
                }
            }

            return (fields, engine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI 提取结果解析失败，返回空结果。Engine={Engine}", engine);
            return (new List<AIExtractedField>(), engine);
        }
    }

    private static string BuildDocumentSummary(ParsedDocumentContent content, int maxLength)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"文件类型: {content.FileType}");
        builder.AppendLine($"解析质量: {content.ParseQuality}");

        if (content.Warnings.Count > 0)
        {
            builder.AppendLine("解析警告:");
            foreach (var warning in content.Warnings.Take(5))
            {
                builder.AppendLine($"- {warning}");
            }
        }

        if (!string.IsNullOrWhiteSpace(content.PlainText))
        {
            builder.AppendLine("正文:");
            builder.AppendLine(content.PlainText);
        }

        if (content.Tables.Count > 0)
        {
            builder.AppendLine("表格:");
            foreach (var table in content.Tables.Take(3))
            {
                builder.AppendLine($"表格{table.TableIndex + 1} 表头: {string.Join(", ", table.Headers)}");
                foreach (var row in table.Rows.Take(10))
                {
                    builder.AppendLine(string.Join(" | ", row));
                }
            }
        }

        var text = builder.ToString();
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength] + "\n\n... [内容已截断] ...";
    }

    private static string ExtractJson(string response)
    {
        var fenced = Regex.Match(response, @"```(?:json)?\s*(\{.*\}|\[.*\])\s*```", RegexOptions.Singleline);
        if (fenced.Success)
        {
            return fenced.Groups[1].Value.Trim();
        }

        var direct = Regex.Match(response, @"(\{.*\}|\[.*\])", RegexOptions.Singleline);
        return direct.Success ? direct.Groups[1].Value.Trim() : response.Trim();
    }

    private sealed class BatchExtractionResponse
    {
        public List<AIExtractedField> Fields { get; set; } = new();
    }
}
