namespace FrameAgentWordFill.Models.Templates;

/// <summary>
/// 模板实体
/// </summary>
public sealed class Template
{
    /// <summary>
    /// 模板ID（GUID）
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 模板名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 存储的文件名（GUID_原始文件名）
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 原始文件名
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 状态（enabled/disabled）
    /// </summary>
    public string Status { get; set; } = "enabled";

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 字段列表（导航属性）
    /// </summary>
    public List<Field> Fields { get; set; } = new();

    /// <summary>
    /// 表格列表（导航属性）
    /// </summary>
    public List<TableDefinition> Tables { get; set; } = new();
}

/// <summary>
/// 字段实体
/// </summary>
public sealed class Field
{
    public int Id { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public bool Required { get; set; } = false;
    public int FieldOrder { get; set; } = 0;
    public string? GuidePrompt { get; set; }
    public string? MissingPrompt { get; set; }
    public string? InvalidPrompt { get; set; }
}

/// <summary>
/// 表格定义实体
/// </summary>
public sealed class TableDefinition
{
    public int Id { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RowType { get; set; } = "dynamic";
    public int MaxRows { get; set; } = 10;
    public string? GuidePrompt { get; set; }
    public List<TableColumn> Columns { get; set; } = new();
}

/// <summary>
/// 表格列实体
/// </summary>
public sealed class TableColumn
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ColumnOrder { get; set; } = 0;
}
