using Microsoft.AspNetCore.Mvc;
using FrameAgentWordFill.Services;
using System.Text;
using System.Text.Json;

namespace FrameAgentWordFill.Controllers;

[ApiController]
[Route("api/chat")]
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
    /// 发送消息（流式响应 - SSE）
    /// </summary>
    [HttpPost("message/stream")]
    public async Task SendMessageStream([FromBody] ChatMessageRequest request)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");
        Response.Headers.Add("X-Accel-Buffering", "no"); // 禁用 nginx 缓冲

        try
        {
            // 处理消息
            var response = await _chatService.ProcessMessageAsync(request.SessionId, request.Message);

            if (!response.Success)
            {
                await SendSseEventAsync("error", new { message = response.Message });
                await SendSseEventAsync("done", null);
                return;
            }

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
            await SendSseEventAsync("done", null);
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
    /// 获取会话状态
    /// </summary>
    [HttpGet("session/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        var (success, session, template) = await _chatService.GetSessionStateAsync(sessionId);

        if (!success || session == null || template == null)
        {
            return NotFound(new { error = "会话不存在" });
        }

        return Ok(new
        {
            sessionId = session.Id,
            templateId = session.TemplateId,
            templateName = template.Name,
            status = session.Status,
            collectedFields = session.CollectedFields,
            totalFields = template.Fields.Count,
            progress = (double)session.CollectedFields.Count / template.Fields.Count
        });
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
        var json = data != null ? JsonSerializer.Serialize(data) : "null";
        var sseData = $"event: {eventType}\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(sseData);
        await Response.Body.WriteAsync(bytes);
        await Response.Body.FlushAsync();
    }
}

/// <summary>
/// 开始会话请求
/// </summary>
public sealed class StartSessionRequest
{
    public string TemplateId { get; set; } = string.Empty;
}

/// <summary>
/// 聊天消息请求
/// </summary>
public sealed class ChatMessageRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
