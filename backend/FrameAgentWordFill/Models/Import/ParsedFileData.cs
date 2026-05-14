namespace FrameAgentWordFill.Models.Import;

/// <summary>
/// 文件解析结果（内存对象，不存数据库）
/// </summary>
public sealed class ParsedFileData
{
    public string FileType { get; set; } = string.Empty;
    public Dictionary<string, string> Fields { get; set; } = new();
    public Dictionary<string, List<Dictionary<string, string>>> Tables { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
