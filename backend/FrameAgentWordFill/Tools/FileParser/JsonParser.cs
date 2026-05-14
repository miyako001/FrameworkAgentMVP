using System.Text.Json;
using FrameAgentWordFill.Models.Import;
using FrameAgentWordFill.Models.AIExtraction;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// JSON 文件解析器
/// </summary>
public sealed class JsonParser
{
    private readonly ILogger<JsonParser> _logger;

    public JsonParser(ILogger<JsonParser> logger)
    {
        _logger = logger;
    }

    public async Task<ParsedFileData> ParseAsync(string filePath)
    {
        var result = new ParsedFileData { FileType = "JSON" };

        try
        {
            var jsonContent = await File.ReadAllTextAsync(filePath);
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("fields", out var fieldsElement) &&
                root.TryGetProperty("tables", out var tablesElement))
            {
                ParseFields(fieldsElement, result);
                ParseTables(tablesElement, result);
            }
            else
            {
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                        ParseTable(property.Name, property.Value, result);
                    else if (property.Value.ValueKind == JsonValueKind.String ||
                             property.Value.ValueKind == JsonValueKind.Number)
                        result.Fields[property.Name] = property.Value.ToString();
                }
            }

            _logger.LogInformation("JSON 解析完成：{FieldCount} 个字段，{TableCount} 个表格",
                result.Fields.Count, result.Tables.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON 解析失败");
            result.Errors.Add($"解析失败：{ex.Message}");
        }

        return result;
    }

    private void ParseFields(JsonElement fieldsElement, ParsedFileData result)
    {
        foreach (var property in fieldsElement.EnumerateObject())
            result.Fields[property.Name] = property.Value.ToString();
    }

    private void ParseTables(JsonElement tablesElement, ParsedFileData result)
    {
        foreach (var tableProperty in tablesElement.EnumerateObject())
            ParseTable(tableProperty.Name, tableProperty.Value, result);
    }

    private void ParseTable(string tableName, JsonElement arrayElement, ParsedFileData result)
    {
        var tableData = new List<Dictionary<string, string>>();

        foreach (var rowElement in arrayElement.EnumerateArray())
        {
            var rowData = new Dictionary<string, string>();
            foreach (var cellProperty in rowElement.EnumerateObject())
                rowData[cellProperty.Name] = cellProperty.Value.ToString();
            if (rowData.Count > 0)
                tableData.Add(rowData);
        }

        if (tableData.Count > 0)
            result.Tables[tableName] = tableData;
    }

    /// <summary>
    /// 提取完整 JSON 文本与结构信息，供 W7 AI 提取链路使用。
    /// </summary>
    public async Task<ParsedDocumentContent> ParseFullContentAsync(string filePath)
    {
        var result = new ParsedDocumentContent { FileType = "JSON" };

        try
        {
            var jsonContent = await File.ReadAllTextAsync(filePath);
            result.PlainText = jsonContent;

            var parsed = await ParseAsync(filePath);
            foreach (var (tableName, rows) in parsed.Tables)
            {
                if (rows.Count == 0)
                {
                    continue;
                }

                var headers = rows.SelectMany(x => x.Keys).Distinct().ToList();
                var table = new ParsedTable
                {
                    TableIndex = result.Tables.Count,
                    Headers = headers,
                    Rows = rows.Select(row => headers.Select(h => row.GetValueOrDefault(h, string.Empty)).ToList()).ToList()
                };
                result.Tables.Add(table);
            }

            if (parsed.Errors.Count > 0)
            {
                result.ParseQuality = 40;
                result.Warnings.AddRange(parsed.Errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON 全量解析失败");
            result.ParseQuality = 20;
            result.Warnings.Add($"JSON 全量解析失败：{ex.Message}");
        }

        return result;
    }
}
