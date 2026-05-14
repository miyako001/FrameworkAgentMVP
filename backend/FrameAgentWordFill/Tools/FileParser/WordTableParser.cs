using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FrameAgentWordFill.Models.Import;
using FrameAgentWordFill.Models.AIExtraction;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// Word 文件解析器（提取表格数据）
/// </summary>
public sealed class WordTableParser
{
    private readonly ILogger<WordTableParser> _logger;

    public WordTableParser(ILogger<WordTableParser> logger)
    {
        _logger = logger;
    }

    public async Task<ParsedFileData> ParseAsync(string filePath)
    {
        var result = new ParsedFileData { FileType = "Word" };

        try
        {
            await Task.Run(() =>
            {
                using var doc = WordprocessingDocument.Open(filePath, false);
                var body = doc.MainDocumentPart?.Document.Body;

                if (body == null)
                {
                    result.Errors.Add("无法读取 Word 文档内容");
                    return;
                }

                var tables = body.Elements<Table>().ToList();

                if (tables.Count == 0)
                {
                    result.Warnings.Add("Word 文档中未找到表格");
                    return;
                }

                for (int i = 0; i < tables.Count; i++)
                {
                    var tableName = $"表格{i + 1}";
                    var tableData = ParseSingleTable(tables[i]);
                    if (tableData.Count > 0)
                        result.Tables[tableName] = tableData;
                }

                _logger.LogInformation("Word 解析完成：提取到 {TableCount} 个表格", result.Tables.Count);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Word 解析失败");
            result.Errors.Add($"解析失败：{ex.Message}");
        }

        return result;
    }

    private List<Dictionary<string, string>> ParseSingleTable(Table table)
    {
        var tableData = new List<Dictionary<string, string>>();
        var rows = table.Elements<TableRow>().ToList();

        if (rows.Count < 2)
            return tableData;

        var headers = rows[0].Elements<TableCell>()
            .Select(cell => cell.InnerText.Trim())
            .ToList();

        for (int i = 1; i < rows.Count; i++)
        {
            var cells = rows[i].Elements<TableCell>().ToList();
            var rowData = new Dictionary<string, string>();

            for (int j = 0; j < Math.Min(headers.Count, cells.Count); j++)
            {
                var columnName = headers[j];
                if (!string.IsNullOrWhiteSpace(columnName))
                    rowData[columnName] = cells[j].InnerText.Trim();
            }

            if (rowData.Count > 0)
                tableData.Add(rowData);
        }

        return tableData;
    }

    /// <summary>
    /// 提取 Word 全量内容，供 W7 AI 提取链路使用。
    /// </summary>
    public async Task<ParsedDocumentContent> ParseFullContentAsync(string filePath)
    {
        var result = new ParsedDocumentContent { FileType = "Word" };

        try
        {
            await Task.Run(() =>
            {
                using var doc = WordprocessingDocument.Open(filePath, false);
                var body = doc.MainDocumentPart?.Document.Body;

                if (body == null)
                {
                    result.ParseQuality = 20;
                    result.Warnings.Add("无法读取 Word 主文档内容");
                    return;
                }

                result.PlainText = body.InnerText;

                var tables = body.Elements<Table>().ToList();
                for (var i = 0; i < tables.Count; i++)
                {
                    var tableRows = ParseSingleTable(tables[i]);
                    if (tableRows.Count == 0)
                    {
                        continue;
                    }

                    var headers = tableRows.SelectMany(x => x.Keys).Distinct().ToList();
                    result.Tables.Add(new ParsedTable
                    {
                        TableIndex = i,
                        Headers = headers,
                        Rows = tableRows.Select(row => headers.Select(h => row.GetValueOrDefault(h, string.Empty)).ToList()).ToList()
                    });
                }

                if (string.IsNullOrWhiteSpace(result.PlainText) && result.Tables.Count == 0)
                {
                    result.ParseQuality = 50;
                    result.Warnings.Add("Word 文档内容较少，可能影响 AI 提取结果");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Word 全量解析失败");
            result.ParseQuality = 20;
            result.Warnings.Add($"Word 全量解析失败：{ex.Message}");
        }

        return result;
    }
}
