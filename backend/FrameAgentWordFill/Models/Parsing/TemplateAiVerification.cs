namespace FrameAgentWordFill.Models.Parsing;

/// <summary>
/// AI 双轨解析比对摘要。
/// </summary>
public sealed class TemplateAiVerification
{
    public bool Enabled { get; set; }

    public bool AiAvailable { get; set; }

    public int FieldDiffCount { get; set; }

    public int TableDiffCount { get; set; }

    public string ComparisonLevel { get; set; } = "disabled";
}

/// <summary>
/// AI 解析结构化结果。
/// </summary>
public sealed class TemplateAiParseResult
{
    public bool Success { get; set; }

    public bool Available { get; set; }

    public string Engine { get; set; } = string.Empty;

    public List<string> Fields { get; set; } = new();

    public List<AiTableCandidate> Tables { get; set; } = new();

    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// AI 识别到的表格候选。
/// </summary>
public sealed class AiTableCandidate
{
    public string Name { get; set; } = string.Empty;

    public List<string> Columns { get; set; } = new();
}

/// <summary>
/// 本地解析与 AI 解析的差异比较结果。
/// </summary>
public sealed class TemplateParseComparisonResult
{
    public int FieldDiffCount { get; set; }

    public int TableDiffCount { get; set; }

    public string ComparisonLevel { get; set; } = "consistent";

    public List<string> Warnings { get; set; } = new();
}
