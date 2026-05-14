namespace FrameAgentWordFill.Models.Import;

/// <summary>
/// 导入会话（记录每次导入操作的状态）
/// </summary>
public sealed class ImportSession
{
    public int SessionId { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = "Parsing";
    public int MatchedFieldCount { get; set; } = 0;
    public int UnmatchedFieldCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}
