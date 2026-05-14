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

    // 占位符正则表达式（支持标准格式和容错格式）
    // 标准: {字段名}、{表格名.字段名}
    // 容错: 【字段名】、［字段名］、{ 字段名 }
    private static readonly Regex PlaceholderRegex = new(
        @"[\{【［]\s*([a-zA-Z0-9_\u4e00-\u9fa5]+(?:\.[a-zA-Z0-9_\u4e00-\u9fa5]+)?)\s*[\}】］]",
        RegexOptions.Compiled
    );

    public TemplateParser(ILogger<TemplateParser> logger)
    {
        _logger = logger;
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

        for (int i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var text = GetParagraphText(paragraph);

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var matches = PlaceholderRegex.Matches(text);
            foreach (Match match in matches)
            {
                var placeholder = match.Groups[1].Value.Trim();

                // 跳过表格字段（包含点号）
                if (placeholder.Contains('.'))
                    continue;

                // 规范化占位符（去除空格、转换全角符号）
                var normalizedName = NormalizePlaceholder(placeholder, match.Value);
                
                // 检查是否需要规范化警告
                if (match.Value != $"{{{normalizedName}}}")
                {
                    result.Warnings.Add($"第 {i + 1} 段：占位符 '{match.Value}' 已自动规范化为 '{{{normalizedName}}}'");
                }

                // 去重
                if (fieldNames.Contains(normalizedName))
                    continue;

                fieldNames.Add(normalizedName);
                result.Fields.Add(new FieldInfo
                {
                    Name = normalizedName,
                    Type = InferFieldType(normalizedName),
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

        foreach (var table in tables)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0)
                continue;

            // 第一行作为表头
            var headerRow = rows[0];
            var headerCells = headerRow.Elements<TableCell>().ToList();

            var tableInfo = new TableInfo();
            var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int colIndex = 0; colIndex < headerCells.Count; colIndex++)
            {
                var cellText = GetCellText(headerCells[colIndex]);
                var matches = PlaceholderRegex.Matches(cellText);

                foreach (Match match in matches)
                {
                    var placeholder = match.Groups[1].Value.Trim();

                    // 只处理表格字段（格式：表格名.字段名）
                    if (!placeholder.Contains('.'))
                        continue;

                    var parts = placeholder.Split('.');
                    if (parts.Length != 2)
                    {
                        result.Warnings.Add($"表格占位符格式错误: {match.Value}，应为 {{表格名.字段名}}");
                        continue;
                    }

                    var tableName = parts[0].Trim();
                    var columnName = parts[1].Trim();

                    // 设置表格名称（第一次遇到时）
                    if (string.IsNullOrEmpty(tableInfo.Name))
                    {
                        tableInfo.Name = tableName;
                    }
                    else if (tableInfo.Name != tableName)
                    {
                        result.Warnings.Add($"表格中发现不同的表格名: {tableName}，已忽略");
                        continue;
                    }

                    // 避免重复列
                    if (tableNames.Contains(columnName))
                        continue;

                    tableNames.Add(columnName);
                    tableInfo.Columns.Add(new TableColumnInfo
                    {
                        Name = columnName,
                        Type = InferFieldType(columnName),
                        Required = false,
                        Order = colIndex
                    });
                }
            }

            // 只添加有效的表格
            if (!string.IsNullOrEmpty(tableInfo.Name) && tableInfo.Columns.Count > 0)
            {
                result.Tables.Add(tableInfo);
                _logger.LogInformation("提取表格: {TableName}, 列数: {ColumnCount}", 
                    tableInfo.Name, tableInfo.Columns.Count);
            }
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
    /// 规范化占位符（去除空格、转换全角符号）
    /// </summary>
    private static string NormalizePlaceholder(string placeholder, string originalText)
    {
        // 去除前后空格
        var normalized = placeholder.Trim();

        // 替换全角符号（如果原文本包含全角括号）
        if (originalText.Contains('【') || originalText.Contains('】') || 
            originalText.Contains('［') || originalText.Contains('］'))
        {
            normalized = normalized.Replace("【", "").Replace("】", "")
                                   .Replace("［", "").Replace("］", "");
        }

        return normalized;
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
