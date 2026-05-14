namespace FrameAgentWordFill.Models.Import;

/// <summary>
/// 导入表格数据（存储从文件中提取的表格数据）
/// </summary>
public sealed class ImportTableData
{
    public int DataId { get; set; }
    public int SessionId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string? CellValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
