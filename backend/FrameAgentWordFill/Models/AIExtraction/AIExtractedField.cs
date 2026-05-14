namespace FrameAgentWordFill.Models.AIExtraction;

/// <summary>
/// AI 批量提取后的字段项。
/// </summary>
public sealed class AIExtractedField
{
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
    public int Confidence { get; set; }
    public string? SourceText { get; set; }
    public string MatchMethod { get; set; } = "Semantic";

    public string ConfidenceLevel => Confidence switch
    {
        >= 90 => "High",
        >= 70 => "Medium",
        _ => "Low"
    };
}
