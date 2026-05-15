using FrameAgentWordFill.Models.Templates;
using FrameAgentWordFill.Services;
using Microsoft.AspNetCore.Mvc;

namespace FrameAgentWordFill.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplatesController : ControllerBase
{
    private readonly TemplateService _templateService;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(TemplateService templateService, ILogger<TemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// 上传模板
    /// </summary>
    /// <param name="file">模板文件</param>
    /// <param name="name">模板名称</param>
    /// <param name="description">描述</param>
    /// <param name="aiVerify">是否启用 AI 双轨比对（默认 false）</param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> UploadTemplate(
        IFormFile file,
        [FromForm] string name,
        [FromForm] string? description = null,
        [FromForm] bool aiVerify = false)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "请选择文件" });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { success = false, message = "请输入模板名称" });
        }

        var (success, templateId, parseResult, aiVerification) =
            await _templateService.UploadTemplateAsync(file, name, description, aiVerify);

        if (!success)
        {
            return BadRequest(new
            {
                success = false,
                message = "模板上传失败",
                parseResult = parseResult,
                aiVerification = aiVerification
            });
        }

        return Ok(new
        {
            success = true,
            message = "模板上传成功",
            templateId = templateId,
            parseResult = parseResult,
            aiVerification = aiVerification
        });
    }

    /// <summary>
    /// 获取所有模板列表（管理员）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllTemplates()
    {
        var templates = await _templateService.GetAllTemplatesAsync();
        return Ok(new
        {
            success = true,
            data = templates
        });
    }

    /// <summary>
    /// 获取启用的模板列表（用户）
    /// </summary>
    [HttpGet("enabled")]
    public async Task<IActionResult> GetEnabledTemplates()
    {
        var templates = await _templateService.GetEnabledTemplatesAsync();
        return Ok(new
        {
            success = true,
            data = templates
        });
    }

    /// <summary>
    /// 获取模板详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTemplateById(string id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            return NotFound(new { success = false, message = "模板不存在" });
        }

        return Ok(new
        {
            success = true,
            data = template
        });
    }

    /// <summary>
    /// 更新模板状态
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateTemplateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        var success = await _templateService.UpdateTemplateStatusAsync(id, request.Status);
        if (!success)
        {
            return BadRequest(new { success = false, message = "更新失败" });
        }

        return Ok(new { success = true, message = "状态更新成功" });
    }

    /// <summary>
    /// 更新字段配置
    /// </summary>
    [HttpPut("fields/{fieldId}")]
    public async Task<IActionResult> UpdateField(int fieldId, [FromBody] Field field)
    {
        if (field.Id != fieldId)
        {
            return BadRequest(new { success = false, message = "字段ID不匹配" });
        }

        var success = await _templateService.UpdateFieldAsync(field);
        if (!success)
        {
            return BadRequest(new { success = false, message = "更新失败" });
        }

        return Ok(new { success = true, message = "字段更新成功" });
    }

    /// <summary>
    /// 删除模板
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        var success = await _templateService.DeleteTemplateAsync(id);
        if (!success)
        {
            return BadRequest(new { success = false, message = "删除失败" });
        }

        return Ok(new { success = true, message = "模板已删除" });
    }

    /// <summary>
    /// 下载模板文件
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadTemplate(string id)
    {
        var (success, content, fileName) = await _templateService.DownloadTemplateAsync(id);
        if (!success || content == null || fileName == null)
        {
            return NotFound(new { success = false, message = "文件不存在" });
        }

        return File(content, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }
}

/// <summary>
/// 更新状态请求模型
/// </summary>
public class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
