namespace FrameAgentWordFill.Models.Chat;

/// <summary>
/// 对话会话
/// </summary>
public sealed class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TemplateId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string Status { get; set; } = "active"; // active/completed/expired
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 已收集的字段数据
    /// </summary>
    public Dictionary<string, SessionField> CollectedFields { get; set; } = new();

    /// <summary>
    /// 已收集的表格数据
    /// </summary>
    public Dictionary<string, List<Dictionary<string, string>>> CollectedTables { get; set; } = new();

    /// <summary>
    /// 对话历史
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// 当前填写进度（下一个要填的字段索引）
    /// </summary>
    public int CurrentFieldIndex { get; set; } = 0;
}

/// <summary>
/// 会话字段（包含置信度）
/// </summary>
public sealed class SessionField
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double Confidence { get; set; } = 1.0; // 0-1，AI提取的置信度
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 对话消息
/// </summary>
public sealed class ChatMessage
{
    public string Role { get; set; } = string.Empty; // user/assistant/system
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 对话响应
/// </summary>
public sealed class ChatResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string>? ExtractedFields { get; set; }
    public List<string>? ValidationErrors { get; set; }
    public bool IsCompleted { get; set; }
    public double Progress { get; set; } // 0-1
    public string? ShortcutType { get; set; }
}

/// <summary>
/// 快捷指令识别结果
/// </summary>
public sealed class ShortcutDetectionResult
{
    public bool HasShortcut { get; set; }
    public string? ShortcutType { get; set; }
    public string? TargetField { get; set; }
}
