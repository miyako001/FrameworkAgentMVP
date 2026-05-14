namespace FrameAgentWordFill.Models.Import;

/// <summary>
/// 导入字段映射（记录源字段与模板字段的匹配关系）
/// </summary>
public sealed class ImportFieldMapping
{
    public int MappingId { get; set; }
    public int SessionId { get; set; }
    public string SourceFieldName { get; set; } = string.Empty;
    public string? TemplateFieldName { get; set; }
    public string? FieldValue { get; set; }
    public int MatchConfidence { get; set; } = 0;
    public string MatchMethod { get; set; } = "Manual";
    public bool IsUserConfirmed { get; set; } = false;
    public string FieldType { get; set; } = "Normal";
    public string? TableName { get; set; }
    public string? ColumnName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
