namespace FrameAgentWordFill.Models.AIExtraction;

/// <summary>
/// 文件解析后的标准化文档内容。
/// </summary>
public sealed class ParsedDocumentContent
{
    public string FileType { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public List<ParsedTable> Tables { get; set; } = new();
    public List<ParsedImage> Images { get; set; } = new();
    public int ParseQuality { get; set; } = 100;
    public List<string> Warnings { get; set; } = new();
}

public sealed class ParsedTable
{
    public int TableIndex { get; set; }
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}

public sealed class ParsedImage
{
    public int ImageIndex { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? OcrText { get; set; }
}
