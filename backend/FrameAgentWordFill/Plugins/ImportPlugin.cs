using System.ComponentModel;
using FrameAgentWordFill.Models.AIExtraction;
using FrameAgentWordFill.Models.Templates;
using FrameAgentWordFill.Tools;
using System.Text.Json;

namespace FrameAgentWordFill.Plugins;

/// <summary>
/// 文件导入场景工具集，由 ImportAgent 按需调用。
/// </summary>
public sealed class ImportPlugin
{
    private readonly AIBatchExtractor _batchExtractor;
    private readonly FieldMatcher _fieldMatcher;

    public ImportPlugin(AIBatchExtractor batchExtractor, FieldMatcher fieldMatcher)
    {
        _batchExtractor = batchExtractor;
        _fieldMatcher = fieldMatcher;
    }

    [Description("从已解析的文档内容中批量提取字段值。输入文档摘要与模板字段列表，返回 AI 提取结果 JSON，含 fieldName/fieldValue/confidence/matchMethod。")]
    public async Task<string> BatchExtractFieldsAsync(
        [Description("已解析的文档内容，JSON 格式（ParsedDocumentContent）")] string documentContentJson,
        [Description("模板字段列表，JSON 数组（Field[]）")] string templateFieldsJson)
    {
        var documentContent = JsonSerializer.Deserialize<ParsedDocumentContent>(documentContentJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var templateFields = JsonSerializer.Deserialize<List<Field>>(templateFieldsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<Field>();

        if (documentContent == null)
            return JsonSerializer.Serialize(new { error = "文档内容解析失败" });

        var (fields, engine) = await _batchExtractor.ExtractFieldsAsync(documentContent, templateFields);
        return JsonSerializer.Serialize(new { fields, engine });
    }

    [Description("对 AI 提取结果与模板字段进行语义匹配，处理字段别名、同义词和近义映射。返回匹配后的字段赋值建议。")]
    public Task<string> MatchExtractedFieldsAsync(
        [Description("AI 提取结果 JSON")] string extractedFieldsJson,
        [Description("模板字段列表 JSON")] string templateFieldsJson)
    {
        var extracted = JsonSerializer.Deserialize<List<AIExtractedField>>(
            extractedFieldsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new();
        var templateFields = JsonSerializer.Deserialize<List<Field>>(
            templateFieldsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new();

        var result = _fieldMatcher.MatchAIExtractedFields(extracted, templateFields);
        return Task.FromResult(JsonSerializer.Serialize(result));
    }
}
