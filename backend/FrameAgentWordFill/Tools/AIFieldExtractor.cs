using FrameAgentWordFill.Models.Chat;
using FrameAgentWordFill.Models.Templates;
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
            if (targetFields.Count == 0)
            {
                _logger.LogWarning("没有需要提取的字段");
                return new Dictionary<string, string>();
            }

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

            // 提取 JSON 部分
            var jsonString = ExtractJson(response);
            
            var result = JsonSerializer.Deserialize<ShortcutDetectionResult>(
                jsonString,
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

            // 提取 JSON 部分
            var jsonString = ExtractJson(response);

            var tableData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(
                jsonString,
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
    /// 解析 LLM 的 JSON 响应（字典格式）
    /// </summary>
    private Dictionary<string, string> ParseJsonResponse(string response)
    {
        try
        {
            // 提取 JSON 部分
            var jsonString = ExtractJson(response);

            // 解析 JSON
            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                jsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JSON 解析失败，响应内容: {Response}", response);
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// 从响应中提取 JSON 部分
    /// </summary>
    private string ExtractJson(string response)
    {
        // 尝试从 markdown 代码块中提取
        var jsonMatch = System.Text.RegularExpressions.Regex.Match(
            response,
            @"```(?:json)?\s*(\{.*?\}|\[.*?\])\s*```",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );

        if (jsonMatch.Success)
        {
            return jsonMatch.Groups[1].Value.Trim();
        }

        // 尝试直接提取 JSON 对象或数组
        var directMatch = System.Text.RegularExpressions.Regex.Match(
            response,
            @"(\{.*?\}|\[.*?\])",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );

        if (directMatch.Success)
        {
            return directMatch.Groups[1].Value.Trim();
        }

        // 如果都失败了，返回原始响应
        return response.Trim();
    }
}
