# W4 实现流程 - AI 智能对话填充

**周期**：第 4 周  
**里程碑**：M5 - AI 对话交互链路  
**目标**：实现用户与 AI Agent 自然语言对话填充字段的核心功能

**⚠️ 核心功能说明**：
- 本周是项目的**最重要功能周**，实现 AI 驱动的智能对话填充
- 用户使用**自然语言**与 AI 对话，AI **智能提取**字段值
- AI 根据模板配置**智能引导**用户填写，提供类似 ChatGPT 的体验
- 支持**快捷指令**、**断点续聊**、**流式对话**

---

## 📋 实施步骤总览

```
步骤1: 设计 WordFillAgent Prompt 和工具集
    ↓
步骤2: 实现 AI 字段提取工具（LLM 驱动）
    ↓
步骤3: 实现对话会话管理（SQLite）
    ↓
步骤4: 实现 AI 对话引导引擎
    ↓
步骤5: 实现快捷指令识别
    ↓
步骤6: 实现流式对话接口（SSE）
    ↓
步骤7: 实现前端对话界面
    ↓
步骤8: 验收测试
```

---

## 步骤 1：设计 WordFillAgent Prompt 和工具集

### 1.1 创建 Agent 配置

创建 `Agents/WordFillAgentConfig.cs`：

```csharp
namespace FrameAgentWordFill.Agents;

/// <summary>
/// WordFillAgent 配置（Prompt 模板和工具定义）
/// </summary>
public static class WordFillAgentConfig
{
    /// <summary>
    /// 系统 Prompt（定义 Agent 行为和能力）
    /// </summary>
    public const string SystemPrompt = @"
你是一个专业的文档填写助手，负责帮助用户智能填写 Word 文档模板。

你的职责：
1. 引导用户填写模板中的所有字段
2. 从用户的自然语言回答中智能提取字段值
3. 验证字段格式是否正确（电话、邮箱、日期等）
4. 提供友好的对话体验，像真人助手一样

你的能力：
- 理解用户的自然语言输入（例如："项目名是智能办公系统，负责人是张三"）
- 从一句话中提取多个字段值
- 识别快捷指令（例如："我要一次性填完"、"下载模板"）
- 智能追问缺失或格式错误的字段

注意事项：
- 保持对话自然流畅，不要机械化
- 每次只询问 1-2 个字段，避免信息过载
- 用户提供的值可能不完全匹配字段名，你需要智能匹配
- 如果用户一次提供多个字段，全部提取
- 表格数据可以分批收集

当前模板信息：
{TEMPLATE_INFO}

已收集字段：
{COLLECTED_FIELDS}

下一步行动：
{NEXT_ACTION}
";

    /// <summary>
    /// 欢迎消息模板
    /// </summary>
    public const string WelcomeMessageTemplate = @"
您好！我是文档填写助手，将帮您填写【{TemplateName}】。

这个模板包含 {FieldCount} 个字段{TableInfo}。

让我们开始吧！{FirstQuestion}
";

    /// <summary>
    /// 字段提取 Prompt 模板
    /// </summary>
    public const string FieldExtractionPrompt = @"
请从用户的回答中提取字段值。

用户回答：
{USER_MESSAGE}

需要提取的字段：
{FIELD_LIST}

要求：
1. 返回 JSON 格式：{""字段名"": ""提取的值"", ...}
2. 如果用户没有提及某个字段，不要包含该字段
3. 智能匹配字段名（例如："负责人"可能对应"项目负责人"或"负责人姓名"）
4. 提取所有提及的字段

示例：
用户回答：项目名是智能办公系统，负责人是张三，预算50万
输出：{{""项目名称"": ""智能办公系统"", ""负责人"": ""张三"", ""预算"": ""50万""}}
";

    /// <summary>
    /// 快捷指令识别 Prompt 模板
    /// </summary>
    public const string ShortcutDetectionPrompt = @"
请判断用户的输入是否包含快捷指令。

用户输入：
{USER_MESSAGE}

支持的快捷指令：
- ""我要一次性填完"" / ""直接列出所有字段"" → 批量填写模式
- ""给我模板"" / ""下载模板"" → 下载模板文件
- ""跳过废话"" / ""直接开始"" → 快速模式
- ""修改[字段名]"" → 修改已填字段
- ""重新开始"" → 清空已填数据

返回 JSON 格式：
{{
    ""hasShortcut"": true/false,
    ""shortcutType"": ""批量填写"" / ""下载模板"" / ""快速模式"" / ""修改字段"" / ""重新开始"",
    ""targetField"": ""字段名（仅修改字段时需要）""
}}

如果没有快捷指令，返回 {{""hasShortcut"": false}}
";

    /// <summary>
    /// 表格数据提取 Prompt 模板
    /// </summary>
    public const string TableExtractionPrompt = @"
请从用户的回答中提取表格数据。

用户回答：
{USER_MESSAGE}

表格结构：
表格名：{TABLE_NAME}
列：{COLUMN_LIST}

要求：
1. 返回 JSON 数组格式：[{{""列名1"": ""值1"", ""列名2"": ""值2""}}, ...]
2. 支持多种输入格式：
   - 逐行输入："第一个人是张三，职务是经理"
   - 批量输入："张三,经理,13800138000; 李四,工程师,13800138001"
   - 自然语言："团队有张三（经理）、李四（工程师）"
3. 智能解析和匹配列名

示例：
用户回答：团队成员有张三，他是项目经理，电话13800138000；还有李四，技术负责人，13800138001
输出：[
    {{""姓名"": ""张三"", ""职务"": ""项目经理"", ""联系方式"": ""13800138000""}},
    {{""姓名"": ""李四"", ""职务"": ""技术负责人"", ""联系方式"": ""13800138001""}}
]
";
}
```

### 1.2 创建会话状态模型

创建 `Models/ChatSession.cs`：

```csharp
using System.Text.Json;

namespace FrameAgentWordFill.Models;

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
```

---

## 步骤 2：实现 AI 字段提取工具

创建 `Tools/AIFieldExtractor.cs`：

```csharp
using FrameAgentWordFill.Models;
using FrameAgentWordFill.Services;
using FrameAgentWordFill.Agents;
using System.Text.Json;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// AI 字段提取工具（使用 LLM 从自然语言中提取字段值）
/// </summary>
public sealed class AIFieldExtractor
{
    private readonly AIService _aiService;
    private readonly ILogger<AIFieldExtractor> _logger;

    public AIFieldExtractor(AIService aiService, ILogger<AIFieldExtractor> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// 从用户消息中提取字段值
    /// </summary>
    /// <param name="userMessage">用户输入的消息</param>
    /// <param name="targetFields">目标字段列表</param>
    /// <returns>提取的字段字典</returns>
    public async Task<Dictionary<string, string>> ExtractFieldsAsync(
        string userMessage,
        List<Field> targetFields)
    {
        try
        {
            // 构建字段列表描述
            var fieldList = string.Join("\n", targetFields.Select(f =>
                $"- {f.Name}（类型：{f.FieldType}，{(f.Required ? "必填" : "可选")}）"
            ));

            // 构建提取 Prompt
            var prompt = WordFillAgentConfig.FieldExtractionPrompt
                .Replace("{USER_MESSAGE}", userMessage)
                .Replace("{FIELD_LIST}", fieldList);

            // 调用 LLM
            var response = await _aiService.GetCompletionAsync(prompt);

            _logger.LogDebug("AI 字段提取响应: {Response}", response);

            // 解析 JSON 响应
            var extractedFields = ParseJsonResponse(response);

            _logger.LogInformation("提取字段成功: {Count} 个字段", extractedFields.Count);
            return extractedFields;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 字段提取失败");
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// 识别快捷指令
    /// </summary>
    public async Task<ShortcutDetectionResult> DetectShortcutAsync(string userMessage)
    {
        try
        {
            var prompt = WordFillAgentConfig.ShortcutDetectionPrompt
                .Replace("{USER_MESSAGE}", userMessage);

            var response = await _aiService.GetCompletionAsync(prompt);

            _logger.LogDebug("快捷指令识别响应: {Response}", response);

            var result = JsonSerializer.Deserialize<ShortcutDetectionResult>(
                response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return result ?? new ShortcutDetectionResult { HasShortcut = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "快捷指令识别失败");
            return new ShortcutDetectionResult { HasShortcut = false };
        }
    }

    /// <summary>
    /// 提取表格数据
    /// </summary>
    public async Task<List<Dictionary<string, string>>> ExtractTableDataAsync(
        string userMessage,
        TableDefinition table)
    {
        try
        {
            var columnList = string.Join("、", table.Columns.Select(c => c.Name));

            var prompt = WordFillAgentConfig.TableExtractionPrompt
                .Replace("{USER_MESSAGE}", userMessage)
                .Replace("{TABLE_NAME}", table.Name)
                .Replace("{COLUMN_LIST}", columnList);

            var response = await _aiService.GetCompletionAsync(prompt);

            _logger.LogDebug("表格数据提取响应: {Response}", response);

            var tableData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(
                response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return tableData ?? new List<Dictionary<string, string>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "表格数据提取失败");
            return new List<Dictionary<string, string>>();
        }
    }

    /// <summary>
    /// 解析 LLM 的 JSON 响应
    /// </summary>
    private Dictionary<string, string> ParseJsonResponse(string response)
    {
        try
        {
            // 提取 JSON 部分（可能包含在 markdown 代码块中）
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                response,
                @"```(?:json)?\s*(\{.*?\})\s*```",
                System.Text.RegularExpressions.RegexOptions.Singleline
            );

            var jsonString = jsonMatch.Success ? jsonMatch.Groups[1].Value : response;

            // 解析 JSON
            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                jsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? new Dictionary<string, string>();
        }
        catch
        {
            // 如果 JSON 解析失败，返回空字典
            return new Dictionary<string, string>();
        }
    }
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
```

---

## 步骤 3：实现对话会话管理

创建 `Repositories/ChatSessionRepository.cs`：

```csharp
using Microsoft.Data.Sqlite;
using FrameAgentWordFill.Models;
using System.Text.Json;

namespace FrameAgentWordFill.Repositories;

/// <summary>
/// 对话会话数据访问层（⚠️ 注意：使用 fa_ 前缀）
/// </summary>
public sealed class ChatSessionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ChatSessionRepository> _logger;

    public ChatSessionRepository(IConfiguration configuration, ILogger<ChatSessionRepository> logger)
    {
        _logger = logger;
        var dbPath = GetDatabasePath(configuration);
        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// 创建会话
    /// </summary>
    public async Task<bool> CreateSessionAsync(ChatSession session)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO fa_chat_sessions (id, template_id, user_id, status, created_at, updated_at)
            VALUES (@id, @templateId, @userId, @status, @createdAt, @updatedAt)
        ";
        command.Parameters.AddWithValue("@id", session.Id);
        command.Parameters.AddWithValue("@templateId", session.TemplateId);
        command.Parameters.AddWithValue("@userId", session.UserId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@status", session.Status);
        command.Parameters.AddWithValue("@createdAt", session.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@updatedAt", session.UpdatedAt.ToString("o"));

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    /// <summary>
    /// 获取会话
    /// </summary>
    public async Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, template_id, user_id, status, created_at, updated_at
            FROM fa_chat_sessions
            WHERE id = @id
        ";
        command.Parameters.AddWithValue("@id", sessionId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var session = new ChatSession
        {
            Id = reader.GetString(0),
            TemplateId = reader.GetString(1),
            UserId = reader.IsDBNull(2) ? null : reader.GetString(2),
            Status = reader.GetString(3),
            CreatedAt = DateTime.Parse(reader.GetString(4)),
            UpdatedAt = DateTime.Parse(reader.GetString(5))
        };

        // 加载会话字段
        await LoadSessionFieldsAsync(connection, session);

        return session;
    }

    /// <summary>
    /// 保存会话字段
    /// </summary>
    public async Task<bool> SaveSessionFieldAsync(string sessionId, SessionField field)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // 先删除旧值
        await using var deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText = @"
            DELETE FROM fa_session_fields WHERE session_id = @sessionId AND field_name = @fieldName
        ";
        deleteCommand.Parameters.AddWithValue("@sessionId", sessionId);
        deleteCommand.Parameters.AddWithValue("@fieldName", field.Name);
        await deleteCommand.ExecuteNonQueryAsync();

        // 插入新值
        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = @"
            INSERT INTO fa_session_fields (session_id, field_name, field_value, confidence, created_at)
            VALUES (@sessionId, @fieldName, @fieldValue, @confidence, @createdAt)
        ";
        insertCommand.Parameters.AddWithValue("@sessionId", sessionId);
        insertCommand.Parameters.AddWithValue("@fieldName", field.Name);
        insertCommand.Parameters.AddWithValue("@fieldValue", field.Value);
        insertCommand.Parameters.AddWithValue("@confidence", field.Confidence);
        insertCommand.Parameters.AddWithValue("@createdAt", field.CollectedAt.ToString("o"));

        var rows = await insertCommand.ExecuteNonQueryAsync();
        return rows > 0;
    }

    /// <summary>
    /// 更新会话状态
    /// </summary>
    public async Task<bool> UpdateSessionStatusAsync(string sessionId, string status)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE fa_chat_sessions 
            SET status = @status, updated_at = @updatedAt
            WHERE id = @id
        ";
        command.Parameters.AddWithValue("@id", sessionId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    /// <summary>
    /// 加载会话字段
    /// </summary>
    private async Task LoadSessionFieldsAsync(SqliteConnection connection, ChatSession session)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT field_name, field_value, confidence, created_at
            FROM fa_session_fields
            WHERE session_id = @sessionId
            ORDER BY created_at
        ";
        command.Parameters.AddWithValue("@sessionId", session.Id);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var field = new SessionField
            {
                Name = reader.GetString(0),
                Value = reader.GetString(1),
                Confidence = reader.GetDouble(2),
                CollectedAt = DateTime.Parse(reader.GetString(3))
            };

            session.CollectedFields[field.Name] = field;
        }
    }

    private static string GetDatabasePath(IConfiguration configuration)
    {
        var rootPath = configuration["Storage:RootPath"] ?? "..\\..\\storage";
        var fullPath = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rootPath));
        return Path.Combine(fullPath, "data", "frameagent.db");
    }
}
```

---

## 步骤 4：实现 AI 对话引导引擎

创建 `Services/ChatService.cs`：

```csharp
using FrameAgentWordFill.Models;
using FrameAgentWordFill.Repositories;
using FrameAgentWordFill.Tools;
using FrameAgentWordFill.Agents;

namespace FrameAgentWordFill.Services;

/// <summary>
/// 对话服务（AI 智能对话引导）
/// </summary>
public sealed class ChatService
{
    private readonly TemplateRepository _templateRepository;
    private readonly ChatSessionRepository _sessionRepository;
    private readonly AIFieldExtractor _fieldExtractor;
    private readonly DataValidator _dataValidator;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        TemplateRepository templateRepository,
        ChatSessionRepository sessionRepository,
        AIFieldExtractor fieldExtractor,
        DataValidator dataValidator,
        ILogger<ChatService> logger)
    {
        _templateRepository = templateRepository;
        _sessionRepository = sessionRepository;
        _fieldExtractor = fieldExtractor;
        _dataValidator = dataValidator;
        _logger = logger;
    }

    /// <summary>
    /// 开始新会话
    /// </summary>
    public async Task<(bool Success, string? SessionId, string? WelcomeMessage)> StartSessionAsync(
        string templateId)
    {
        try
        {
            // 1. 获取模板
            var template = await _templateRepository.GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                return (false, null, "模板不存在");
            }

            // 2. 创建会话
            var session = new ChatSession
            {
                TemplateId = templateId,
                Status = "active"
            };

            var created = await _sessionRepository.CreateSessionAsync(session);
            if (!created)
            {
                return (false, null, "创建会话失败");
            }

            // 3. 生成欢迎消息
            var welcomeMessage = GenerateWelcomeMessage(template);

            _logger.LogInformation("会话创建成功: {SessionId}, 模板: {TemplateId}", 
                session.Id, templateId);

            return (true, session.Id, welcomeMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动会话失败: {TemplateId}", templateId);
            return (false, null, $"启动会话失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理用户消息
    /// </summary>
    public async Task<ChatResponse> ProcessMessageAsync(string sessionId, string userMessage)
    {
        try
        {
            // 1. 获取会话和模板
            var session = await _sessionRepository.GetSessionAsync(sessionId);
            if (session == null)
            {
                return new ChatResponse
                {
                    Success = false,
                    Message = "会话不存在或已过期"
                };
            }

            var template = await _templateRepository.GetTemplateByIdAsync(session.TemplateId);
            if (template == null)
            {
                return new ChatResponse
                {
                    Success = false,
                    Message = "模板不存在"
                };
            }

            // 2. 检测快捷指令
            var shortcut = await _fieldExtractor.DetectShortcutAsync(userMessage);
            if (shortcut.HasShortcut)
            {
                return await HandleShortcutAsync(session, template, shortcut);
            }

            // 3. 提取字段值
            var remainingFields = template.Fields
                .Where(f => !session.CollectedFields.ContainsKey(f.Name))
                .ToList();

            var extractedFields = await _fieldExtractor.ExtractFieldsAsync(
                userMessage,
                remainingFields
            );

            // 4. 验证并保存字段
            var validationErrors = new List<string>();
            foreach (var kvp in extractedFields)
            {
                var field = template.Fields.FirstOrDefault(f =>
                    f.Name.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));

                if (field == null)
                    continue;

                // 验证字段值
                var (isValid, errorMessage) = _dataValidator.ValidateField(field, kvp.Value);
                if (!isValid)
                {
                    validationErrors.Add($"{field.Name}：{errorMessage}");
                    continue;
                }

                // 保存字段
                var sessionField = new SessionField
                {
                    Name = field.Name,
                    Value = kvp.Value,
                    Confidence = 0.9 // AI 提取的置信度
                };

                await _sessionRepository.SaveSessionFieldAsync(sessionId, sessionField);
                session.CollectedFields[field.Name] = sessionField;

                _logger.LogInformation("字段收集: {SessionId}, {FieldName} = {FieldValue}",
                    sessionId, field.Name, kvp.Value);
            }

            // 5. 生成下一步引导消息
            var nextMessage = await GenerateNextGuideMessageAsync(session, template, validationErrors);

            // 6. 检查是否完成
            var isCompleted = CheckIfCompleted(session, template);
            if (isCompleted)
            {
                await _sessionRepository.UpdateSessionStatusAsync(sessionId, "completed");
            }

            return new ChatResponse
            {
                Success = true,
                Message = nextMessage,
                ExtractedFields = extractedFields,
                ValidationErrors = validationErrors.Count > 0 ? validationErrors : null,
                IsCompleted = isCompleted,
                Progress = CalculateProgress(session, template)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理消息失败: {SessionId}", sessionId);
            return new ChatResponse
            {
                Success = false,
                Message = $"处理失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 生成欢迎消息
    /// </summary>
    private string GenerateWelcomeMessage(Template template)
    {
        var tableInfo = template.Tables.Count > 0
            ? $"和 {template.Tables.Count} 个表格"
            : "";

        var firstField = template.Fields.FirstOrDefault();
        var firstQuestion = firstField != null
            ? $"\n\n{firstField.GuidePrompt ?? $"首先，请告诉我{firstField.Name}是什么？"}"
            : "";

        return WordFillAgentConfig.WelcomeMessageTemplate
            .Replace("{TemplateName}", template.Name)
            .Replace("{FieldCount}", template.Fields.Count.ToString())
            .Replace("{TableInfo}", tableInfo)
            .Replace("{FirstQuestion}", firstQuestion);
    }

    /// <summary>
    /// 生成下一步引导消息
    /// </summary>
    private async Task<string> GenerateNextGuideMessageAsync(
        ChatSession session,
        Template template,
        List<string> validationErrors)
    {
        // 1. 如果有验证错误，先提示错误
        if (validationErrors.Count > 0)
        {
            return $"抱歉，以下字段格式不正确：\n{string.Join("\n", validationErrors)}\n\n请重新输入。";
        }

        // 2. 确认刚收集的字段
        var justCollected = session.CollectedFields.Values
            .OrderByDescending(f => f.CollectedAt)
            .Take(3)
            .ToList();

        var confirmation = justCollected.Count > 0
            ? $"好的，已记录：{string.Join("、", justCollected.Select(f => $"{f.Name}={f.Value}"))}。\n\n"
            : "";

        // 3. 询问下一个未填字段
        var nextField = template.Fields
            .FirstOrDefault(f => !session.CollectedFields.ContainsKey(f.Name));

        if (nextField != null)
        {
            var guidePrompt = nextField.GuidePrompt ?? $"请告诉我{nextField.Name}是什么？";
            return $"{confirmation}{guidePrompt}";
        }

        // 4. 如果普通字段都填完了，询问表格
        var nextTable = template.Tables
            .FirstOrDefault(t => !session.CollectedTables.ContainsKey(t.Name));

        if (nextTable != null)
        {
            var columnList = string.Join("、", nextTable.Columns.Select(c => c.Name));
            var guidePrompt = nextTable.GuidePrompt 
                ?? $"接下来请提供{nextTable.Name}的数据，包括：{columnList}";
            return $"{confirmation}{guidePrompt}";
        }

        // 5. 全部完成
        return $"{confirmation}太好了！所有信息已收集完成。\n\n您可以点击\"生成文档\"按钮下载最终文档。";
    }

    /// <summary>
    /// 处理快捷指令
    /// </summary>
    private async Task<ChatResponse> HandleShortcutAsync(
        ChatSession session,
        Template template,
        ShortcutDetectionResult shortcut)
    {
        return shortcut.ShortcutType switch
        {
            "批量填写" => new ChatResponse
            {
                Success = true,
                Message = GenerateBatchFillGuide(template, session),
                ShortcutType = "批量填写"
            },
            "下载模板" => new ChatResponse
            {
                Success = true,
                Message = $"您可以点击这里下载模板文件：/api/template/{template.Id}/download",
                ShortcutType = "下载模板"
            },
            _ => new ChatResponse
            {
                Success = true,
                Message = "抱歉，我还不支持这个快捷指令。"
            }
        };
    }

    /// <summary>
    /// 生成批量填写引导
    /// </summary>
    private string GenerateBatchFillGuide(Template template, ChatSession session)
    {
        var remainingFields = template.Fields
            .Where(f => !session.CollectedFields.ContainsKey(f.Name))
            .ToList();

        if (remainingFields.Count == 0)
        {
            return "所有字段已填写完成！";
        }

        var fieldList = string.Join("\n", remainingFields.Select(f =>
            $"- {f.Name}（{f.FieldType}，{(f.Required ? "必填" : "可选")}）"
        ));

        return $"好的，以下是所有需要填写的字段：\n\n{fieldList}\n\n您可以一次性提供所有信息，我会智能识别。";
    }

    /// <summary>
    /// 检查是否完成
    /// </summary>
    private bool CheckIfCompleted(ChatSession session, Template template)
    {
        // 检查所有必填字段是否都已填写
        var requiredFields = template.Fields.Where(f => f.Required).ToList();
        return requiredFields.All(f => session.CollectedFields.ContainsKey(f.Name));
    }

    /// <summary>
    /// 计算进度
    /// </summary>
    private double CalculateProgress(ChatSession session, Template template)
    {
        if (template.Fields.Count == 0)
            return 1.0;

        return (double)session.CollectedFields.Count / template.Fields.Count;
    }
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
    public double Progress { get; set; }
    public string? ShortcutType { get; set; }
}
```

---

## 步骤 5：实现流式对话接口（SSE）

创建 `Controllers/ChatController.cs`：

```csharp
using Microsoft.AspNetCore.Mvc;
using FrameAgentWordFill.Services;
using System.Text;

namespace FrameAgentWordFill.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// 开始新会话
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            return BadRequest(new { error = "TemplateId 不能为空" });
        }

        var (success, sessionId, welcomeMessage) = await _chatService.StartSessionAsync(request.TemplateId);

        if (!success)
        {
            return BadRequest(new { error = welcomeMessage });
        }

        return Ok(new
        {
            success = true,
            sessionId = sessionId,
            message = welcomeMessage
        });
    }

    /// <summary>
    /// 发送消息（流式响应）
    /// </summary>
    [HttpPost("message/stream")]
    public async Task SendMessageStream([FromBody] ChatMessageRequest request)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");

        try
        {
            // 处理消息
            var response = await _chatService.ProcessMessageAsync(request.SessionId, request.Message);

            // 流式发送响应（模拟打字效果）
            await StreamResponseAsync(response.Message);

            // 发送元数据
            await SendSseEventAsync("metadata", new
            {
                extractedFields = response.ExtractedFields,
                validationErrors = response.ValidationErrors,
                isCompleted = response.IsCompleted,
                progress = response.Progress
            });

            // 发送完成信号
            await SendSseEventAsync("done", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式消息处理失败");
            await SendSseEventAsync("error", new { message = ex.Message });
        }

        await Response.Body.FlushAsync();
    }

    /// <summary>
    /// 发送消息（普通响应）
    /// </summary>
    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "SessionId 和 Message 不能为空" });
        }

        var response = await _chatService.ProcessMessageAsync(request.SessionId, request.Message);

        if (!response.Success)
        {
            return BadRequest(new { error = response.Message });
        }

        return Ok(response);
    }

    /// <summary>
    /// 流式发送响应（打字效果）
    /// </summary>
    private async Task StreamResponseAsync(string message)
    {
        const int chunkSize = 3; // 每次发送的字符数
        const int delayMs = 30; // 每次发送的延迟（毫秒）

        for (int i = 0; i < message.Length; i += chunkSize)
        {
            var chunk = message.Substring(i, Math.Min(chunkSize, message.Length - i));
            await SendSseEventAsync("message", new { chunk });
            await Task.Delay(delayMs);
        }
    }

    /// <summary>
    /// 发送 SSE 事件
    /// </summary>
    private async Task SendSseEventAsync(string eventType, object? data)
    {
        var json = data != null ? System.Text.Json.JsonSerializer.Serialize(data) : "null";
        var sseData = $"event: {eventType}\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(sseData);
        await Response.Body.WriteAsync(bytes);
        await Response.Body.FlushAsync();
    }
}

public sealed class StartSessionRequest
{
    public string TemplateId { get; set; } = string.Empty;
}

public sealed class ChatMessageRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
```

---

## 步骤 6：注册服务到 Program.cs

编辑 `Program.cs`，添加新服务注册：

```csharp
// W4 新增服务
builder.Services.AddSingleton<AIFieldExtractor>();
builder.Services.AddSingleton<ChatSessionRepository>();
builder.Services.AddSingleton<ChatService>();
```

---

## 步骤 7：实现前端对话界面

创建 `frontend/src/views/user/ChatFill.vue`：

```vue
<template>
  <div class="chat-fill">
    <el-page-header title="返回" @back="goBack" :content="`对话填写 - ${templateName}`" />

    <el-card class="chat-container">
      <!-- 进度条 -->
      <div class="progress-bar">
        <el-progress :percentage="Math.round(progress * 100)" :color="progressColor" />
        <span class="progress-text">已收集 {{ collectedFieldCount }} / {{ totalFieldCount }} 个字段</span>
      </div>

      <!-- 消息列表 -->
      <div class="message-list" ref="messageListRef">
        <div
          v-for="(msg, index) in messages"
          :key="index"
          :class="['message', msg.role === 'user' ? 'message-user' : 'message-assistant']"
        >
          <div class="message-avatar">
            {{ msg.role === 'user' ? '我' : 'AI' }}
          </div>
          <div class="message-content">
            <div class="message-text">{{ msg.content }}</div>
            <div class="message-time">{{ formatTime(msg.timestamp) }}</div>
          </div>
        </div>

        <!-- 正在输入指示器 -->
        <div v-if="isTyping" class="message message-assistant">
          <div class="message-avatar">AI</div>
          <div class="message-content">
            <div class="typing-indicator">
              <span></span><span></span><span></span>
            </div>
          </div>
        </div>
      </div>

      <!-- 输入框 -->
      <div class="input-area">
        <el-input
          v-model="userInput"
          type="textarea"
          :rows="3"
          placeholder="请输入您的回答..."
          @keydown.ctrl.enter="sendMessage"
          :disabled="isCompleted || isSending"
        />
        <div class="input-actions">
          <el-button type="primary" @click="sendMessage" :loading="isSending" :disabled="isCompleted">
            发送 (Ctrl+Enter)
          </el-button>
          <el-button v-if="isCompleted" type="success" @click="generateDocument">
            生成文档
          </el-button>
        </div>

        <!-- 快捷指令提示 -->
        <div class="shortcut-hint">
          💡 提示：你可以说"我要一次性填完"、"下载模板"等快捷指令
        </div>
      </div>
    </el-card>

    <!-- 已收集字段预览 -->
    <el-card class="collected-fields" v-if="Object.keys(collectedFields).length > 0">
      <template #header>
        <span>已收集字段</span>
      </template>
      <el-descriptions :column="2" border size="small">
        <el-descriptions-item
          v-for="(value, key) in collectedFields"
          :key="key"
          :label="key"
        >
          {{ value }}
        </el-descriptions-item>
      </el-descriptions>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import axios from 'axios'

const router = useRouter()
const route = useRoute()

const templateId = route.params.templateId as string
const templateName = ref('')
const sessionId = ref('')
const messages = ref<any[]>([])
const userInput = ref('')
const isSending = ref(false)
const isTyping = ref(false)
const isCompleted = ref(false)
const progress = ref(0)
const collectedFields = ref<Record<string, string>>({})
const collectedFieldCount = ref(0)
const totalFieldCount = ref(0)
const messageListRef = ref<HTMLElement>()

const progressColor = computed(() => {
  if (progress.value < 0.3) return '#f56c6c'
  if (progress.value < 0.7) return '#e6a23c'
  return '#67c23a'
})

const startSession = async () => {
  try {
    const response = await axios.post('/api/chat/start', { templateId })
    
    sessionId.value = response.data.sessionId
    
    // 添加欢迎消息
    messages.value.push({
      role: 'assistant',
      content: response.data.message,
      timestamp: new Date()
    })

    // 获取模板信息
    const templateResponse = await axios.get(`/api/template/${templateId}`)
    templateName.value = templateResponse.data.name
    totalFieldCount.value = templateResponse.data.fields.length

    await scrollToBottom()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '启动会话失败')
  }
}

const sendMessage = async () => {
  if (!userInput.value.trim() || isSending.value) return

  const message = userInput.value.trim()
  userInput.value = ''

  // 添加用户消息
  messages.value.push({
    role: 'user',
    content: message,
    timestamp: new Date()
  })

  await scrollToBottom()

  // 发送消息（流式响应）
  isSending.value = true
  isTyping.value = true

  try {
    await sendMessageWithStream(message)
  } catch (error: any) {
    ElMessage.error('发送失败')
    isTyping.value = false
  } finally {
    isSending.value = false
  }
}

const sendMessageWithStream = async (message: string) => {
  const response = await fetch('/api/chat/message/stream', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId: sessionId.value, message })
  })

  if (!response.body) {
    throw new Error('No response body')
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let currentMessage = ''

  // 添加一个空的 AI 消息（用于流式填充）
  messages.value.push({
    role: 'assistant',
    content: '',
    timestamp: new Date()
  })
  
  const messageIndex = messages.value.length - 1

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n\n')
    buffer = lines.pop() || ''

    for (const line of lines) {
      if (!line.trim()) continue

      const eventMatch = line.match(/^event: (.+)$/m)
      const dataMatch = line.match(/^data: (.+)$/m)

      if (!eventMatch || !dataMatch) continue

      const eventType = eventMatch[1]
      const data = JSON.parse(dataMatch[1])

      if (eventType === 'message') {
        // 流式追加文本
        currentMessage += data.chunk
        messages.value[messageIndex].content = currentMessage
        await scrollToBottom()
      } else if (eventType === 'metadata') {
        // 更新元数据
        if (data.extractedFields) {
          Object.assign(collectedFields.value, data.extractedFields)
          collectedFieldCount.value = Object.keys(collectedFields.value).length
        }
        if (data.progress !== undefined) {
          progress.value = data.progress
        }
        if (data.isCompleted) {
          isCompleted.value = true
        }
      } else if (eventType === 'done') {
        isTyping.value = false
      } else if (eventType === 'error') {
        ElMessage.error(data.message)
        isTyping.value = false
      }
    }
  }
}

const generateDocument = async () => {
  try {
    // 构建生成请求
    const request = {
      templateId: templateId,
      fields: Object.keys(collectedFields.value).map((key) => ({
        name: key,
        value: collectedFields.value[key]
      })),
      tables: []
    }

    const response = await axios.post('/api/generate', request)

    if (response.data.success) {
      ElMessage.success('文档生成成功')
      
      // 下载文档
      const downloadUrl = response.data.downloadUrl
      const downloadResponse = await axios.get(downloadUrl, { responseType: 'blob' })
      const url = window.URL.createObjectURL(new Blob([downloadResponse.data]))
      const link = document.createElement('a')
      link.href = url
      link.setAttribute('download', response.data.fileName)
      document.body.appendChild(link)
      link.click()
      link.remove()
    }
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '生成文档失败')
  }
}

const scrollToBottom = async () => {
  await nextTick()
  if (messageListRef.value) {
    messageListRef.value.scrollTop = messageListRef.value.scrollHeight
  }
}

const formatTime = (date: Date) => {
  return new Date(date).toLocaleTimeString('zh-CN', {
    hour: '2-digit',
    minute: '2-digit'
  })
}

const goBack = () => {
  router.push('/')
}

onMounted(() => {
  startSession()
})
</script>

<style scoped>
.chat-fill {
  padding: 20px;
  max-width: 1200px;
  margin: 0 auto;
}

.chat-container {
  height: calc(100vh - 250px);
  display: flex;
  flex-direction: column;
}

.progress-bar {
  margin-bottom: 15px;
}

.progress-text {
  font-size: 12px;
  color: #666;
  margin-top: 5px;
  display: block;
}

.message-list {
  flex: 1;
  overflow-y: auto;
  padding: 15px;
  background: #f5f5f5;
  border-radius: 4px;
  margin-bottom: 15px;
}

.message {
  display: flex;
  margin-bottom: 20px;
  animation: fadeIn 0.3s;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.message-user {
  flex-direction: row-reverse;
}

.message-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: #409eff;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  flex-shrink: 0;
}

.message-user .message-avatar {
  background: #67c23a;
}

.message-content {
  max-width: 70%;
  margin: 0 10px;
}

.message-user .message-content {
  text-align: right;
}

.message-text {
  background: white;
  padding: 10px 15px;
  border-radius: 8px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
  word-wrap: break-word;
  white-space: pre-wrap;
}

.message-user .message-text {
  background: #409eff;
  color: white;
}

.message-time {
  font-size: 11px;
  color: #999;
  margin-top: 5px;
}

.typing-indicator {
  display: flex;
  gap: 4px;
  padding: 10px 15px;
}

.typing-indicator span {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #ccc;
  animation: typing 1.4s infinite;
}

.typing-indicator span:nth-child(2) {
  animation-delay: 0.2s;
}

.typing-indicator span:nth-child(3) {
  animation-delay: 0.4s;
}

@keyframes typing {
  0%, 60%, 100% {
    opacity: 0.4;
  }
  30% {
    opacity: 1;
  }
}

.input-area {
  margin-top: 10px;
}

.input-actions {
  margin-top: 10px;
  display: flex;
  gap: 10px;
}

.shortcut-hint {
  margin-top: 10px;
  font-size: 12px;
  color: #999;
}

.collected-fields {
  margin-top: 20px;
}
</style>
```

### 添加路由

编辑 `frontend/src/router/index.ts`，添加对话路由：

```typescript
{
  path: '/chat/:templateId',
  name: 'ChatFill',
  component: () => import('../views/user/ChatFill.vue')
}
```

---

## 步骤 8：验收测试

### 8.1 启动服务

```powershell
# 后端
cd c:\gitrepos\FrameworkAgentMVP\backend\FrameAgentWordFill
dotnet run

# 前端
cd c:\gitrepos\FrameworkAgentMVP\frontend
npm run dev
```

### 8.2 测试 AI 对话填充

1. 访问模板列表页
2. 选择一个模板，点击「对话填写」
3. 开始对话测试

**测试场景 1：自然语言输入**
```
AI: 您好！我是文档填写助手，将帮您填写【项目申请表】...首先，请告诉我项目名称是什么？
用户: 我们要做一个智能文档填充系统
AI: 好的，已记录：项目名称=智能文档填充系统。接下来，请告诉我项目负责人是谁？
用户: 负责人是张三，电话13800138000，邮箱zhangsan@example.com
AI: 好的，已记录：负责人=张三、负责人电话=13800138000、负责人邮箱=zhangsan@example.com。请告诉我项目预算是多少？
```

**预期结果**：
- ✅ AI 能从一句话中提取多个字段
- ✅ 进度条实时更新
- ✅ 已收集字段显示正确

**测试场景 2：快捷指令**
```
用户: 我要一次性填完
AI: 好的，以下是所有需要填写的字段：
   - 项目名称（text，必填）
   - 负责人（text，必填）
   - 负责人电话（phone，必填）
   ...
   您可以一次性提供所有信息，我会智能识别。
用户: 项目名是智能办公，负责人张三，电话13800138000，邮箱zhangsan@example.com，预算50万，开始日期2026-05-15
AI: 太好了！所有字段已填写完成，您可以点击"生成文档"按钮...
```

**预期结果**：
- ✅ 快捷指令被正确识别
- ✅ 批量提取所有字段
- ✅ 完成后显示生成文档按钮

**测试场景 3：数据验证**
```
用户: 电话是12345
AI: 抱歉，以下字段格式不正确：
   负责人电话：电话号码格式不正确
   请重新输入。
```

**预期结果**：
- ✅ 格式错误被捕获
- ✅ 提示清晰友好

**测试场景 4：流式对话**
- 观察 AI 回复是否有打字效果
- 确认消息逐字显示

**预期结果**：
- ✅ 流式响应正常工作
- ✅ 打字效果流畅自然

---

## ✅ W4 验收清单

- [ ] WordFillAgent Prompt 设计完成
- [ ] AI 字段提取工具实现完成（LLM 驱动）
- [ ] 快捷指令识别功能实现完成
- [ ] 表格数据提取功能实现完成
- [ ] 对话会话数据访问层实现完成
- [ ] 对话服务（AI 引导引擎）实现完成
- [ ] 流式对话接口（SSE）实现完成
- [ ] 前端对话界面实现完成
- [ ] 自然语言输入测试通过
- [ ] 批量字段提取测试通过
- [ ] 快捷指令识别测试通过
- [ ] 数据验证测试通过
- [ ] 流式响应测试通过（打字效果）
- [ ] 进度条实时更新
- [ ] 会话保存和恢复正常
- [ ] 完成后生成文档功能正常

---

## 🔧 常见问题排查

### 问题 1：AI 无法提取字段

**现象**：用户输入后，AI 没有提取任何字段

**排查步骤**：
1. 检查 LLM 服务是否正常（`/test/llm`）
2. 查看后端日志中的 AI 响应
3. 检查 Prompt 是否正确

**解决方案**：
- 优化 Prompt，提供更多示例
- 检查 LLM 返回的 JSON 格式
- 添加容错处理

### 问题 2：流式响应不工作

**现象**：消息直接全量显示，没有打字效果

**原因**：
- SSE 配置不正确
- 浏览器不支持 EventSource
- 网络代理问题

**解决方案**：
确保后端正确设置 SSE 响应头：
```csharp
Response.ContentType = "text/event-stream";
Response.Headers.Add("Cache-Control", "no-cache");
Response.Headers.Add("Connection", "keep-alive");
```

### 问题 3：会话丢失

**现象**：刷新页面后会话数据丢失

**解决方案**：
在前端添加会话恢复功能：
```typescript
// 保存会话 ID 到 localStorage
localStorage.setItem('currentSessionId', sessionId.value)

// 页面加载时恢复会话
const savedSessionId = localStorage.getItem('currentSessionId')
if (savedSessionId) {
  sessionId.value = savedSessionId
  await loadSession(savedSessionId)
}
```

### 问题 4：AI 回复速度慢

**现象**：每次回复需要等待很久

**原因**：
- LLM 推理速度慢
- Prompt 太长
- 网络延迟

**解决方案**：
- 优化 Prompt，减少不必要的上下文
- 使用更快的模型
- 添加超时处理

### 问题 5：快捷指令识别不准确

**现象**：用户输入快捷指令后，AI 没有识别

**解决方案**：
- 扩展快捷指令的同义词库
- 使用更强的 LLM 模型
- 添加关键词匹配作为兜底

---

## 📝 下一步（W5）

W4 完成后，可以进入 W5：导入填充链路。下一步需要：
1. 实现 Excel/JSON/Word 文件解析
2. 实现字段智能匹配算法
3. 实现导入界面和可视化匹配

---

## 🎯 W4 总结

本周完成的核心功能：
1. ✅ AI 驱动的智能对话引擎（自然语言理解）
2. ✅ 智能字段提取（LLM 驱动）
3. ✅ 快捷指令识别
4. ✅ 对话会话管理（持久化存储）
5. ✅ 流式对话接口（SSE + 打字效果）
6. ✅ 完整的前端对话界面

技术要点：
- Microsoft Agent Framework + GitHub Copilot SDK
- Prompt 工程（字段提取、指令识别）
- 流式响应（Server-Sent Events）
- 自然语言处理（NLP）
- 会话状态管理

**🎉 至此，AI 智能对话填充功能已完成！这是整个项目最核心的功能模块。**

**用户体验亮点**：
- 💬 自然对话，像与真人助手交流
- 🤖 智能理解，从一句话中提取多个字段
- ⚡ 快捷指令，提高填写效率
- 📊 实时进度，清晰可见
- 💾 断点续聊，随时保存恢复


