using FrameAgentWordFill.Models.Import;
using FrameAgentWordFill.Models.AIExtraction;
using FrameAgentWordFill.Models.Templates;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 字段智能匹配器（精确匹配 → 模糊匹配 → 语义匹配）
/// </summary>
public sealed class FieldMatcher
{
    private readonly ILogger<FieldMatcher> _logger;

    private readonly Dictionary<string, List<string>> _synonyms = new()
    {
        { "项目名称", new List<string> { "项目名", "名称", "项目", "project", "name" } },
        { "负责人", new List<string> { "负责人姓名", "项目负责人", "主管", "经理", "manager", "leader" } },
        { "电话", new List<string> { "电话号码", "手机", "联系方式", "联系电话", "phone", "tel", "mobile" } },
        { "邮箱", new List<string> { "电子邮箱", "邮件", "email", "mail", "e-mail" } },
        { "日期", new List<string> { "时间", "日期时间", "date", "time", "datetime" } },
        { "金额", new List<string> { "费用", "预算", "价格", "总额", "amount", "price", "budget" } },
        { "备注", new List<string> { "说明", "描述", "备注信息", "remark", "note", "description" } }
    };

    public FieldMatcher(ILogger<FieldMatcher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 匹配源字段到模板字段
    /// </summary>
    public List<ImportFieldMapping> MatchFields(
        Dictionary<string, string> sourceFields,
        List<Field> templateFields)
    {
        var mappings = new List<ImportFieldMapping>();
        var templateFieldNames = templateFields.Select(f => f.Name).ToList();

        foreach (var (sourceFieldName, fieldValue) in sourceFields)
        {
            var mapping = new ImportFieldMapping
            {
                SourceFieldName = sourceFieldName,
                FieldValue = fieldValue,
                FieldType = "Normal"
            };

            var exactMatch = ExactMatch(sourceFieldName, templateFieldNames);
            if (exactMatch != null)
            {
                mapping.TemplateFieldName = exactMatch;
                mapping.MatchConfidence = 100;
                mapping.MatchMethod = "Exact";
                mappings.Add(mapping);
                continue;
            }

            var fuzzyMatch = FuzzyMatch(sourceFieldName, templateFieldNames);
            if (fuzzyMatch.Match != null && fuzzyMatch.Confidence >= 70)
            {
                mapping.TemplateFieldName = fuzzyMatch.Match;
                mapping.MatchConfidence = fuzzyMatch.Confidence;
                mapping.MatchMethod = "Fuzzy";
                mappings.Add(mapping);
                continue;
            }

            var semanticMatch = SemanticMatch(sourceFieldName, templateFieldNames);
            if (semanticMatch.Match != null && semanticMatch.Confidence >= 60)
            {
                mapping.TemplateFieldName = semanticMatch.Match;
                mapping.MatchConfidence = semanticMatch.Confidence;
                mapping.MatchMethod = "Semantic";
                mappings.Add(mapping);
                continue;
            }

            mapping.MatchConfidence = 0;
            mapping.MatchMethod = "Manual";
            mappings.Add(mapping);
        }

        _logger.LogInformation("字段匹配完成：{HighCount} 个高置信度匹配",
            mappings.Count(m => m.MatchConfidence >= 70));

        return mappings;
    }

    /// <summary>
    /// 匹配 AI 批量提取结果到模板字段。
    /// </summary>
    public List<ImportFieldMapping> MatchAIExtractedFields(
        List<AIExtractedField> aiExtractedFields,
        List<Field> templateFields)
    {
        var mappings = new List<ImportFieldMapping>();
        var templateFieldNames = templateFields.Select(f => f.Name).ToList();

        foreach (var aiField in aiExtractedFields)
        {
            var mapping = new ImportFieldMapping
            {
                SourceFieldName = aiField.FieldName,
                FieldValue = aiField.FieldValue,
                FieldType = "Normal",
                MatchConfidence = Math.Clamp(aiField.Confidence, 0, 100),
                MatchMethod = string.IsNullOrWhiteSpace(aiField.MatchMethod) ? "Semantic" : aiField.MatchMethod
            };

            var exactMatch = ExactMatch(aiField.FieldName, templateFieldNames);
            if (exactMatch != null)
            {
                mapping.TemplateFieldName = exactMatch;
                mapping.MatchMethod = "Exact";
                mapping.MatchConfidence = Math.Max(mapping.MatchConfidence, 95);
                mappings.Add(mapping);
                continue;
            }

            var fuzzyMatch = FuzzyMatch(aiField.FieldName, templateFieldNames);
            if (fuzzyMatch.Match != null && fuzzyMatch.Confidence >= 70)
            {
                mapping.TemplateFieldName = fuzzyMatch.Match;
                mapping.MatchMethod = "Fuzzy";
                mapping.MatchConfidence = (mapping.MatchConfidence + fuzzyMatch.Confidence) / 2;
                mappings.Add(mapping);
                continue;
            }

            var semanticMatch = SemanticMatch(aiField.FieldName, templateFieldNames);
            if (semanticMatch.Match != null && semanticMatch.Confidence >= 60)
            {
                mapping.TemplateFieldName = semanticMatch.Match;
                mapping.MatchMethod = "Semantic";
                mapping.MatchConfidence = (mapping.MatchConfidence + semanticMatch.Confidence) / 2;
                mappings.Add(mapping);
                continue;
            }

            mapping.MatchMethod = "NoMatch";
            mapping.MatchConfidence = Math.Min(mapping.MatchConfidence, 50);
            mappings.Add(mapping);
        }

        _logger.LogInformation("AI 字段匹配完成：{HighCount} 个高置信度匹配",
            mappings.Count(m => m.MatchConfidence >= 70));

        return mappings;
    }

    private string? ExactMatch(string sourceFieldName, List<string> templateFieldNames)
    {
        var normalized = Normalize(sourceFieldName);

        var direct = templateFieldNames.FirstOrDefault(f =>
            Normalize(f).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (direct != null) return direct;

        foreach (var (key, synonymList) in _synonyms)
        {
            if (synonymList.Any(s => s.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                var match = templateFieldNames.FirstOrDefault(f =>
                    Normalize(f).Equals(Normalize(key), StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
        }

        return null;
    }

    private (string? Match, int Confidence) FuzzyMatch(string sourceFieldName, List<string> templateFieldNames)
    {
        var normalized = Normalize(sourceFieldName);
        string? bestMatch = null;
        int maxSimilarity = 0;

        foreach (var fieldName in templateFieldNames)
        {
            var similarity = CalculateSimilarity(normalized, Normalize(fieldName));
            if (similarity > maxSimilarity)
            {
                maxSimilarity = similarity;
                bestMatch = fieldName;
            }
        }

        return (bestMatch, maxSimilarity);
    }

    private (string? Match, int Confidence) SemanticMatch(string sourceFieldName, List<string> templateFieldNames)
    {
        var normalized = Normalize(sourceFieldName);

        foreach (var (key, synonymList) in _synonyms)
        {
            foreach (var synonym in synonymList)
            {
                if (normalized.Contains(synonym, StringComparison.OrdinalIgnoreCase) ||
                    synonym.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    var match = templateFieldNames.FirstOrDefault(f =>
                        Normalize(f).Contains(Normalize(key), StringComparison.OrdinalIgnoreCase));
                    if (match != null) return (match, 75);
                }
            }
        }

        return (null, 0);
    }

    private string Normalize(string s) =>
        Regex.Replace(s, @"[\s\-_、\.\(\)]", "").ToLower();

    private int CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0;
        var distance = LevenshteinDistance(s1, s2);
        var maxLength = Math.Max(s1.Length, s2.Length);
        return Math.Max(0, (int)((1.0 - (double)distance / maxLength) * 100));
    }

    private int LevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];
        for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }

        return matrix[s1.Length, s2.Length];
    }
}
