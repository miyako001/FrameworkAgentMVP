using FrameAgentWordFill.Models.Parsing;
using FrameAgentWordFill.Models.Templates;
using FrameAgentWordFill.Repositories;
using FrameAgentWordFill.Tools;

namespace FrameAgentWordFill.Services;

/// <summary>
/// 模板管理服务（业务逻辑层）
/// </summary>
public sealed class TemplateService
{
    private readonly TemplateRepository _repository;
    private readonly FileStorageService _fileStorage;
    private readonly TemplateParser _parser;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(
        TemplateRepository repository,
        FileStorageService fileStorage,
        TemplateParser parser,
        ILogger<TemplateService> logger)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _parser = parser;
        _logger = logger;
    }

    /// <summary>
    /// 上传并解析模板
    /// </summary>
    public async Task<(bool Success, string? TemplateId, TemplateParseResult? ParseResult)> UploadTemplateAsync(
        IFormFile file,
        string templateName,
        string? description = null)
    {
        try
        {
            // 1. 验证文件
            if (file.Length == 0 || !file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("无效的文件格式: {FileName}", file.FileName);
                return (false, null, null);
            }

            // 2. 生成文件名并保存文件
            var templateId = Guid.NewGuid().ToString();
            var fileName = $"{templateId}_{file.FileName}";
            var filePath = await _fileStorage.SaveTemplateAsync(file, fileName);

            // 3. 解析模板
            var parseResult = await _parser.ParseTemplateAsync(filePath);
            if (!parseResult.Success)
            {
                _logger.LogError("模板解析失败: {FileName}", file.FileName);
                // 删除已保存的文件
                File.Delete(filePath);
                return (false, null, parseResult);
            }

            // 4. 创建模板实体
            var template = new Template
            {
                Id = templateId,
                Name = templateName,
                FileName = fileName,
                OriginalFileName = file.FileName,
                Description = description,
                Status = "enabled",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // 5. 转换解析结果为字段和表格
            for (int i = 0; i < parseResult.Fields.Count; i++)
            {
                var fieldInfo = parseResult.Fields[i];
                template.Fields.Add(new Field
                {
                    TemplateId = templateId,
                    Name = fieldInfo.Name,
                    FieldType = fieldInfo.Type,
                    Required = fieldInfo.Required,
                    FieldOrder = i,
                    GuidePrompt = $"请输入{fieldInfo.Name}",
                    MissingPrompt = $"{fieldInfo.Name}不能为空",
                    InvalidPrompt = $"{fieldInfo.Name}格式不正确"
                });
            }

            foreach (var tableInfo in parseResult.Tables)
            {
                var tableDef = new TableDefinition
                {
                    TemplateId = templateId,
                    Name = tableInfo.Name,
                    RowType = tableInfo.RowType,
                    MaxRows = tableInfo.MaxRows,
                    GuidePrompt = $"请提供{tableInfo.Name}数据"
                };

                for (int i = 0; i < tableInfo.Columns.Count; i++)
                {
                    tableDef.Columns.Add(new TableColumn
                    {
                        Name = tableInfo.Columns[i].Name,
                        ColumnOrder = i
                    });
                }

                template.Tables.Add(tableDef);
            }

            // 6. 保存到数据库
            var success = await _repository.CreateTemplateAsync(template);
            if (!success)
            {
                // 删除已保存的文件
                File.Delete(filePath);
                return (false, null, null);
            }

            _logger.LogInformation("模板上传成功: {TemplateId}, 名称: {TemplateName}", templateId, templateName);
            return (true, templateId, parseResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传模板失败");
            return (false, null, null);
        }
    }

    /// <summary>
    /// 获取所有模板列表
    /// </summary>
    public async Task<List<Template>> GetAllTemplatesAsync()
    {
        return await _repository.GetAllTemplatesAsync();
    }

    /// <summary>
    /// 获取启用的模板列表（用户界面）
    /// </summary>
    public async Task<List<Template>> GetEnabledTemplatesAsync()
    {
        var templates = await _repository.GetAllTemplatesAsync();
        return templates.Where(t => t.Status == "enabled").ToList();
    }

    /// <summary>
    /// 获取模板详情
    /// </summary>
    public async Task<Template?> GetTemplateByIdAsync(string templateId)
    {
        return await _repository.GetTemplateByIdAsync(templateId);
    }

    /// <summary>
    /// 更新模板状态
    /// </summary>
    public async Task<bool> UpdateTemplateStatusAsync(string templateId, string status)
    {
        if (status != "enabled" && status != "disabled")
        {
            _logger.LogWarning("无效的状态值: {Status}", status);
            return false;
        }

        return await _repository.UpdateTemplateStatusAsync(templateId, status);
    }

    /// <summary>
    /// 更新字段配置
    /// </summary>
    public async Task<bool> UpdateFieldAsync(Field field)
    {
        return await _repository.UpdateFieldAsync(field);
    }

    /// <summary>
    /// 删除模板
    /// </summary>
    public async Task<bool> DeleteTemplateAsync(string templateId)
    {
        // 1. 获取模板信息
        var template = await _repository.GetTemplateByIdAsync(templateId);
        if (template == null)
        {
            _logger.LogWarning("模板不存在: {TemplateId}", templateId);
            return false;
        }

        // 2. 删除数据库记录
        var success = await _repository.DeleteTemplateAsync(templateId);
        if (!success)
            return false;

        // 3. 删除模板文件
        try
        {
            var filePath = Path.Combine(_fileStorage.GetTemplatesPath(), template.FileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("模板文件已删除: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除模板文件失败: {FileName}", template.FileName);
            // 文件删除失败不影响整体操作
        }

        return true;
    }

    /// <summary>
    /// 下载模板文件
    /// </summary>
    public async Task<(bool Success, byte[]? Content, string? FileName)> DownloadTemplateAsync(string templateId)
    {
        var template = await _repository.GetTemplateByIdAsync(templateId);
        if (template == null)
        {
            _logger.LogWarning("模板不存在: {TemplateId}", templateId);
            return (false, null, null);
        }

        var filePath = Path.Combine(_fileStorage.GetTemplatesPath(), template.FileName);
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("模板文件不存在: {FilePath}", filePath);
            return (false, null, null);
        }

        try
        {
            var content = await File.ReadAllBytesAsync(filePath);
            return (true, content, template.OriginalFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取模板文件失败: {FilePath}", filePath);
            return (false, null, null);
        }
    }
}
