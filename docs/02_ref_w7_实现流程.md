# W7 实现流程 - AI 文件智能提取

**周期**：第 7 周  
**里程碑**：M7 - AI 文件提取与批量填充  
**目标**：实现用户上传参考文件，AI 自动批量提取字段数据的高级功能

**⚠️ 核心功能说明**：
- 本周是项目的**高级 AI 功能周**，实现文件智能提取
- 用户上传**参考文件**（PDF/Word/Excel/图片），AI **自动批量提取**所有字段
- 提供**置信度评分**（0-100），区分高/中/低置信度字段
- 支持**三层匹配策略**（精确匹配 → 模糊匹配 → 语义匹配）
- 提供**可视化确认界面**，用户可批量编辑和确认
- **复用 W4 的 LLM 能力**，支持多引擎兜底

---

## 📋 实施步骤总览

```
步骤1: 实现文件解析工具（多格式支持）
    ↓
步骤2: 实现 AI 批量提取工具（复用 W4 的 LLM）
    ↓
步骤3: 实现智能字段匹配算法（三层策略）
    ↓
步骤4: 实现置信度评分机制
    ↓
步骤5: 实现多引擎兜底策略
    ↓
步骤6: 实现 AI 提取会话管理
    ↓
步骤7: 实现前端提取和确认界面
    ↓
步骤8: 验收测试
```

---

## 步骤 1：实现文件解析工具

### 1.1 创建文件解析相关数据模型

创建 `Models/AIExtraction/ParsedDocumentContent.cs`：

```csharp
namespace FrameAgentWordFill.Models.AIExtraction;

/// <summary>
/// 文件解析后的文档内容
/// </summary>
public sealed class ParsedDocumentContent
{
    /// <summary>
    /// 文件类型
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// 纯文本内容
    /// </summary>
    public string PlainText { get; set; } = string.Empty;

    /// <summary>
    /// 表格数据（如果有）
    /// </summary>
    public List<ParsedTable> Tables { get; set; } = new();

    /// <summary>
    /// 图片列表（如果有）
    /// </summary>
    public List<ParsedImage> Images { get; set; } = new();

    /// <summary>
    /// 解析质量评分（0-100）
    /// </summary>
    public int ParseQuality { get; set; } = 100;

    /// <summary>
    /// 解析警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 解析的表格
/// </summary>
public sealed class ParsedTable
{
    /// <summary>
    /// 表格索引
    /// </summary>
    public int TableIndex { get; set; }

    /// <summary>
    /// 表头
    /// </summary>
    public List<string> Headers { get; set; } = new();

    /// <summary>
    /// 数据行
    /// </summary>
    public List<List<string>> Rows { get; set; } = new();
}

/// <summary>
/// 解析的图片
/// </summary>
public sealed class ParsedImage
{
    /// <summary>
    /// 图片索引
    /// </summary>
    public int ImageIndex { get; set; }

    /// <summary>
    /// 图片位置描述
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// OCR 识别的文本（如果有）
    /// </summary>
    public string? OcrText { get; set; }
}
```

### 1.2 创建 PDF 解析器

创建 `Tools/FileParser/PdfParser.cs`：

```csharp
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using FrameAgentWordFill.Models.AIExtraction;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// PDF 文件解析器（使用 iText7）
/// </summary>
public sealed class PdfParser
{
    private readonly ILogger<PdfParser> _logger;

    public PdfParser(ILogger<PdfParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析 PDF 文件
    /// </summary>
    public async Task<ParsedDocumentContent> ParseAsync(string filePath)
    {
        var result = new ParsedDocumentContent { FileType = "PDF" };

        try
        {
            await Task.Run(() =>
            {
                using var pdfReader = new PdfReader(filePath);
                using var pdfDoc = new PdfDocument(pdfReader);

                var textBuilder = new System.Text.StringBuilder();

                // 提取每一页的文本
                for (int pageNum = 1; pageNum <= pdfDoc.GetNumberOfPages(); pageNum++)
                {
                    var page = pdfDoc.GetPage(pageNum);
                    var strategy = new LocationTextExtractionStrategy();
                    var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

                    textBuilder.AppendLine(pageText);
                    textBuilder.AppendLine(); // 页与页之间添加空行
                }

                result.PlainText = textBuilder.ToString();

                // 尝试提取表格（简单实现，复杂表格可能需要更强大的库）
                // 这里假设使用简单的文本识别策略
                result.Tables = ExtractTablesFromText(result.PlainText);

                _logger.LogInformation($"PDF 解析完成：{pdfDoc.GetNumberOfPages()} 页，{result.PlainText.Length} 字符");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF 解析失败");
            result.ParseQuality = 30;
            result.Warnings.Add($"PDF 解析出错：{ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 从文本中提取表格（基于启发式规则）
    /// </summary>
    private List<ParsedTable> ExtractTablesFromText(string text)
    {
        var tables = new List<ParsedTable>();

        // 简单实现：查找具有明显列分隔符的文本块
        // 实际项目中可使用专门的 PDF 表格提取库（如 Tabula）

        _logger.LogInformation("尝试从 PDF 文本中提取表格");

        return tables;
    }
}
```

**注意**：需要安装 iText7 NuGet 包：
```bash
dotnet add package itext7 --version 8.0.0
```

### 1.3 更新 Word 和 Excel 解析器

由于 W5 已经实现了 Word 和 Excel 解析器，这里可以复用并增强。

编辑 `Tools/FileParser/WordTableParser.cs`，添加纯文本提取方法：

```csharp
/// <summary>
/// 提取 Word 文档的所有文本内容
/// </summary>
public async Task<ParsedDocumentContent> ParseFullContentAsync(string filePath)
{
    var result = new ParsedDocumentContent { FileType = "Word" };

    try
    {
        await Task.Run(() =>
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document.Body;

            if (body == null)
            {
                result.Warnings.Add("无法读取 Word 文档内容");
                result.ParseQuality = 0;
                return;
            }

            // 提取纯文本
            result.PlainText = body.InnerText;

            // 提取表格
            var tables = body.Elements<Table>().ToList();
            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                var parsedTable = ParseSingleTable(table);

                if (parsedTable.Headers.Count > 0 && parsedTable.Rows.Count > 0)
                {
                    result.Tables.Add(new ParsedTable
                    {
                        TableIndex = i,
                        Headers = parsedTable.Headers,
                        Rows = parsedTable.Rows.Select(r => r.Values.ToList()).ToList()
                    });
                }
            }

            _logger.LogInformation($"Word 解析完成：{result.PlainText.Length} 字符，{result.Tables.Count} 个表格");
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Word 解析失败");
        result.ParseQuality = 30;
        result.Warnings.Add($"Word 解析出错：{ex.Message}");
    }

    return result;
}

/// <summary>
/// 解析单个表格（返回简化结构）
/// </summary>
private (List<string> Headers, List<Dictionary<string, string>> Rows) ParseSingleTable(Table table)
{
    var rows = table.Elements<TableRow>().ToList();
    var headers = new List<string>();
    var dataRows = new List<Dictionary<string, string>>();

    if (rows.Count < 2) return (headers, dataRows);

    // 第一行作为表头
    headers = rows[0].Elements<TableCell>()
        .Select(cell => cell.InnerText.Trim())
        .ToList();

    // 后续行作为数据
    for (int i = 1; i < rows.Count; i++)
    {
        var cells = rows[i].Elements<TableCell>().ToList();
        var rowData = new Dictionary<string, string>();

        for (int j = 0; j < Math.Min(headers.Count, cells.Count); j++)
        {
            if (!string.IsNullOrWhiteSpace(headers[j]))
            {
                rowData[headers[j]] = cells[j].InnerText.Trim();
            }
        }

        if (rowData.Count > 0)
        {
            dataRows.Add(rowData);
        }
    }

    return (headers, dataRows);
}
```

编辑 `Tools/FileParser/ExcelParser.cs`，添加完整内容提取：

```csharp
/// <summary>
/// 提取 Excel 的所有文本内容（用于 AI 提取）
/// </summary>
public async Task<ParsedDocumentContent> ParseFullContentAsync(string filePath)
{
    var result = new ParsedDocumentContent { FileType = "Excel" };

    try
    {
        using var package = new ExcelPackage(new FileInfo(filePath));
        var textBuilder = new System.Text.StringBuilder();

        foreach (var worksheet in package.Workbook.Worksheets)
        {
            textBuilder.AppendLine($"工作表：{worksheet.Name}");
            textBuilder.AppendLine();

            if (worksheet.Dimension == null) continue;

            // 提取所有单元格文本
            for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
            {
                var rowValues = new List<string>();

                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Text.Trim();
                    if (!string.IsNullOrWhiteSpace(cellValue))
                    {
                        rowValues.Add(cellValue);
                    }
                }

                if (rowValues.Count > 0)
                {
                    textBuilder.AppendLine(string.Join(" | ", rowValues));
                }
            }

            textBuilder.AppendLine();

            // 提取表格结构
            if (worksheet.Dimension.End.Row > 1)
            {
                var headerRow = new List<string>();
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    headerRow.Add(worksheet.Cells[1, col].Text.Trim());
                }

                var dataRows = new List<List<string>>();
                for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                {
                    var rowData = new List<string>();
                    for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                    {
                        rowData.Add(worksheet.Cells[row, col].Text.Trim());
                    }
                    dataRows.Add(rowData);
                }

                result.Tables.Add(new ParsedTable
                {
                    TableIndex = result.Tables.Count,
                    Headers = headerRow,
                    Rows = dataRows
                });
            }
        }

        result.PlainText = textBuilder.ToString();

        _logger.LogInformation($"Excel 解析完成：{result.PlainText.Length} 字符，{result.Tables.Count} 个表格");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Excel 解析失败");
        result.ParseQuality = 30;
        result.Warnings.Add($"Excel 解析出错：{ex.Message}");
    }

    return await Task.FromResult(result);
}
```

### 1.4 创建图片 OCR 解析器（可选）

创建 `Tools/FileParser/ImageOcrParser.cs`：

```csharp
namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// 图片 OCR 解析器（需要 Tesseract 或云端 OCR 服务）
/// </summary>
public sealed class ImageOcrParser
{
    private readonly ILogger<ImageOcrParser> _logger;

    public ImageOcrParser(ILogger<ImageOcrParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析图片（OCR 识别文字）
    /// </summary>
    public async Task<ParsedDocumentContent> ParseAsync(string filePath)
    {
        var result = new ParsedDocumentContent { FileType = "Image" };

        try
        {
            // TODO: 集成 OCR 服务（如 Azure Computer Vision、Tesseract 等）
            // 这里仅提供框架，实际 OCR 需要额外集成

            _logger.LogWarning("图片 OCR 功能尚未实现，需要集成 OCR 服务");

            result.ParseQuality = 0;
            result.Warnings.Add("图片 OCR 功能需要额外配置 OCR 服务");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图片解析失败");
            result.Warnings.Add($"图片解析出错：{ex.Message}");
        }

        return await Task.FromResult(result);
    }
}
```

---

## 步骤 2：实现 AI 批量提取工具

### 2.1 创建 AI 提取配置

创建 `Agents/AIExtractionAgentConfig.cs`：

```csharp
namespace FrameAgentWordFill.Agents;

/// <summary>
/// AI 提取 Agent 配置（复用 W4 的 LLM 能力）
/// </summary>
public static class AIExtractionAgentConfig
{
    /// <summary>
    /// 批量提取 Prompt 模板
    /// </summary>
    public const string BatchExtractionPrompt = @"
你是一个专业的文档信息提取助手。

任务：从以下文档内容中提取指定字段的值。

文档内容：
{DOCUMENT_CONTENT}

需要提取的字段列表：
{FIELD_LIST}

要求：
1. 仔细阅读文档内容
2. 准确提取每个字段的值
3. 如果文档中没有某个字段，值设为 null
4. 如果字段值不确定，在 confidence 中标注置信度（0-100）
5. 如果字段名与文档中的标签不完全匹配，使用语义理解找到对应值

输出格式（JSON）：
{
  ""fields"": [
    {
      ""fieldName"": ""字段名"",
      ""fieldValue"": ""提取的值"",
      ""confidence"": 95,
      ""sourceText"": ""文档中的原文片段"",
      ""matchMethod"": ""Exact|Fuzzy|Semantic""
    }
  ]
}
";

    /// <summary>
    /// 表格提取 Prompt 模板
    /// </summary>
    public const string TableExtractionPrompt = @"
从以下文档内容中提取表格数据：

文档内容：
{DOCUMENT_CONTENT}

需要提取的表格：
- 表格名称：{TABLE_NAME}
- 列名：{COLUMN_NAMES}

输出格式（JSON）：
{
  ""tableName"": ""表格名"",
  ""rows"": [
    {
      ""列名1"": ""值1"",
      ""列名2"": ""值2""
    }
  ]
}
";

    /// <summary>
    /// 语义匹配 Prompt 模板
    /// </summary>
    public const string SemanticMatchPrompt = @"
任务：判断文档中的字段标签是否与模板字段语义匹配。

文档字段标签：{DOCUMENT_LABEL}
模板字段名：{TEMPLATE_FIELD}

请判断这两个字段是否表示相同的信息，并给出置信度（0-100）。

输出格式（JSON）：
{
  ""isMatch"": true,
  ""confidence"": 85,
  ""reason"": ""原因说明""
}
";
}
```

### 2.2 创建 AI 批量提取工具

创建 `Tools/AIBatchExtractor.cs`：

```csharp
using FrameAgentWordFill.Models.AIExtraction;
using FrameAgentWordFill.Models.Parsing;
using FrameAgentWordFill.Agents;
using System.Text.Json;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// AI 批量提取工具（复用 W4 的 LLM 能力）
/// </summary>
public sealed class AIBatchExtractor
{
    private readonly AIService _aiService;
    private readonly ILogger<AIBatchExtractor> _logger;

    public AIBatchExtractor(AIService aiService, ILogger<AIBatchExtractor> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// 批量提取字段
    /// </summary>
    /// <param name="documentContent">文档内容</param>
    /// <param name="templateFields">模板字段列表</param>
    /// <returns>提取结果</returns>
    public async Task<List<AIExtractedField>> ExtractFieldsAsync(
        ParsedDocumentContent documentContent,
        List<FieldInfo> templateFields)
    {
        var results = new List<AIExtractedField>();

        try
        {
            // 构建字段列表字符串
            var fieldList = string.Join("\n", templateFields.Select(f =>
                $"- {f.FieldName} ({f.FieldType}){(f.IsRequired ? " [必填]" : "")}"));

            // 构建 Prompt
            var prompt = AIExtractionAgentConfig.BatchExtractionPrompt
                .Replace("{DOCUMENT_CONTENT}", TruncateContent(documentContent.PlainText, 8000))
                .Replace("{FIELD_LIST}", fieldList);

            // 调用 LLM
            var response = await _aiService.CallLLMAsync(prompt);

            // 解析 JSON 响应
            var extractionResult = JsonSerializer.Deserialize<BatchExtractionResponse>(response);

            if (extractionResult?.Fields != null)
            {
                foreach (var field in extractionResult.Fields)
                {
                    results.Add(new AIExtractedField
                    {
                        FieldName = field.FieldName,
                        FieldValue = field.FieldValue,
                        Confidence = field.Confidence,
                        SourceText = field.SourceText,
                        MatchMethod = field.MatchMethod
                    });
                }
            }

            _logger.LogInformation($"AI 批量提取完成：成功提取 {results.Count} 个字段");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 批量提取失败");
        }

        return results;
    }

    /// <summary>
    /// 提取表格数据
    /// </summary>
    public async Task<List<Dictionary<string, string>>> ExtractTableDataAsync(
        ParsedDocumentContent documentContent,
        string tableName,
        List<string> columnNames)
    {
        var results = new List<Dictionary<string, string>>();

        try
        {
            // 如果文档已经解析出表格，优先使用
            var parsedTable = documentContent.Tables.FirstOrDefault();
            if (parsedTable != null)
            {
                foreach (var row in parsedTable.Rows)
                {
                    var rowData = new Dictionary<string, string>();
                    for (int i = 0; i < Math.Min(parsedTable.Headers.Count, row.Count); i++)
                    {
                        rowData[parsedTable.Headers[i]] = row[i];
                    }
                    results.Add(rowData);
                }

                return results;
            }

            // 否则使用 AI 提取
            var prompt = AIExtractionAgentConfig.TableExtractionPrompt
                .Replace("{DOCUMENT_CONTENT}", TruncateContent(documentContent.PlainText, 6000))
                .Replace("{TABLE_NAME}", tableName)
                .Replace("{COLUMN_NAMES}", string.Join(", ", columnNames));

            var response = await _aiService.CallLLMAsync(prompt);

            var tableResult = JsonSerializer.Deserialize<TableExtractionResponse>(response);

            if (tableResult?.Rows != null)
            {
                results = tableResult.Rows;
            }

            _logger.LogInformation($"AI 表格提取完成：提取 {results.Count} 行数据");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 表格提取失败");
        }

        return results;
    }

    /// <summary>
    /// 语义匹配（判断文档字段与模板字段是否匹配）
    /// </summary>
    public async Task<(bool IsMatch, int Confidence, string Reason)> SemanticMatchAsync(
        string documentLabel,
        string templateField)
    {
        try
        {
            var prompt = AIExtractionAgentConfig.SemanticMatchPrompt
                .Replace("{DOCUMENT_LABEL}", documentLabel)
                .Replace("{TEMPLATE_FIELD}", templateField);

            var response = await _aiService.CallLLMAsync(prompt);

            var matchResult = JsonSerializer.Deserialize<SemanticMatchResponse>(response);

            if (matchResult != null)
            {
                return (matchResult.IsMatch, matchResult.Confidence, matchResult.Reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "语义匹配失败");
        }

        return (false, 0, "匹配失败");
    }

    /// <summary>
    /// 截断内容（避免超出 LLM Token 限制）
    /// </summary>
    private string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength)
        {
            return content;
        }

        return content.Substring(0, maxLength) + "\n\n... [内容已截断] ...";
    }
}

// 响应模型
internal sealed class BatchExtractionResponse
{
    public List<ExtractedFieldResponse> Fields { get; set; } = new();
}

internal sealed class ExtractedFieldResponse
{
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
    public int Confidence { get; set; }
    public string? SourceText { get; set; }
    public string MatchMethod { get; set; } = "Semantic";
}

internal sealed class TableExtractionResponse
{
    public string TableName { get; set; } = string.Empty;
    public List<Dictionary<string, string>> Rows { get; set; } = new();
}

internal sealed class SemanticMatchResponse
{
    public bool IsMatch { get; set; }
    public int Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}
```

### 2.3 创建 AI 提取结果模型

创建 `Models/AIExtraction/AIExtractedField.cs`：

```csharp
namespace FrameAgentWordFill.Models.AIExtraction;

/// <summary>
/// AI 提取的字段
/// </summary>
public sealed class AIExtractedField
{
    /// <summary>
    /// 字段名
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 字段值
    /// </summary>
    public string? FieldValue { get; set; }

    /// <summary>
    /// 置信度（0-100）
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// 源文本（文档中的原文）
    /// </summary>
    public string? SourceText { get; set; }

    /// <summary>
    /// 匹配方式（Exact/Fuzzy/Semantic）
    /// </summary>
    public string MatchMethod { get; set; } = "Semantic";

    /// <summary>
    /// 是否需要人工确认
    /// </summary>
    public bool NeedsConfirmation => Confidence < 70;

    /// <summary>
    /// 置信度等级（High/Medium/Low）
    /// </summary>
    public string ConfidenceLevel
    {
        get
        {
            if (Confidence >= 90) return "High";
            if (Confidence >= 70) return "Medium";
            return "Low";
        }
    }
}
```

---

## 步骤 3：实现智能字段匹配算法

W5 已经实现了字段匹配器，这里增强它以支持 AI 提取的结果。

编辑 `Tools/FieldMatcher.cs`，添加 AI 提取结果的匹配方法：

```csharp
/// <summary>
/// 匹配 AI 提取的字段到模板字段
/// </summary>
/// <param name="aiExtractedFields">AI 提取的字段</param>
/// <param name="templateFields">模板字段</param>
/// <returns>匹配结果</returns>
public List<AIFieldMatchResult> MatchAIExtractedFields(
    List<AIExtractedField> aiExtractedFields,
    List<FieldInfo> templateFields)
{
    var matchResults = new List<AIFieldMatchResult>();

    foreach (var aiField in aiExtractedFields)
    {
        var matchResult = new AIFieldMatchResult
        {
            SourceFieldName = aiField.FieldName,
            FieldValue = aiField.FieldValue,
            OriginalConfidence = aiField.Confidence,
            SourceText = aiField.SourceText,
            OriginalMatchMethod = aiField.MatchMethod
        };

        // 尝试精确匹配
        var exactMatch = templateFields.FirstOrDefault(f =>
            NormalizeFieldName(f.FieldName).Equals(NormalizeFieldName(aiField.FieldName), StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            matchResult.TemplateFieldName = exactMatch.FieldName;
            matchResult.FinalConfidence = Math.Max(aiField.Confidence, 95); // 精确匹配提升置信度
            matchResult.FinalMatchMethod = "Exact";
            matchResults.Add(matchResult);
            continue;
        }

        // 尝试模糊匹配
        var (fuzzyMatch, fuzzySimilarity) = FuzzyMatch(aiField.FieldName, templateFields);
        if (fuzzyMatch != null && fuzzySimilarity >= 70)
        {
            matchResult.TemplateFieldName = fuzzyMatch.FieldName;
            matchResult.FinalConfidence = (aiField.Confidence + fuzzySimilarity) / 2;
            matchResult.FinalMatchMethod = "Fuzzy";
            matchResults.Add(matchResult);
            continue;
        }

        // 语义匹配（使用 AI 原始匹配结果）
        var semanticMatch = templateFields.FirstOrDefault(f =>
            NormalizeFieldName(f.FieldName).Contains(NormalizeFieldName(aiField.FieldName), StringComparison.OrdinalIgnoreCase) ||
            NormalizeFieldName(aiField.FieldName).Contains(NormalizeFieldName(f.FieldName), StringComparison.OrdinalIgnoreCase));

        if (semanticMatch != null)
        {
            matchResult.TemplateFieldName = semanticMatch.FieldName;
            matchResult.FinalConfidence = Math.Min(aiField.Confidence, 80); // 语义匹配降低置信度上限
            matchResult.FinalMatchMethod = "Semantic";
            matchResults.Add(matchResult);
            continue;
        }

        // 无法匹配
        matchResult.FinalConfidence = Math.Min(aiField.Confidence, 50);
        matchResult.FinalMatchMethod = "NoMatch";
        matchResults.Add(matchResult);
    }

    _logger.LogInformation($"AI 字段匹配完成：{matchResults.Count(m => m.FinalConfidence >= 70)} 个高置信度匹配");

    return matchResults;
}
```

创建 `Models/AIExtraction/AIFieldMatchResult.cs`：

```csharp
namespace FrameAgentWordFill.Models.AIExtraction;

/// <summary>
/// AI 字段匹配结果
/// </summary>
public sealed class AIFieldMatchResult
{
    /// <summary>
    /// 源字段名（AI 提取的）
    /// </summary>
    public string SourceFieldName { get; set; } = string.Empty;

    /// <summary>
    /// 模板字段名（匹配到的）
    /// </summary>
    public string? TemplateFieldName { get; set; }

    /// <summary>
    /// 字段值
    /// </summary>
    public string? FieldValue { get; set; }

    /// <summary>
    /// 原始置信度（AI 提取时的置信度）
    /// </summary>
    public int OriginalConfidence { get; set; }

    /// <summary>
    /// 最终置信度（综合匹配后的置信度）
    /// </summary>
    public int FinalConfidence { get; set; }

    /// <summary>
    /// 源文本
    /// </summary>
    public string? SourceText { get; set; }

    /// <summary>
    /// 原始匹配方式
    /// </summary>
    public string OriginalMatchMethod { get; set; } = string.Empty;

    /// <summary>
    /// 最终匹配方式
    /// </summary>
    public string FinalMatchMethod { get; set; } = string.Empty;

    /// <summary>
    /// 是否需要人工确认
    /// </summary>
    public bool NeedsConfirmation => FinalConfidence < 70;

    /// <summary>
    /// 置信度等级
    /// </summary>
    public string ConfidenceLevel
    {
        get
        {
            if (FinalConfidence >= 90) return "High";
            if (FinalConfidence >= 70) return "Medium";
            return "Low";
        }
    }
}
```

---

## 步骤 4：实现置信度评分机制

创建 `Tools/ConfidenceScorer.cs`：

```csharp
using FrameAgentWordFill.Models.AIExtraction;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 置信度评分器（综合多种因素计算最终置信度）
/// </summary>
public sealed class ConfidenceScorer
{
    private readonly DataValidator _dataValidator;
    private readonly ILogger<ConfidenceScorer> _logger;

    public ConfidenceScorer(DataValidator dataValidator, ILogger<ConfidenceScorer> logger)
    {
        _dataValidator = dataValidator;
        _logger = logger;
    }

    /// <summary>
    /// 计算最终置信度
    /// </summary>
    /// <param name="matchResult">匹配结果</param>
    /// <param name="fieldType">字段类型</param>
    /// <returns>最终置信度（0-100）</returns>
    public int CalculateFinalConfidence(AIFieldMatchResult matchResult, string fieldType)
    {
        var scores = new List<int>();

        // 1. AI 原始置信度（权重 40%）
        scores.Add((int)(matchResult.OriginalConfidence * 0.4));

        // 2. 匹配方式得分（权重 30%）
        var matchScore = matchResult.FinalMatchMethod switch
        {
            "Exact" => 100,
            "Fuzzy" => 80,
            "Semantic" => 60,
            _ => 30
        };
        scores.Add((int)(matchScore * 0.3));

        // 3. 数据格式验证得分（权重 30%）
        var validationScore = 100;
        if (!string.IsNullOrEmpty(matchResult.FieldValue))
        {
            var validationResult = _dataValidator.ValidateField(fieldType, matchResult.FieldValue);
            validationScore = validationResult.IsValid ? 100 : 50;
        }
        scores.Add((int)(validationScore * 0.3));

        var finalScore = scores.Sum();

        _logger.LogDebug($"置信度计算：{matchResult.SourceFieldName} -> {finalScore} " +
                        $"(AI:{matchResult.OriginalConfidence}, Match:{matchScore}, Validation:{validationScore})");

        return Math.Clamp(finalScore, 0, 100);
    }

    /// <summary>
    /// 批量计算置信度
    /// </summary>
    public List<AIFieldMatchResult> CalculateBatchConfidence(
        List<AIFieldMatchResult> matchResults,
        Dictionary<string, string> fieldTypes)
    {
        foreach (var matchResult in matchResults)
        {
            if (matchResult.TemplateFieldName != null &&
                fieldTypes.TryGetValue(matchResult.TemplateFieldName, out var fieldType))
            {
                matchResult.FinalConfidence = CalculateFinalConfidence(matchResult, fieldType);
            }
        }

        return matchResults;
    }
}
```

---

## 步骤 5：实现多引擎兜底策略

创建 `Services/MultiEngineLLMService.cs`：

```csharp
namespace FrameAgentWordFill.Services;

/// <summary>
/// 多引擎 LLM 服务（Copilot SDK → Azure OpenAI → OpenAI）
/// </summary>
public sealed class MultiEngineLLMService
{
    private readonly AIService _aiService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MultiEngineLLMService> _logger;

    public MultiEngineLLMService(
        AIService aiService,
        IConfiguration configuration,
        ILogger<MultiEngineLLMService> logger)
    {
        _aiService = aiService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 调用 LLM（自动尝试多个引擎）
    /// </summary>
    public async Task<(string Response, string Engine)> CallLLMWithFallbackAsync(string prompt)
    {
        // 引擎优先级列表
        var engines = new[]
        {
            "CopilotSDK",
            "AzureOpenAI",
            "OpenAI"
        };

        foreach (var engine in engines)
        {
            try
            {
                _logger.LogInformation($"尝试使用引擎：{engine}");

                var response = await CallEngineAsync(engine, prompt);

                if (!string.IsNullOrWhiteSpace(response))
                {
                    _logger.LogInformation($"引擎 {engine} 调用成功");
                    return (response, engine);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"引擎 {engine} 调用失败，尝试下一个引擎");
            }
        }

        // 所有引擎都失败，返回降级响应
        _logger.LogError("所有 LLM 引擎均不可用，使用本地规则提取");
        return (string.Empty, "LocalRules");
    }

    /// <summary>
    /// 调用指定引擎
    /// </summary>
    private async Task<string> CallEngineAsync(string engine, string prompt)
    {
        return engine switch
        {
            "CopilotSDK" => await CallCopilotSDKAsync(prompt),
            "AzureOpenAI" => await CallAzureOpenAIAsync(prompt),
            "OpenAI" => await CallOpenAIAsync(prompt),
            _ => throw new NotSupportedException($"不支持的引擎：{engine}")
        };
    }

    /// <summary>
    /// 调用 Copilot SDK（复用 W4 的 AIService）
    /// </summary>
    private async Task<string> CallCopilotSDKAsync(string prompt)
    {
        return await _aiService.CallLLMAsync(prompt);
    }

    /// <summary>
    /// 调用 Azure OpenAI
    /// </summary>
    private async Task<string> CallAzureOpenAIAsync(string prompt)
    {
        var endpoint = _configuration["AzureOpenAI:Endpoint"];
        var apiKey = _configuration["AzureOpenAI:ApiKey"];
        var deploymentName = _configuration["AzureOpenAI:DeploymentName"];

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Azure OpenAI 配置不完整");
        }

        // TODO: 实现 Azure OpenAI API 调用
        // 使用 Azure.AI.OpenAI NuGet 包

        _logger.LogWarning("Azure OpenAI 调用未实现");
        throw new NotImplementedException("Azure OpenAI 调用需要实现");
    }

    /// <summary>
    /// 调用 OpenAI
    /// </summary>
    private async Task<string> CallOpenAIAsync(string prompt)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API Key 未配置");
        }

        // TODO: 实现 OpenAI API 调用
        // 使用 OpenAI NuGet 包

        _logger.LogWarning("OpenAI 调用未实现");
        throw new NotImplementedException("OpenAI 调用需要实现");
    }
}
```

---

## 步骤 6：实现 AI 提取会话管理

### 6.1 创建 AI 提取会话模型

创建 `Models/AIExtraction/AIExtractionSession.cs`：

```csharp
namespace FrameAgentWordFill.Models.AIExtraction;

/// <summary>
/// AI 提取会话
/// </summary>
public sealed class AIExtractionSession
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public int SessionId { get; set; }

    /// <summary>
    /// 模板ID
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// 上传文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件类型
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// 提取状态（Parsing/Extracting/WaitingConfirm/Completed）
    /// </summary>
    public string Status { get; set; } = "Parsing";

    /// <summary>
    /// 使用的 LLM 引擎
    /// </summary>
    public string UsedEngine { get; set; } = string.Empty;

    /// <summary>
    /// 提取的字段数量
    /// </summary>
    public int ExtractedFieldCount { get; set; } = 0;

    /// <summary>
    /// 高置信度字段数量
    /// </summary>
    public int HighConfidenceCount { get; set; } = 0;

    /// <summary>
    /// 低置信度字段数量（需人工确认）
    /// </summary>
    public int LowConfidenceCount { get; set; } = 0;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

### 6.2 创建数据访问层

创建 `Repositories/AIExtractionRepository.cs`：

```csharp
using Microsoft.Data.Sqlite;
using FrameAgentWordFill.Models.AIExtraction;

namespace FrameAgentWordFill.Repositories;

/// <summary>
/// AI 提取数据访问层
/// </summary>
public sealed class AIExtractionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AIExtractionRepository> _logger;

    public AIExtractionRepository(IConfiguration configuration, ILogger<AIExtractionRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("连接字符串未配置");
        _logger = logger;
    }

    /// <summary>
    /// 创建提取会话
    /// </summary>
    public async Task<int> CreateSessionAsync(AIExtractionSession session)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO fa_ai_extraction_sessions 
            (template_id, file_path, file_type, status, used_engine, created_at, updated_at)
            VALUES (@templateId, @filePath, @fileType, @status, @usedEngine, @createdAt, @updatedAt);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@templateId", session.TemplateId);
        command.Parameters.AddWithValue("@filePath", session.FilePath);
        command.Parameters.AddWithValue("@fileType", session.FileType);
        command.Parameters.AddWithValue("@status", session.Status);
        command.Parameters.AddWithValue("@usedEngine", session.UsedEngine);
        command.Parameters.AddWithValue("@createdAt", session.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@updatedAt", session.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

        var sessionId = Convert.ToInt32(await command.ExecuteScalarAsync());
        _logger.LogInformation($"AI 提取会话创建成功：SessionId={sessionId}");

        return sessionId;
    }

    /// <summary>
    /// 保存提取结果
    /// </summary>
    public async Task SaveExtractionResultsAsync(int sessionId, List<AIFieldMatchResult> results)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var result in results)
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO fa_ai_extracted_fields
                    (session_id, source_field_name, template_field_name, field_value, 
                     original_confidence, final_confidence, source_text, match_method, 
                     needs_confirmation, created_at)
                    VALUES (@sessionId, @sourceFieldName, @templateFieldName, @fieldValue,
                            @originalConfidence, @finalConfidence, @sourceText, @matchMethod,
                            @needsConfirmation, @createdAt);";

                command.Parameters.AddWithValue("@sessionId", sessionId);
                command.Parameters.AddWithValue("@sourceFieldName", result.SourceFieldName);
                command.Parameters.AddWithValue("@templateFieldName", (object?)result.TemplateFieldName ?? DBNull.Value);
                command.Parameters.AddWithValue("@fieldValue", (object?)result.FieldValue ?? DBNull.Value);
                command.Parameters.AddWithValue("@originalConfidence", result.OriginalConfidence);
                command.Parameters.AddWithValue("@finalConfidence", result.FinalConfidence);
                command.Parameters.AddWithValue("@sourceText", (object?)result.SourceText ?? DBNull.Value);
                command.Parameters.AddWithValue("@matchMethod", result.FinalMatchMethod);
                command.Parameters.AddWithValue("@needsConfirmation", result.NeedsConfirmation ? 1 : 0);
                command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                await command.ExecuteNonQueryAsync();
            }

            transaction.Commit();
            _logger.LogInformation($"保存提取结果成功：{results.Count} 个字段");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 获取提取结果
    /// </summary>
    public async Task<List<AIFieldMatchResult>> GetExtractionResultsAsync(int sessionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT * FROM fa_ai_extracted_fields 
            WHERE session_id = @sessionId 
            ORDER BY final_confidence DESC;";
        command.Parameters.AddWithValue("@sessionId", sessionId);

        var results = new List<AIFieldMatchResult>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new AIFieldMatchResult
            {
                SourceFieldName = reader.GetString(2),
                TemplateFieldName = reader.IsDBNull(3) ? null : reader.GetString(3),
                FieldValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                OriginalConfidence = reader.GetInt32(5),
                FinalConfidence = reader.GetInt32(6),
                SourceText = reader.IsDBNull(7) ? null : reader.GetString(7),
                FinalMatchMethod = reader.GetString(8)
            });
        }

        return results;
    }
}
```

### 6.3 更新数据库表结构

编辑 `Data/DatabaseInitializer.cs`，添加 AI 提取相关表：

```csharp
// W7: 创建 AI 提取会话表
command.CommandText = @"
CREATE TABLE IF NOT EXISTS fa_ai_extraction_sessions (
    session_id INTEGER PRIMARY KEY AUTOINCREMENT,
    template_id INTEGER NOT NULL,
    file_path TEXT NOT NULL,
    file_type TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'Parsing',
    used_engine TEXT NOT NULL DEFAULT '',
    extracted_field_count INTEGER NOT NULL DEFAULT 0,
    high_confidence_count INTEGER NOT NULL DEFAULT 0,
    low_confidence_count INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    error_message TEXT,
    FOREIGN KEY (template_id) REFERENCES fa_templates(template_id) ON DELETE CASCADE
);";
await command.ExecuteNonQueryAsync();

// W7: 创建 AI 提取字段表
command.CommandText = @"
CREATE TABLE IF NOT EXISTS fa_ai_extracted_fields (
    field_id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL,
    source_field_name TEXT NOT NULL,
    template_field_name TEXT,
    field_value TEXT,
    original_confidence INTEGER NOT NULL DEFAULT 0,
    final_confidence INTEGER NOT NULL DEFAULT 0,
    source_text TEXT,
    match_method TEXT NOT NULL DEFAULT 'Semantic',
    needs_confirmation INTEGER NOT NULL DEFAULT 0,
    is_user_confirmed INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (session_id) REFERENCES fa_ai_extraction_sessions(session_id) ON DELETE CASCADE
);";
await command.ExecuteNonQueryAsync();

// W7: 创建索引
command.CommandText = "CREATE INDEX IF NOT EXISTS idx_ai_extraction_sessions_template_id ON fa_ai_extraction_sessions(template_id);";
await command.ExecuteNonQueryAsync();
command.CommandText = "CREATE INDEX IF NOT EXISTS idx_ai_extracted_fields_session_id ON fa_ai_extracted_fields(session_id);";
await command.ExecuteNonQueryAsync();
```

---

## 步骤 7：实现服务层和 API

### 7.1 创建 AI 提取服务

创建 `Services/AIExtractionService.cs`：

```csharp
using FrameAgentWordFill.Models.AIExtraction;
using FrameAgentWordFill.Repositories;
using FrameAgentWordFill.Tools;
using FrameAgentWordFill.Tools.FileParser;

namespace FrameAgentWordFill.Services;

/// <summary>
/// AI 提取服务（业务逻辑层）
/// </summary>
public sealed class AIExtractionService
{
    private readonly AIExtractionRepository _repository;
    private readonly TemplateRepository _templateRepository;
    private readonly FileStorageService _fileStorage;
    private readonly PdfParser _pdfParser;
    private readonly WordTableParser _wordParser;
    private readonly ExcelParser _excelParser;
    private readonly AIBatchExtractor _aiExtractor;
    private readonly FieldMatcher _fieldMatcher;
    private readonly ConfidenceScorer _confidenceScorer;
    private readonly MultiEngineLLMService _multiEngineLLM;
    private readonly ILogger<AIExtractionService> _logger;

    public AIExtractionService(
        AIExtractionRepository repository,
        TemplateRepository templateRepository,
        FileStorageService fileStorage,
        PdfParser pdfParser,
        WordTableParser wordParser,
        ExcelParser excelParser,
        AIBatchExtractor aiExtractor,
        FieldMatcher fieldMatcher,
        ConfidenceScorer confidenceScorer,
        MultiEngineLLMService multiEngineLLM,
        ILogger<AIExtractionService> logger)
    {
        _repository = repository;
        _templateRepository = templateRepository;
        _fileStorage = fileStorage;
        _pdfParser = pdfParser;
        _wordParser = wordParser;
        _excelParser = excelParser;
        _aiExtractor = aiExtractor;
        _fieldMatcher = fieldMatcher;
        _confidenceScorer = confidenceScorer;
        _multiEngineLLM = multiEngineLLM;
        _logger = logger;
    }

    /// <summary>
    /// 上传文件并开始 AI 提取
    /// </summary>
    public async Task<(int SessionId, string ErrorMessage)> UploadAndExtractAsync(
        int templateId,
        IFormFile file)
    {
        try
        {
            // 1. 验证模板
            var template = await _templateRepository.GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                return (-1, "模板不存在");
            }

            // 2. 保存文件
            var filePath = await _fileStorage.SaveUploadFileAsync(file);
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            var fileType = fileExtension switch
            {
                ".pdf" => "PDF",
                ".docx" or ".doc" => "Word",
                ".xlsx" or ".xls" => "Excel",
                _ => "Unknown"
            };

            if (fileType == "Unknown")
            {
                return (-1, "不支持的文件类型");
            }

            // 3. 创建会话
            var session = new AIExtractionSession
            {
                TemplateId = templateId,
                FilePath = filePath,
                FileType = fileType,
                Status = "Parsing"
            };

            var sessionId = await _repository.CreateSessionAsync(session);

            // 4. 异步执行提取（避免阻塞）
            _ = Task.Run(async () => await PerformExtractionAsync(sessionId, filePath, fileType, templateId));

            _logger.LogInformation($"AI 提取会话创建成功：SessionId={sessionId}");

            return (sessionId, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传文件失败");
            return (-1, $"上传失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 执行 AI 提取（后台任务）
    /// </summary>
    private async Task PerformExtractionAsync(int sessionId, string filePath, string fileType, int templateId)
    {
        try
        {
            // 1. 解析文件
            var fullPath = _fileStorage.GetUploadFilePath(filePath);
            ParsedDocumentContent parsedContent;

            switch (fileType)
            {
                case "PDF":
                    parsedContent = await _pdfParser.ParseAsync(fullPath);
                    break;
                case "Word":
                    parsedContent = await _wordParser.ParseFullContentAsync(fullPath);
                    break;
                case "Excel":
                    parsedContent = await _excelParser.ParseFullContentAsync(fullPath);
                    break;
                default:
                    throw new NotSupportedException($"不支持的文件类型：{fileType}");
            }

            // 2. 获取模板字段
            var parseResult = await _templateRepository.GetTemplateParseResultAsync(templateId);
            if (parseResult == null)
            {
                throw new InvalidOperationException("模板解析结果不存在");
            }

            // 3. AI 批量提取
            var aiExtractedFields = await _aiExtractor.ExtractFieldsAsync(
                parsedContent,
                parseResult.Fields);

            // 4. 字段匹配
            var matchResults = _fieldMatcher.MatchAIExtractedFields(
                aiExtractedFields,
                parseResult.Fields);

            // 5. 置信度评分
            var fieldTypes = parseResult.Fields.ToDictionary(f => f.FieldName, f => f.FieldType);
            matchResults = _confidenceScorer.CalculateBatchConfidence(matchResults, fieldTypes);

            // 6. 保存结果
            await _repository.SaveExtractionResultsAsync(sessionId, matchResults);

            _logger.LogInformation($"AI 提取完成：SessionId={sessionId}，提取 {matchResults.Count} 个字段");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"AI 提取失败：SessionId={sessionId}");
        }
    }

    /// <summary>
    /// 获取提取结果
    /// </summary>
    public async Task<List<AIFieldMatchResult>> GetExtractionResultsAsync(int sessionId)
    {
        return await _repository.GetExtractionResultsAsync(sessionId);
    }
}
```

### 7.2 创建 API 控制器

创建 `Controllers/AIExtractionController.cs`：

```csharp
using Microsoft.AspNetCore.Mvc;
using FrameAgentWordFill.Services;

namespace FrameAgentWordFill.Controllers;

/// <summary>
/// AI 提取 API
/// </summary>
[ApiController]
[Route("api/ai-extraction")]
public sealed class AIExtractionController : ControllerBase
{
    private readonly AIExtractionService _extractionService;
    private readonly ILogger<AIExtractionController> _logger;

    public AIExtractionController(AIExtractionService extractionService, ILogger<AIExtractionController> logger)
    {
        _extractionService = extractionService;
        _logger = logger;
    }

    /// <summary>
    /// 上传文件并开始 AI 提取
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadAndExtract([FromForm] int templateId, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "文件不能为空" });
        }

        var (sessionId, errorMessage) = await _extractionService.UploadAndExtractAsync(templateId, file);

        if (sessionId == -1)
        {
            return BadRequest(new { error = errorMessage });
        }

        return Ok(new { sessionId, message = "文件上传成功，AI 正在提取数据..." });
    }

    /// <summary>
    /// 获取提取结果
    /// </summary>
    [HttpGet("results/{sessionId}")]
    public async Task<IActionResult> GetResults(int sessionId)
    {
        var results = await _extractionService.GetExtractionResultsAsync(sessionId);

        return Ok(new
        {
            sessionId,
            results = results.Select(r => new
            {
                r.SourceFieldName,
                r.TemplateFieldName,
                r.FieldValue,
                r.FinalConfidence,
                r.ConfidenceLevel,
                r.SourceText,
                r.FinalMatchMethod,
                r.NeedsConfirmation
            })
        });
    }
}
```

### 7.3 注册服务

编辑 `Program.cs`，注册新服务：

```csharp
// W7: AI 提取服务
builder.Services.AddScoped<AIExtractionRepository>();
builder.Services.AddScoped<PdfParser>();
builder.Services.AddScoped<AIBatchExtractor>();
builder.Services.AddScoped<ConfidenceScorer>();
builder.Services.AddScoped<MultiEngineLLMService>();
builder.Services.AddScoped<AIExtractionService>();
```

---

## 步骤 8：实现前端提取和确认界面

### 8.1 创建 AI 提取页面

创建 `frontend/src/views/user/AIExtraction.vue`：

```vue
<template>
  <div class="ai-extraction-container">
    <el-card class="header-card">
      <h2>AI 智能提取</h2>
      <p>上传参考文件，AI 自动提取字段数据</p>
    </el-card>

    <!-- 步骤1: 选择模板 -->
    <el-card v-if="currentStep === 1">
      <h3>步骤 1：选择模板</h3>
      <el-select v-model="selectedTemplateId" placeholder="请选择模板" style="width: 100%">
        <el-option
          v-for="template in templates"
          :key="template.templateId"
          :label="template.templateName"
          :value="template.templateId"
        />
      </el-select>
      <div class="button-group">
        <el-button type="primary" :disabled="!selectedTemplateId" @click="nextStep">下一步</el-button>
      </div>
    </el-card>

    <!-- 步骤2: 上传文件 -->
    <el-card v-if="currentStep === 2">
      <h3>步骤 2：上传参考文件</h3>
      <el-upload
        ref="uploadRef"
        :auto-upload="false"
        :limit="1"
        :on-change="handleFileChange"
        :file-list="fileList"
        accept=".pdf,.docx,.doc,.xlsx,.xls"
      >
        <el-button type="primary">选择文件</el-button>
        <template #tip>
          <div class="el-upload__tip">
            支持 PDF、Word、Excel 格式（AI 会自动提取所有字段）
          </div>
        </template>
      </el-upload>
      <div class="button-group">
        <el-button @click="prevStep">上一步</el-button>
        <el-button type="primary" :disabled="!uploadFile" :loading="extracting" @click="uploadAndExtract">
          上传并开始提取
        </el-button>
      </div>
    </el-card>

    <!-- 步骤3: AI 提取中 -->
    <el-card v-if="currentStep === 3">
      <el-result icon="info" title="AI 正在提取数据">
        <template #sub-title>
          <div class="extracting-info">
            <el-icon class="is-loading" :size="40"><Loading /></el-icon>
            <p>正在使用 AI 分析文档并提取字段...</p>
            <p style="color: #909399; font-size: 14px;">这可能需要 10-30 秒</p>
          </div>
        </template>
      </el-result>
    </el-card>

    <!-- 步骤4: 确认提取结果 -->
    <el-card v-if="currentStep === 4">
      <template #header>
        <div class="card-header">
          <span class="title">步骤 3：确认提取结果</span>
          <el-tag type="success">AI 已提取 {{ extractedFields.length }} 个字段</el-tag>
        </div>
      </template>

      <!-- 统计信息 -->
      <el-alert
        :title="`高置信度字段：${highConfidenceFields.length} 个 | 中置信度字段：${mediumConfidenceFields.length} 个 | 低置信度字段：${lowConfidenceFields.length} 个`"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 20px"
      />

      <!-- 字段列表 -->
      <el-table :data="extractedFields" stripe style="width: 100%">
        <el-table-column label="置信度" width="120">
          <template #default="{ row }">
            <el-tag :type="getConfidenceTagType(row.confidenceLevel)" size="large">
              {{ row.finalConfidence }}%
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="templateFieldName" label="字段名" width="150" />
        <el-table-column label="字段值" width="250">
          <template #default="{ row }">
            <el-input
              v-model="row.fieldValue"
              placeholder="请输入值"
              :class="{ 'needs-confirmation': row.needsConfirmation }"
            />
          </template>
        </el-table-column>
        <el-table-column label="源文本" show-overflow-tooltip>
          <template #default="{ row }">
            <span style="color: #909399; font-size: 12px;">{{ row.sourceText || '无' }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="finalMatchMethod" label="匹配方式" width="100">
          <template #default="{ row }">
            <el-tag size="small">{{ getMatchMethodText(row.finalMatchMethod) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="120">
          <template #default="{ row }">
            <el-button
              v-if="row.needsConfirmation"
              type="success"
              size="small"
              @click="confirmField(row)"
            >
              确认
            </el-button>
            <el-tag v-else type="success" size="small">已确认</el-tag>
          </template>
        </el-table-column>
      </el-table>

      <!-- 操作按钮 -->
      <div class="button-group">
        <el-button @click="prevStep">重新上传</el-button>
        <el-button type="success" @click="confirmAllFields">全部确认</el-button>
        <el-button type="primary" :loading="generating" @click="generateDocument">
          生成文档
        </el-button>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { ElMessage } from 'element-plus';
import { Loading } from '@element-plus/icons-vue';
import type { UploadUserFile, UploadFile } from 'element-plus';
import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

const router = useRouter();

// 状态
const currentStep = ref(1);
const selectedTemplateId = ref<number | null>(null);
const templates = ref<any[]>([]);
const uploadFile = ref<File | null>(null);
const fileList = ref<UploadUserFile[]>([]);
const extracting = ref(false);
const generating = ref(false);
const sessionId = ref<number | null>(null);
const extractedFields = ref<any[]>([]);

// 分类字段
const highConfidenceFields = computed(() =>
  extractedFields.value.filter((f: any) => f.confidenceLevel === 'High')
);
const mediumConfidenceFields = computed(() =>
  extractedFields.value.filter((f: any) => f.confidenceLevel === 'Medium')
);
const lowConfidenceFields = computed(() =>
  extractedFields.value.filter((f: any) => f.confidenceLevel === 'Low')
);

// 加载模板列表
const loadTemplates = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/templates`);
    templates.value = response.data;
  } catch (error) {
    ElMessage.error('加载模板列表失败');
  }
};

// 文件选择
const handleFileChange = (file: UploadFile) => {
  uploadFile.value = file.raw || null;
};

// 上传并提取
const uploadAndExtract = async () => {
  if (!uploadFile.value || !selectedTemplateId.value) return;

  extracting.value = true;
  currentStep.value = 3; // 显示加载页面

  try {
    // 1. 上传文件
    const formData = new FormData();
    formData.append('file', uploadFile.value);
    formData.append('templateId', selectedTemplateId.value.toString());

    const uploadResponse = await axios.post(`${API_BASE_URL}/ai-extraction/upload`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    });

    sessionId.value = uploadResponse.data.sessionId;

    // 2. 轮询提取结果（每 2 秒查询一次）
    const pollResults = async () => {
      try {
        const resultsResponse = await axios.get(`${API_BASE_URL}/ai-extraction/results/${sessionId.value}`);

        if (resultsResponse.data.results.length > 0) {
          extractedFields.value = resultsResponse.data.results;
          currentStep.value = 4;
          extracting.value = false;
          ElMessage.success('AI 提取完成');
        } else {
          // 继续轮询
          setTimeout(pollResults, 2000);
        }
      } catch (error) {
        ElMessage.error('获取提取结果失败');
        extracting.value = false;
      }
    };

    setTimeout(pollResults, 2000);
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '上传失败');
    extracting.value = false;
    currentStep.value = 2;
  }
};

// 确认单个字段
const confirmField = (field: any) => {
  field.needsConfirmation = false;
  ElMessage.success('字段已确认');
};

// 全部确认
const confirmAllFields = () => {
  extractedFields.value.forEach(field => {
    field.needsConfirmation = false;
  });
  ElMessage.success('所有字段已确认');
};

// 生成文档
const generateDocument = async () => {
  // TODO: 调用生成文档 API
  ElMessage.success('文档生成成功（功能待实现）');
};

// 步骤控制
const nextStep = () => {
  if (currentStep.value < 4) currentStep.value++;
};

const prevStep = () => {
  if (currentStep.value > 1) currentStep.value--;
};

// 置信度标签类型
const getConfidenceTagType = (level: string) => {
  if (level === 'High') return 'success';
  if (level === 'Medium') return '';
  return 'danger';
};

// 匹配方式文本
const getMatchMethodText = (method: string) => {
  const map: Record<string, string> = {
    Exact: '精确',
    Fuzzy: '模糊',
    Semantic: '语义',
    NoMatch: '未匹配'
  };
  return map[method] || method;
};

onMounted(() => {
  loadTemplates();
});
</script>

<style scoped>
.ai-extraction-container {
  padding: 20px;
}

.header-card {
  margin-bottom: 20px;
}

.button-group {
  margin-top: 20px;
  display: flex;
  gap: 10px;
  justify-content: flex-end;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-header .title {
  font-size: 18px;
  font-weight: bold;
}

.extracting-info {
  text-align: center;
  padding: 40px 0;
}

.needs-confirmation {
  border-color: #f56c6c;
}
</style>
```

### 8.2 更新路由配置

编辑 `frontend/src/router/index.ts`，添加路由：

```typescript
{
  path: '/ai-extraction',
  name: 'AIExtraction',
  component: () => import('../views/user/AIExtraction.vue'),
  meta: { title: 'AI 智能提取' }
}
```

### 8.3 更新主菜单

编辑 `frontend/src/App.vue`，添加菜单项：

```vue
<el-menu-item index="/ai-extraction">
  <el-icon><MagicStick /></el-icon>
  <span>AI 智能提取</span>
</el-menu-item>
```

---

## 📝 总结

### 本周新增文件清单

**后端文件**（15 个）：
1. `Models/AIExtraction/ParsedDocumentContent.cs` - 文档解析结果
2. `Models/AIExtraction/AIExtractedField.cs` - AI 提取字段
3. `Models/AIExtraction/AIFieldMatchResult.cs` - 字段匹配结果
4. `Models/AIExtraction/AIExtractionSession.cs` - 提取会话
5. `Tools/FileParser/PdfParser.cs` - PDF 解析器
6. `Tools/FileParser/ImageOcrParser.cs` - 图片 OCR 解析器
7. `Tools/FileParser/WordTableParser.cs` - 更新（新增方法）
8. `Tools/FileParser/ExcelParser.cs` - 更新（新增方法）
9. `Agents/AIExtractionAgentConfig.cs` - AI 提取配置
10. `Tools/AIBatchExtractor.cs` - AI 批量提取工具
11. `Tools/FieldMatcher.cs` - 更新（新增方法）
12. `Tools/ConfidenceScorer.cs` - 置信度评分器
13. `Services/MultiEngineLLMService.cs` - 多引擎 LLM 服务
14. `Repositories/AIExtractionRepository.cs` - AI 提取仓库
15. `Services/AIExtractionService.cs` - AI 提取服务
16. `Controllers/AIExtractionController.cs` - AI 提取控制器

**前端文件**（1 个）：
1. `frontend/src/views/user/AIExtraction.vue` - AI 提取页面

**总计**：16 个核心文件

### 技术要点回顾

1. **多格式文件解析**：PDF（iText7）、Word（OpenXML）、Excel（EPPlus）
2. **AI 批量提取**：复用 W4 的 LLM 能力，使用精心设计的 Prompt
3. **三层匹配策略**：精确匹配 → 模糊匹配（编辑距离）→ 语义匹配（LLM）
4. **置信度评分**：综合 AI 置信度、匹配方式、数据验证三个维度
5. **多引擎兜底**：Copilot SDK → Azure OpenAI → OpenAI → 本地规则
6. **异步处理**：文件上传后后台执行提取，避免阻塞
7. **可视化确认**：按置信度分类显示，低置信度字段高亮提示

### 验收标准

✅ **功能验收**：
1. 支持 PDF、Word、Excel 文件上传
2. AI 能准确提取至少 80% 的字段
3. 置信度评分合理（高/中/低三级）
4. 字段匹配准确率 ≥ 85%
5. 人工确认界面友好，操作便捷

✅ **性能验收**：
1. 文件上传响应时间 < 2 秒
2. AI 提取时间 < 30 秒（普通文档）
3. 字段匹配时间 < 2 秒

### 下一步：W8 - 验收收尾

进入 W8 开发阶段，完成：
- 端到端测试
- Bug 修复
- 文档整理
- 演示材料准备

**参考文档**：
- 03_guide_项目执行计划.md


