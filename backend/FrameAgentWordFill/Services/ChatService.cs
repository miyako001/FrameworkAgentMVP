using FrameAgentWordFill.Models.Chat;
using FrameAgentWordFill.Models.Templates;
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
            var nextMessage = GenerateNextGuideMessage(session, template, validationErrors);

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
    /// 获取会话状态
    /// </summary>
    public async Task<(bool Success, ChatSession? Session, Template? Template)> GetSessionStateAsync(
        string sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetSessionAsync(sessionId);
            if (session == null)
            {
                return (false, null, null);
            }

            var template = await _templateRepository.GetTemplateByIdAsync(session.TemplateId);
            if (template == null)
            {
                return (false, null, null);
            }

            return (true, session, template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话状态失败: {SessionId}", sessionId);
            return (false, null, null);
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
    private string GenerateNextGuideMessage(
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
                Message = $"您可以点击这里下载模板文件：/api/templates/{template.Id}/download",
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
