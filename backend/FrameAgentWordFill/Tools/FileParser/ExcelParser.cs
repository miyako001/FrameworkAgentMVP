using OfficeOpenXml;
using FrameAgentWordFill.Models.Import;
using FrameAgentWordFill.Models.AIExtraction;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// Excel 文件解析器（支持 .xlsx 格式）
/// </summary>
public sealed class ExcelParser
{
    private readonly ILogger<ExcelParser> _logger;

    public ExcelParser(ILogger<ExcelParser> logger)
    {
        _logger = logger;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public async Task<ParsedFileData> ParseAsync(string filePath)
    {
        var result = new ParsedFileData { FileType = "Excel" };

        try
        {
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0];

            if (worksheet.Dimension == null)
            {
                result.Errors.Add("Excel 文件为空");
                return result;
            }

            var colCount = worksheet.Dimension.End.Column;
            var headerRow = new List<string>();

            for (int col = 1; col <= colCount; col++)
            {
                headerRow.Add(worksheet.Cells[1, col].Text.Trim());
            }

            if (colCount == 2)
            {
                await ParseAsFieldValuePairsAsync(worksheet, result);
            }
            else if (colCount > 2 && headerRow.Any(h => !string.IsNullOrWhiteSpace(h)))
            {
                await ParseAsTableAsync(worksheet, headerRow, result);
            }
            else
            {
                result.Warnings.Add("无法识别 Excel 数据结构，请确保格式符合要求");
            }

            _logger.LogInformation("Excel 解析完成：{FieldCount} 个字段，{TableCount} 个表格",
                result.Fields.Count, result.Tables.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel 解析失败");
            result.Errors.Add($"解析失败：{ex.Message}");
        }

        return result;
    }

    private Task ParseAsFieldValuePairsAsync(ExcelWorksheet worksheet, ParsedFileData result)
    {
        for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
        {
            var fieldName = worksheet.Cells[row, 1].Text.Trim();
            var fieldValue = worksheet.Cells[row, 2].Text.Trim();
            if (!string.IsNullOrWhiteSpace(fieldName))
                result.Fields[fieldName] = fieldValue;
        }
        return Task.CompletedTask;
    }

    private Task ParseAsTableAsync(ExcelWorksheet worksheet, List<string> headerRow, ParsedFileData result)
    {
        var tableName = string.IsNullOrWhiteSpace(worksheet.Name) ? "导入表格" : worksheet.Name;
        var tableData = new List<Dictionary<string, string>>();

        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
        {
            var rowData = new Dictionary<string, string>();
            for (int col = 0; col < headerRow.Count; col++)
            {
                var columnName = headerRow[col];
                if (!string.IsNullOrWhiteSpace(columnName))
                    rowData[columnName] = worksheet.Cells[row, col + 1].Text.Trim();
            }
            if (rowData.Count > 0)
                tableData.Add(rowData);
        }

        if (tableData.Count > 0)
            result.Tables[tableName] = tableData;

        return Task.CompletedTask;
    }

    /// <summary>
    /// 提取完整内容，供 W7 AI 批量提取链路使用。
    /// </summary>
    public Task<ParsedDocumentContent> ParseFullContentAsync(string filePath)
    {
        var result = new ParsedDocumentContent { FileType = "Excel" };

        try
        {
            using var package = new ExcelPackage(new FileInfo(filePath));
            var textBuilder = new System.Text.StringBuilder();

            foreach (var worksheet in package.Workbook.Worksheets)
            {
                textBuilder.AppendLine($"工作表：{worksheet.Name}");
                if (worksheet.Dimension == null)
                {
                    textBuilder.AppendLine();
                    continue;
                }

                var headers = new List<string>();
                for (var col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    headers.Add(worksheet.Cells[1, col].Text.Trim());
                }

                var table = new ParsedTable
                {
                    TableIndex = result.Tables.Count,
                    Headers = headers,
                    Rows = new List<List<string>>()
                };

                for (var row = 1; row <= worksheet.Dimension.End.Row; row++)
                {
                    var rowValues = new List<string>();
                    for (var col = 1; col <= worksheet.Dimension.End.Column; col++)
                    {
                        rowValues.Add(worksheet.Cells[row, col].Text.Trim());
                    }

                    if (rowValues.Any(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        textBuilder.AppendLine(string.Join(" | ", rowValues));
                        if (row > 1)
                        {
                            table.Rows.Add(rowValues);
                        }
                    }
                }

                if (table.Headers.Count > 0 && table.Rows.Count > 0)
                {
                    result.Tables.Add(table);
                }

                textBuilder.AppendLine();
            }

            result.PlainText = textBuilder.ToString();
            if (string.IsNullOrWhiteSpace(result.PlainText))
            {
                result.ParseQuality = 60;
                result.Warnings.Add("Excel 中未提取到有效文本内容");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel 全量解析失败");
            result.ParseQuality = 20;
            result.Warnings.Add($"Excel 全量解析失败：{ex.Message}");
        }

        return Task.FromResult(result);
    }
}
