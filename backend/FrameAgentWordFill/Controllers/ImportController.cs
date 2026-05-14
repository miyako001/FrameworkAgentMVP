using Microsoft.AspNetCore.Mvc;
using FrameAgentWordFill.Services;

namespace FrameAgentWordFill.Controllers;

/// <summary>
/// 导入填充 API
/// </summary>
[ApiController]
[Route("api/import")]
public sealed class ImportController : ControllerBase
{
    private readonly ImportService _importService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(ImportService importService, ILogger<ImportController> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    /// <summary>
    /// 上传文件并创建导入会话
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([FromForm] string templateId, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "文件不能为空" });

        if (string.IsNullOrWhiteSpace(templateId))
            return BadRequest(new { error = "模板ID不能为空" });

        var (sessionId, errorMessage) = await _importService.UploadFileAndCreateSessionAsync(templateId, file);

        if (sessionId == -1)
            return BadRequest(new { error = errorMessage });

        return Ok(new { sessionId, message = "文件上传成功" });
    }

    /// <summary>
    /// 解析文件并匹配字段
    /// </summary>
    [HttpPost("parse/{sessionId:int}")]
    public async Task<IActionResult> ParseFile(int sessionId, [FromQuery] bool useAI = false)
    {
        var (success, errorMessage) = useAI
            ? await _importService.ParseAndMatchFieldsWithAiAsync(sessionId)
            : await _importService.ParseAndMatchFieldsAsync(sessionId);

        if (!success)
            return BadRequest(new { error = errorMessage });

        return Ok(new
        {
            message = useAI ? "AI 提取与解析完成" : "文件解析完成",
            mode = useAI ? "AI" : "Rule"
        });
    }

    /// <summary>
    /// 获取字段匹配结果
    /// </summary>
    [HttpGet("mappings/{sessionId:int}")]
    public async Task<IActionResult> GetFieldMappings(int sessionId)
    {
        var (session, mappings) = await _importService.GetFieldMappingsAsync(sessionId);

        if (session == null)
            return NotFound(new { error = "导入会话不存在" });

        return Ok(new
        {
            session = new
            {
                session.SessionId,
                session.TemplateId,
                session.FileType,
                session.Status,
                session.MatchedFieldCount,
                session.UnmatchedFieldCount
            },
            mappings = mappings.Select(m => new
            {
                m.MappingId,
                m.SourceFieldName,
                m.TemplateFieldName,
                m.FieldValue,
                m.MatchConfidence,
                m.MatchMethod,
                m.IsUserConfirmed
            })
        });
    }

    /// <summary>
    /// 更新字段映射（用户手动调整）
    /// </summary>
    [HttpPut("mappings/{mappingId:int}")]
    public async Task<IActionResult> UpdateFieldMapping(int mappingId, [FromBody] UpdateMappingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateFieldName))
            return BadRequest(new { error = "模板字段名不能为空" });

        await _importService.UpdateFieldMappingAsync(mappingId, request.TemplateFieldName);
        return Ok(new { message = "字段映射已更新" });
    }

    /// <summary>
    /// 生成文档
    /// </summary>
    [HttpPost("generate/{sessionId:int}")]
    public async Task<IActionResult> GenerateDocument(int sessionId)
    {
        var result = await _importService.GenerateDocumentAsync(sessionId);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new
        {
            outputFileName = result.OutputFileName,
            downloadUrl = result.DownloadUrl,
            message = "文档生成成功"
        });
    }
}

public sealed record UpdateMappingRequest(string TemplateFieldName);
