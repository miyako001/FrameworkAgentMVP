using FrameAgentWordFill.Models.Templates;
using FrameAgentWordFill.Repositories;
using FrameAgentWordFill.Tools;

namespace FrameAgentWordFill.Services;

/// <summary>
/// 文档生成服务（业务逻辑层）
/// </summary>
public sealed class GenerateService
{
    private readonly TemplateRepository _templateRepository;
    private readonly FileStorageService _fileStorage;
    private readonly DocGenerator _docGenerator;
    private readonly DataValidator _dataValidator;
    private readonly ILogger<GenerateService> _logger;

    public GenerateService(
        TemplateRepository templateRepository,
        FileStorageService fileStorage,
        DocGenerator docGenerator,
        DataValidator dataValidator,
        ILogger<GenerateService> logger)
    {
        _templateRepository = templateRepository;
        _fileStorage = fileStorage;
        _docGenerator = docGenerator;
        _dataValidator = dataValidator;
        _logger = logger;
    }

    /// <summary>
    /// 生成文档
    /// </summary>
    /// <param name="request">生成请求</param>
    /// <returns>生成结果</returns>
    public async Task<GenerateResult> GenerateDocumentAsync(GenerateRequest request)
    {
        try
        {
            // 1. 获取模板
            var template = await _templateRepository.GetTemplateByIdAsync(request.TemplateId);
            if (template == null)
            {
                return new GenerateResult
                {
                    Success = false,
                    ErrorMessage = "模板不存在"
                };
            }

            // 2. 获取模板文件路径
            var templatePath = Path.Combine(_fileStorage.GetTemplatesPath(), template.FileName);
            if (!File.Exists(templatePath))
            {
                return new GenerateResult
                {
                    Success = false,
                    ErrorMessage = "模板文件不存在"
                };
            }

            // 3. 验证字段数据
            var validationResult = ValidateFieldData(template, request.Fields);
            if (!validationResult.IsValid)
            {
                return new GenerateResult
                {
                    Success = false,
                    ErrorMessage = "数据验证失败",
                    ValidationErrors = validationResult.Errors
                };
            }

            // 4. 准备字段数据（字段名 → 值）
            var fieldData = request.Fields.ToDictionary(
                f => f.Name,
                f => f.Value,
                StringComparer.OrdinalIgnoreCase
            );

            // 5. 准备表格数据（表格名 → 行列表）
            var tableData = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var tableRequest in request.Tables)
            {
                tableData[tableRequest.Name] = tableRequest.Rows;
            }

            // 6. 生成输出文件名
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputFileName = $"{template.Name}_{timestamp}.docx";
            var outputPath = Path.Combine(_fileStorage.GetOutputPath(), outputFileName);

            // 7. 生成文档
            var (success, errorMessage) = await _docGenerator.GenerateDocumentAsync(
                templatePath,
                outputPath,
                fieldData,
                tableData
            );

            if (!success)
            {
                return new GenerateResult
                {
                    Success = false,
                    ErrorMessage = errorMessage ?? "生成失败"
                };
            }

            _logger.LogInformation("文档生成成功: {OutputFileName}", outputFileName);

            return new GenerateResult
            {
                Success = true,
                OutputFileName = outputFileName,
                OutputPath = outputPath,
                DownloadUrl = $"/api/generate/download/{outputFileName}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成文档失败: {TemplateId}", request.TemplateId);
            return new GenerateResult
            {
                Success = false,
                ErrorMessage = $"生成失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 验证字段数据
    /// </summary>
    private ValidationResult ValidateFieldData(Template template, List<FieldValue> fieldValues)
    {
        var result = new ValidationResult { IsValid = true };
        var errors = new List<string>();

        // 构建字段值字典（字段名 → 值）
        var fieldValueDict = fieldValues.ToDictionary(
            f => f.Name,
            f => f.Value,
            StringComparer.OrdinalIgnoreCase
        );

        // 验证每个字段
        foreach (var field in template.Fields)
        {
            fieldValueDict.TryGetValue(field.Name, out var value);
            
            var (isValid, errorMessage) = _dataValidator.ValidateField(field, value ?? string.Empty);
            
            if (!isValid)
            {
                result.IsValid = false;
                errors.Add($"{field.Name}: {errorMessage}");
            }
        }

        result.Errors = errors;
        return result;
    }

    /// <summary>
    /// 获取生成的文档文件路径
    /// </summary>
    public string? GetOutputFilePath(string fileName)
    {
        var path = Path.Combine(_fileStorage.GetOutputPath(), fileName);
        return File.Exists(path) ? path : null;
    }
}

/// <summary>
/// 文档生成请求
/// </summary>
public sealed class GenerateRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public List<FieldValue> Fields { get; set; } = new();
    public List<TableData> Tables { get; set; } = new();
}

/// <summary>
/// 字段值
/// </summary>
public sealed class FieldValue
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 表格数据
/// </summary>
public sealed class TableData
{
    public string Name { get; set; } = string.Empty;
    public List<Dictionary<string, string>> Rows { get; set; } = new();
}

/// <summary>
/// 生成结果
/// </summary>
public sealed class GenerateResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? ValidationErrors { get; set; }
    public string? OutputFileName { get; set; }
    public string? OutputPath { get; set; }
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// 验证结果
/// </summary>
internal sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
