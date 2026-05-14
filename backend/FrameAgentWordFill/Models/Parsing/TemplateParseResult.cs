namespace FrameAgentWordFill.Models.Parsing;

/// <summary>
/// 模板解析结果
/// </summary>
public sealed class TemplateParseResult
{
    /// <summary>
    /// 解析是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 普通字段列表
    /// </summary>
    public List<FieldInfo> Fields { get; set; } = new();

    /// <summary>
    /// 表格列表
    /// </summary>
    public List<TableInfo> Tables { get; set; } = new();

    /// <summary>
    /// 警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 字段信息
/// </summary>
public sealed class FieldInfo
{
    /// <summary>
    /// 字段名称（例如：项目名称）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型（text/phone/email/date/number）
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// 在模板中的位置（段落索引）
    /// </summary>
    public int Position { get; set; }
}

/// <summary>
/// 表格信息
/// </summary>
public sealed class TableInfo
{
    /// <summary>
    /// 表格名称（例如：成员列表）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 表格列（字段）
    /// </summary>
    public List<TableColumnInfo> Columns { get; set; } = new();

    /// <summary>
    /// 行类型（fixed/dynamic）
    /// </summary>
    public string RowType { get; set; } = "dynamic";

    /// <summary>
    /// 最大行数
    /// </summary>
    public int MaxRows { get; set; } = 10;
}

/// <summary>
/// 表格列信息
/// </summary>
public sealed class TableColumnInfo
{
    /// <summary>
    /// 列名（例如：姓名）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 列类型
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// 列顺序
    /// </summary>
    public int Order { get; set; }
}
