using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FrameAgentWordFill.Models.Parsing;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 模板解析工具（提取占位符和表格结构）
/// </summary>
public sealed class TemplateParser
{
    private readonly ILogger<TemplateParser> _logger;
    private readonly TemplatePlaceholderNormalizer _normalizer;

    // 支持：{}、{{}}、[]、()，并兼容全角括号。
    private static readonly Regex PlaceholderRegex = new(
        @"(\{\{\s*[^{}]+?\s*\}\}|\{\s*[^{}]+?\s*\}|\[\s*[^\[\]]+?\s*\]|\(\s*[^()]+?\s*\)|【\s*[^】]+?\s*】|［\s*[^］]+?\s*］)",
        RegexOptions.Compiled
    );

    public TemplateParser(ILogger<TemplateParser> logger, TemplatePlaceholderNormalizer normalizer)
    {
        _logger = logger;
        _normalizer = normalizer;
    }

    /// <summary>
    /// 解析 Word 模板文件
    /// </summary>
    /// <param name="filePath">模板文件路径</param>
    /// <returns>解析结果</returns>
    public async Task<TemplateParseResult> ParseTemplateAsync(string filePath)
    {
        var result = new TemplateParseResult();

        try
        {
            await Task.Run(() =>
            {
                using var document = WordprocessingDocument.Open(filePath, false);
                if (document.MainDocumentPart == null)
                {
                    result.Errors.Add("无法打开文档：MainDocumentPart 为空");
                    result.Success = false;
                    return;
                }

                var body = document.MainDocumentPart.Document.Body;
                if (body == null)
                {
                    result.Errors.Add("文档内容为空");
                    result.Success = false;
                    return;
                }

                // 提取普通字段（从段落文本中）
                ExtractFieldsFromParagraphs(body, result);

                // 提取表格字段（从表格中）
                ExtractFieldsFromTables(body, result);

                result.Success = result.Errors.Count == 0;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析模板文件失败: {FilePath}", filePath);
            result.Errors.Add($"解析失败: {ex.Message}");
            result.Success = false;
        }

        return result;
    }

    /// <summary>
    /// 从段落中提取普通字段
    /// </summary>
    private void ExtractFieldsFromParagraphs(Body body, TemplateParseResult result)
    {
        var paragraphs = body.Elements<Paragraph>().ToList();
        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tableColumnKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var text = GetParagraphText(paragraph);

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var placeholders = NormalizePlaceholders(text, $"第 {i + 1} 段", result);
            foreach (var placeholder in placeholders)
            {
                if (placeholder.Contains('.'))
                {
                    AddTableFieldCandidate(placeholder, result, tableColumnKeys);
                    continue;
                }

                // 去重
                if (fieldNames.Contains(placeholder))
                    continue;

                fieldNames.Add(placeholder);
                result.Fields.Add(new FieldInfo
                {
                    Name = placeholder,
                    Type = InferFieldType(placeholder),
                    Required = false,
                    Position = i
                });
            }
        }

        _logger.LogInformation("提取普通字段: {Count} 个", result.Fields.Count);
    }

    /// <summary>
    /// 从表格中提取表格字段
    /// </summary>
    private void ExtractFieldsFromTables(Body body, TemplateParseResult result)
    {
        var tables = body.Elements<Table>().ToList();
        var tableColumnKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            var table = tables[tableIndex];
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0)
                continue;

            // 第一行作为表头
            var headerRow = rows[0];
            var headerCells = headerRow.Elements<TableCell>().ToList();

            for (int colIndex = 0; colIndex < headerCells.Count; colIndex++)
            {
                var cellText = GetCellText(headerCells[colIndex]);
                var placeholders = NormalizePlaceholders(cellText, $"第 {tableIndex + 1} 个表格第 {colIndex + 1} 列", result);

                foreach (var placeholder in placeholders)
                {
                    if (!placeholder.Contains('.'))
                        continue;

                    AddTableFieldCandidate(placeholder, result, tableColumnKeys, colIndex);
                }
            }
        }

        foreach (var tableInfo in result.Tables)
        {
            _logger.LogInformation("提取表格: {TableName}, 列数: {ColumnCount}",
                tableInfo.Name,
                tableInfo.Columns.Count);
        }
    }

    /// <summary>
    /// 获取段落文本
    /// </summary>
    private static string GetParagraphText(Paragraph paragraph)
    {
        return paragraph.InnerText;
    }

    /// <summary>
    /// 获取单元格文本
    /// </summary>
    private static string GetCellText(TableCell cell)
    {
        return cell.InnerText;
    }

    /// <summary>
    /// 将文本中的占位符规范化为统一 token。
    /// </summary>
    private List<string> NormalizePlaceholders(string text, string location, TemplateParseResult result)
    {
        var output = new List<string>();
        var matches = PlaceholderRegex.Matches(text);

        foreach (Match match in matches)
        {
            var raw = match.Value;
            var normalized = _normalizer.Normalize(raw);
            if (!normalized.Success)
            {
                if (!string.IsNullOrWhiteSpace(normalized.Error))
                {
                    result.Warnings.Add($"{location}：{normalized.Error}");
                }
                continue;
            }

            if (!string.IsNullOrWhiteSpace(normalized.Warning))
            {
                result.Warnings.Add($"{location}：{normalized.Warning}");
            }

            output.Add(normalized.NormalizedToken);
        }

        return output;
    }

    /// <summary>
    /// 将点号字段加入表格候选（至少两段，例如 A.B 或 A.B.C）。
    /// </summary>
    private void AddTableFieldCandidate(
        string normalizedToken,
        TemplateParseResult result,
        HashSet<string> tableColumnKeys,
        int order = 0)
    {
        var parts = normalizedToken
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 2)
        {
            result.Warnings.Add($"点号字段格式无效: {{{normalizedToken}}}，应至少包含两段");
            return;
        }

        var tableName = parts[0];
        var columnName = string.Join('.', parts.Skip(1));
        var key = $"{tableName}.{columnName}";

        if (!tableColumnKeys.Add(key))
        {
            return;
        }

        var table = result.Tables.FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));
        if (table == null)
        {
            table = new TableInfo
            {
                Name = tableName
            };
            result.Tables.Add(table);
        }

        table.Columns.Add(new TableColumnInfo
        {
            Name = columnName,
            Type = InferFieldType(columnName),
            Required = false,
            Order = table.Columns.Count > 0 ? table.Columns.Max(c => c.Order) + 1 : order
        });
    }

    /// <summary>
    /// 推断字段类型（基于字段名称）
    /// </summary>
    private static string InferFieldType(string fieldName)
    {
        var name = fieldName.ToLower();

        if (name.Contains("电话") || name.Contains("手机") || name.Contains("phone"))
            return "phone";

        if (name.Contains("邮箱") || name.Contains("email") || name.Contains("mail"))
            return "email";

        if (name.Contains("日期") || name.Contains("时间") || name.Contains("date"))
            return "date";

        if (name.Contains("金额") || name.Contains("数量") || name.Contains("预算") || 
            name.Contains("number") || name.Contains("amount"))
            return "number";

        return "text";
    }
}
