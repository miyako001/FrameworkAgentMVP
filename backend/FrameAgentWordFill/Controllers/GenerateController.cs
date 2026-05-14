using Microsoft.AspNetCore.Mvc;
using FrameAgentWordFill.Services;

namespace FrameAgentWordFill.Controllers;

[ApiController]
[Route("api/generate")]
public class GenerateController : ControllerBase
{
    private readonly GenerateService _generateService;
    private readonly ILogger<GenerateController> _logger;

    public GenerateController(GenerateService generateService, ILogger<GenerateController> logger)
    {
        _generateService = generateService;
        _logger = logger;
    }

    /// <summary>
    /// 生成文档
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GenerateDocument([FromBody] GenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            return BadRequest(new { error = "TemplateId 不能为空" });
        }

        var result = await _generateService.GenerateDocumentAsync(request);

        if (!result.Success)
        {
            return BadRequest(new
            {
                error = result.ErrorMessage,
                validationErrors = result.ValidationErrors
            });
        }

        return Ok(new
        {
            success = true,
            fileName = result.OutputFileName,
            downloadUrl = result.DownloadUrl
        });
    }

    /// <summary>
    /// 下载生成的文档
    /// </summary>
    [HttpGet("download/{fileName}")]
    public IActionResult DownloadDocument(string fileName)
    {
        var filePath = _generateService.GetOutputFilePath(fileName);
        if (filePath == null || !System.IO.File.Exists(filePath))
        {
            return NotFound(new { error = "文件不存在" });
        }

        var fileBytes = System.IO.File.ReadAllBytes(filePath);
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }
}
