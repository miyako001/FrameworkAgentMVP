using FrameAgentWordFill.Models.AIExtraction;
using System.Text;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// 纯文本类文件解析器（txt/csv/md）。
/// </summary>
public sealed class PlainTextParser
{
    private readonly ILogger<PlainTextParser> _logger;

    public PlainTextParser(ILogger<PlainTextParser> logger)
    {
        _logger = logger;
    }

    public async Task<ParsedDocumentContent> ParseAsync(string filePath, string fileType)
    {
        var result = new ParsedDocumentContent { FileType = fileType };

        try
        {
            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            result.PlainText = content;

            if (fileType.Equals("CSV", StringComparison.OrdinalIgnoreCase))
            {
                ParseCsv(content, result);
            }

            if (string.IsNullOrWhiteSpace(result.PlainText))
            {
                result.ParseQuality = 60;
                result.Warnings.Add("文本内容为空");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文本文件解析失败");
            result.ParseQuality = 20;
            result.Warnings.Add($"文本解析失败：{ex.Message}");
        }

        return result;
    }

    private static void ParseCsv(string content, ParsedDocumentContent result)
    {
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return;
        }

        var headers = lines[0].Split(',').Select(x => x.Trim()).ToList();
        var table = new ParsedTable
        {
            TableIndex = 0,
            Headers = headers,
            Rows = new List<List<string>>()
        };

        for (var i = 1; i < lines.Length; i++)
        {
            var row = lines[i].Split(',').Select(x => x.Trim()).ToList();
            table.Rows.Add(row);
        }

        result.Tables.Add(table);
    }
}
