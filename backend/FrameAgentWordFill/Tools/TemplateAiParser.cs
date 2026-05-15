using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FrameAgentWordFill.Models.Parsing;
using FrameAgentWordFill.Services;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 模板 AI 语义解析器：用于与本地规则做差异比对。
/// </summary>
public sealed class TemplateAiParser
{
    private readonly MultiEngineLLMService _llmService;
    private readonly TemplatePlaceholderNormalizer _normalizer;
    private readonly ILogger<TemplateAiParser> _logger;

    public TemplateAiParser(
        MultiEngineLLMService llmService,
        TemplatePlaceholderNormalizer normalizer,
        ILogger<TemplateAiParser> logger)
    {
        _llmService = llmService;
        _normalizer = normalizer;
        _logger = logger;
    }

    public async Task<TemplateAiParseResult> ParseTemplateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = new TemplateAiParseResult();

        try
        {
            var templateText = ExtractTemplateText(filePath);
            if (string.IsNullOrWhiteSpace(templateText))
            {
                result.Warnings.Add("AI 比对已降级：模板文本为空，无法执行语义解析");
                return result;
            }

            var prompt = BuildPrompt(templateText);
            var (response, engine) = await _llmService.CallWithFallbackAsync(prompt, cancellationToken);
            result.Engine = engine;

            if (string.IsNullOrWhiteSpace(response) || string.Equals(engine, "LocalRules", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add("AI 比对已降级：当前无可用 AI 引擎，已仅使用本地解析");
                return result;
            }

            var json = ExtractJson(response);
            var parsed = JsonSerializer.Deserialize<AiTemplateContract>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (parsed == null)
            {
                result.Warnings.Add("AI 比对已降级：AI 返回为空，已仅使用本地解析");
                return result;
            }

            result.Fields = NormalizeFields(parsed.Fields);
            result.Tables = NormalizeTables(parsed.Tables);
            result.Success = true;
            result.Available = true;

            _logger.LogInformation("AI 解析完成，引擎: {Engine}, 字段: {FieldCount}, 表格: {TableCount}",
                result.Engine,
                result.Fields.Count,
                result.Tables.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI 模板解析失败，已降级到本地规则");
            result.Warnings.Add("AI 比对已降级：AI 调用异常或返回不可解析 JSON，已仅使用本地解析");
        }

        return result;
    }

    private List<string> NormalizeFields(List<string>? rawFields)
    {
        var output = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rawFields == null)
        {
            return output.ToList();
        }

        foreach (var field in rawFields)
        {
            var token = field?.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (!(token.StartsWith("{", StringComparison.Ordinal) || token.StartsWith("[", StringComparison.Ordinal) || token.StartsWith("(", StringComparison.Ordinal)))
            {
                token = $"{{{token}}}";
            }

            var normalized = _normalizer.Normalize(token);
            if (normalized.Success)
            {
                output.Add(normalized.NormalizedToken);
            }
        }

        return output.ToList();
    }

    private List<AiTableCandidate> NormalizeTables(List<AiTableCandidate>? rawTables)
    {
        var tableMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        if (rawTables == null)
        {
            return new List<AiTableCandidate>();
        }

        foreach (var table in rawTables)
        {
            if (string.IsNullOrWhiteSpace(table.Name))
            {
                continue;
            }

            var name = table.Name.Trim();
            if (!tableMap.TryGetValue(name, out var columns))
            {
                columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                tableMap[name] = columns;
            }

            foreach (var column in table.Columns)
            {
                var value = column?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    columns.Add(value);
                }
            }
        }

        return tableMap.Select(kvp => new AiTableCandidate
        {
            Name = kvp.Key,
            Columns = kvp.Value.ToList()
        }).ToList();
    }

    private static string BuildPrompt(string templateText)
    {
                return "你是模板占位符解析助手。请从下面模板文本中提取字段和表格列，并严格按 JSON 返回。\n\n"
                        + "提取规则：\n"
                        + "1) 普通字段放入 fields。\n"
                        + "2) 点号字段（如 A.B 或 A.B.C）放入 tables，table 名是第一段，column 为后续段拼接。\n"
                        + "3) 忽略明显不是占位符的自然语言。\n"
                        + "4) 只返回 JSON，不要输出任何解释。\n\n"
                        + "JSON 格式：\n"
                        + "{\n"
                        + "  \"fields\": [\"项目名称\", \"负责人\"],\n"
                        + "  \"tables\": [\n"
                        + "    { \"name\": \"成员表\", \"columns\": [\"姓名\", \"职位\"] }\n"
                        + "  ]\n"
                        + "}\n\n"
                        + "模板文本：\n"
                        + templateText;
    }

    private static string ExtractTemplateText(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        if (document.MainDocumentPart?.Document.Body == null)
        {
            return string.Empty;
        }

        var body = document.MainDocumentPart.Document.Body;
        var builder = new StringBuilder();

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            var text = paragraph.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text);
            }
        }

        foreach (var table in body.Elements<Table>())
        {
            var header = table.Elements<TableRow>().FirstOrDefault();
            if (header == null)
            {
                continue;
            }

            var cells = header.Elements<TableCell>()
                .Select(c => c.InnerText?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t));

            var rowText = string.Join(" | ", cells!);
            if (!string.IsNullOrWhiteSpace(rowText))
            {
                builder.AppendLine(rowText);
            }
        }

        return builder.ToString();
    }

    private static string ExtractJson(string response)
    {
        var markdownMatch = Regex.Match(
            response,
            @"```(?:json)?\s*(\{.*?\})\s*```",
            RegexOptions.Singleline
        );

        if (markdownMatch.Success)
        {
            return markdownMatch.Groups[1].Value.Trim();
        }

        var directMatch = Regex.Match(response, @"\{.*\}", RegexOptions.Singleline);
        if (directMatch.Success)
        {
            return directMatch.Value.Trim();
        }

        return response.Trim();
    }

    private sealed class AiTemplateContract
    {
        public List<string> Fields { get; set; } = new();

        public List<AiTableCandidate> Tables { get; set; } = new();
    }
}
