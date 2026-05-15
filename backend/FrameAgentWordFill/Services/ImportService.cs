using FrameAgentWordFill.Models.Import;
using FrameAgentWordFill.Models.AIExtraction;
using FrameAgentWordFill.Repositories;
using FrameAgentWordFill.Tools;
using FrameAgentWordFill.Tools.FileParser;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace FrameAgentWordFill.Services;

/// <summary>
/// 导入填充服务（业务逻辑层）
/// </summary>
public sealed class ImportService
{
    private readonly ImportSessionRepository _sessionRepository;
    private readonly TemplateRepository _templateRepository;
    private readonly FileStorageService _fileStorage;
    private readonly ExcelParser _excelParser;
    private readonly JsonParser _jsonParser;
    private readonly WordTableParser _wordParser;
    private readonly PdfParser _pdfParser;
    private readonly PlainTextParser _plainTextParser;
    private readonly ImageOcrParser _imageOcrParser;
    private readonly AIBatchExtractor _aiBatchExtractor;
    private readonly FieldMatcher _fieldMatcher;
    private readonly GenerateService _generateService;
    private readonly AIAgent _importAgent;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        ImportSessionRepository sessionRepository,
        TemplateRepository templateRepository,
        FileStorageService fileStorage,
        ExcelParser excelParser,
        JsonParser jsonParser,
        WordTableParser wordParser,
        PdfParser pdfParser,
        PlainTextParser plainTextParser,
        ImageOcrParser imageOcrParser,
        AIBatchExtractor aiBatchExtractor,
        FieldMatcher fieldMatcher,
        GenerateService generateService,
        [FromKeyedServices("import")] AIAgent importAgent,
        ILogger<ImportService> logger)
    {
        _sessionRepository = sessionRepository;
        _templateRepository = templateRepository;
        _fileStorage = fileStorage;
        _excelParser = excelParser;
        _jsonParser = jsonParser;
        _wordParser = wordParser;
        _pdfParser = pdfParser;
        _plainTextParser = plainTextParser;
        _imageOcrParser = imageOcrParser;
        _aiBatchExtractor = aiBatchExtractor;
        _fieldMatcher = fieldMatcher;
        _generateService = generateService;
        _importAgent = importAgent;
        _logger = logger;
    }

    /// <summary>
    /// 上传文件并创建导入会话
    /// </summary>
    public async Task<(int SessionId, string ErrorMessage)> UploadFileAndCreateSessionAsync(
        string templateId,
        IFormFile file)
    {
        try
        {
            var template = await _templateRepository.GetTemplateByIdAsync(templateId);
            if (template == null)
                return (-1, "模板不存在");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileType = ext switch
            {
                ".xlsx" or ".xls" => "Excel",
                ".json" => "JSON",
                ".docx" or ".doc" => "Word",
                ".pdf" => "PDF",
                ".txt" => "Text",
                ".csv" => "CSV",
                ".md" => "Markdown",
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" => "Image",
                _ => null
            };

            if (fileType == null)
                return (-1, "不支持的文件类型，仅支持 Excel、JSON、Word、PDF、图片、TXT、CSV、Markdown");

            var relativeName = await _fileStorage.SaveUploadFileAsync(file);

            var session = new ImportSession
            {
                TemplateId = templateId,
                FileType = fileType,
                FilePath = relativeName,
                Status = "Parsing"
            };

            var sessionId = await _sessionRepository.CreateSessionAsync(session);
            _logger.LogInformation("导入会话创建成功：SessionId={SessionId}，FileType={FileType}", sessionId, fileType);
            return (sessionId, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传文件失败");
            return (-1, $"上传失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 解析文件并匹配字段
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> ParseAndMatchFieldsAsync(int sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetSessionByIdAsync(sessionId);
            if (session == null)
                return (false, "导入会话不存在");

            var template = await _templateRepository.GetTemplateByIdAsync(session.TemplateId);
            if (template == null)
                return (false, "模板不存在");

            var fullPath = _fileStorage.GetUploadFilePath(session.FilePath);

            ParsedFileData parsedData = session.FileType switch
            {
                "Excel" => await _excelParser.ParseAsync(fullPath),
                "JSON" => await _jsonParser.ParseAsync(fullPath),
                "Word" => await _wordParser.ParseAsync(fullPath),
                _ => throw new InvalidOperationException($"不支持的文件类型：{session.FileType}")
            };

            if (parsedData.Errors.Count > 0)
            {
                var errorMsg = string.Join("; ", parsedData.Errors);
                await _sessionRepository.UpdateSessionStatusAsync(sessionId, "Failed", 0, 0, errorMsg);
                return (false, errorMsg);
            }

            var fieldMappings = _fieldMatcher.MatchFields(parsedData.Fields, template.Fields);
            await _sessionRepository.SaveFieldMappingsAsync(sessionId, fieldMappings);

            foreach (var (tableName, tableData) in parsedData.Tables)
                await _sessionRepository.SaveTableDataAsync(sessionId, tableName, tableData);

            var matchedCount = fieldMappings.Count(m => m.MatchConfidence >= 70);
            var unmatchedCount = fieldMappings.Count(m => m.MatchConfidence < 70);
            await _sessionRepository.UpdateSessionStatusAsync(sessionId, "WaitingConfirm", matchedCount, unmatchedCount);

            _logger.LogInformation("文件解析完成：匹配 {Matched} 个字段，未匹配 {Unmatched} 个字段",
                matchedCount, unmatchedCount);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析文件失败");
            await _sessionRepository.UpdateSessionStatusAsync(sessionId, "Failed", 0, 0, ex.Message);
            return (false, $"解析失败：{ex.Message}");
        }
    }

    /// <summary>
    /// W10: 使用 ImportAgent 批量提取并匹配字段（Agent 自主决策工具调用）。
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> ParseAndMatchFieldsWithAiAsync(int sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return (false, "导入会话不存在");
            }

            var template = await _templateRepository.GetTemplateByIdAsync(session.TemplateId);
            if (template == null)
            {
                return (false, "模板不存在");
            }

            var fullPath = _fileStorage.GetUploadFilePath(session.FilePath);
            var parsedContent = await ParseDocumentContentAsync(session.FileType, fullPath);

            // W10: 尝试使用 ImportAgent 自主决策
            List<ImportFieldMapping> mappings;
            try
            {
                var documentContentJson = JsonSerializer.Serialize(parsedContent);
                var templateFieldsJson = JsonSerializer.Serialize(template.Fields);
                var prompt = $"文档内容：{documentContentJson}\n模板字段：{templateFieldsJson}";

                var agentSession = await _importAgent.CreateSessionAsync();
                var agentResponse = await _importAgent.RunAsync(
                    new ChatMessage(ChatRole.User, prompt),
                    agentSession,
                    new AgentRunOptions(),
                    default);

                mappings = ParseImportAgentResult(agentResponse);
                _logger.LogInformation("ImportAgent 提取完成：SessionId={SessionId}, Mappings={Count}",
                    sessionId, mappings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportAgent 调用失败，降级到直接 Tool 调用。SessionId={SessionId}", sessionId);
                var (aiFields, _) = await _aiBatchExtractor.ExtractFieldsAsync(parsedContent, template.Fields);
                mappings = _fieldMatcher.MatchAIExtractedFields(aiFields, template.Fields);
            }

            // AI 结果为空时，回退到原有规则解析路径，保证可用性。
            if (mappings.Count == 0)
            {
                _logger.LogWarning("提取结果为空，回退到规则解析。SessionId={SessionId}", sessionId);
                return await ParseAndMatchFieldsAsync(sessionId);
            }

            await _sessionRepository.SaveFieldMappingsAsync(sessionId, mappings);

            var parsedTables = ConvertTablesToImportStructure(parsedContent.Tables);
            foreach (var (tableName, tableRows) in parsedTables)
            {
                await _sessionRepository.SaveTableDataAsync(sessionId, tableName, tableRows);
            }

            var matchedCount = mappings.Count(m => m.MatchConfidence >= 70);
            var unmatchedCount = mappings.Count(m => m.MatchConfidence < 70);
            var warningText = parsedContent.Warnings.Count > 0
                ? $"解析警告: {string.Join(" | ", parsedContent.Warnings.Take(3))}"
                : null;

            await _sessionRepository.UpdateSessionStatusAsync(sessionId, "WaitingConfirm", matchedCount, unmatchedCount, warningText);

            _logger.LogInformation(
                "AI 提取完成（W10）：SessionId={SessionId}, Matched={Matched}, Unmatched={Unmatched}",
                sessionId, matchedCount, unmatchedCount);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 提取失败");
            await _sessionRepository.UpdateSessionStatusAsync(sessionId, "Failed", 0, 0, ex.Message);
            return (false, $"AI 提取失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取字段匹配结果
    /// </summary>
    public async Task<(ImportSession? Session, List<ImportFieldMapping> Mappings)> GetFieldMappingsAsync(int sessionId)
    {
        var session = await _sessionRepository.GetSessionByIdAsync(sessionId);
        var mappings = await _sessionRepository.GetFieldMappingsAsync(sessionId);
        return (session, mappings);
    }

    /// <summary>
    /// 更新字段映射（用户手动调整）
    /// </summary>
    public async Task UpdateFieldMappingAsync(int mappingId, string templateFieldName)
    {
        await _sessionRepository.UpdateFieldMappingAsync(mappingId, templateFieldName);
        _logger.LogInformation("字段映射已更新：MappingId={MappingId}", mappingId);
    }

    /// <summary>
    /// 生成文档（基于导入数据）
    /// </summary>
    public async Task<GenerateResult> GenerateDocumentAsync(int sessionId)
    {
        var session = await _sessionRepository.GetSessionByIdAsync(sessionId);
        if (session == null)
            return new GenerateResult { Success = false, ErrorMessage = "导入会话不存在" };

        var mappings = await _sessionRepository.GetFieldMappingsAsync(sessionId);
        var tableData = await _sessionRepository.GetTableDataAsync(sessionId);

        var request = new GenerateRequest
        {
            TemplateId = session.TemplateId,
            Fields = mappings
                .Where(m => m.TemplateFieldName != null && m.FieldType == "Normal")
                .Select(m => new FieldValue { Name = m.TemplateFieldName!, Value = m.FieldValue ?? string.Empty })
                .ToList(),
            Tables = tableData
                .Select(kv => new TableData { Name = kv.Key, Rows = kv.Value })
                .ToList()
        };

        return await _generateService.GenerateDocumentAsync(request);
    }

    private async Task<ParsedDocumentContent> ParseDocumentContentAsync(string fileType, string fullPath)
    {
        return fileType switch
        {
            "Excel" => await _excelParser.ParseFullContentAsync(fullPath),
            "JSON" => await _jsonParser.ParseFullContentAsync(fullPath),
            "Word" => await _wordParser.ParseFullContentAsync(fullPath),
            "PDF" => await _pdfParser.ParseAsync(fullPath),
            "Text" => await _plainTextParser.ParseAsync(fullPath, "Text"),
            "CSV" => await _plainTextParser.ParseAsync(fullPath, "CSV"),
            "Markdown" => await _plainTextParser.ParseAsync(fullPath, "Markdown"),
            "Image" => await _imageOcrParser.ParseAsync(fullPath),
            _ => new ParsedDocumentContent
            {
                FileType = fileType,
                ParseQuality = 0,
                Warnings = { $"暂不支持此文件类型的 AI 解析：{fileType}" }
            }
        };
    }

    private static Dictionary<string, List<Dictionary<string, string>>> ConvertTablesToImportStructure(List<ParsedTable> tables)
    {
        var result = new Dictionary<string, List<Dictionary<string, string>>>();

        foreach (var table in tables)
        {
            if (table.Headers.Count == 0 || table.Rows.Count == 0)
            {
                continue;
            }

            var tableName = $"表格{table.TableIndex + 1}";
            var rows = new List<Dictionary<string, string>>();
            foreach (var row in table.Rows)
            {
                var rowData = new Dictionary<string, string>();
                for (var i = 0; i < Math.Min(table.Headers.Count, row.Count); i++)
                {
                    if (!string.IsNullOrWhiteSpace(table.Headers[i]))
                    {
                        rowData[table.Headers[i]] = row[i];
                    }
                }

                if (rowData.Count > 0)
                {
                    rows.Add(rowData);
                }
            }

            if (rows.Count > 0)
            {
                result[tableName] = rows;
            }
        }

        return result;
    }

    /// <summary>
    /// W10: 从 ImportAgent 响应中解析字段映射结果。
    /// Agent 被要求以 JSON 数组形式返回映射，格式：
    /// [{"sourceFieldName":"...","templateFieldName":"...","fieldValue":"...","matchConfidence":85,"matchMethod":"AI"}]
    /// </summary>
    private List<ImportFieldMapping> ParseImportAgentResult(AgentResponse agentResponse)
    {
        try
        {
            var text = agentResponse.Text ?? string.Empty;
            var jsonStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (jsonStart < 0) return new List<ImportFieldMapping>();
            var contentStart = text.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = text.IndexOf("```", contentStart, StringComparison.OrdinalIgnoreCase);
            if (jsonEnd <= contentStart) return new List<ImportFieldMapping>();
            var jsonBlock = text[contentStart..jsonEnd].Trim();
            var parsed = JsonSerializer.Deserialize<List<ImportFieldMapping>>(
                jsonBlock,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return parsed ?? new List<ImportFieldMapping>();
        }
        catch
        {
            return new List<ImportFieldMapping>();
        }
    }
}
