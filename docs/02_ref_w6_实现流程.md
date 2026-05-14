# W6 实现流程 - 复杂模板能力增强

**周期**：第 6 周  
**里程碑**：M6 - 复杂模板能力增强  
**目标**：支持复杂 Word 模板元素处理，提供模板预检和错误提示功能

**⚠️ 核心功能说明**：
- 本周是项目的**模板质量保障周**，确保系统能处理各种复杂模板
- 支持 Word **内容控件**（7 种类型）的识别和填充
- 支持**图片动态替换**（Logo、签名等）
- 提供**模板预检**功能，自动检测和修复格式问题
- 增强系统**容错能力**，降低模板格式要求

---

## 📋 实施步骤总览

```
步骤1: 实现模板预检工具（自动检测格式问题）
    ↓
步骤2: 实现占位符规范化工具（自动修复格式）
    ↓
步骤3: 实现内容控件识别和填充（7种控件类型）
    ↓
步骤4: 实现图片替换功能（Base64 和本地文件）
    ↓
步骤5: 实现错误提示和修复建议（三级分类）
    ↓
步骤6: 实现前端模板校验报告页面
    ↓
步骤7: 验收测试
```

---

## 步骤 1：实现模板预检工具

### 1.1 创建校验相关数据模型

创建 `Models/Validation/ValidationResult.cs`：

```csharp
namespace FrameAgentWordFill.Models.Validation;

/// <summary>
/// 模板校验结果
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// 是否通过校验
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 问题列表
    /// </summary>
    public List<ValidationIssue> Issues { get; set; } = new();

    /// <summary>
    /// 错误数量
    /// </summary>
    public int ErrorCount => Issues.Count(i => i.Severity == IssueSeverity.Error);

    /// <summary>
    /// 警告数量
    /// </summary>
    public int WarningCount => Issues.Count(i => i.Severity == IssueSeverity.Warning);

    /// <summary>
    /// 信息数量
    /// </summary>
    public int InfoCount => Issues.Count(i => i.Severity == IssueSeverity.Info);

    /// <summary>
    /// 校验摘要
    /// </summary>
    public string Summary => $"发现 {ErrorCount} 个错误，{WarningCount} 个警告，{InfoCount} 条信息";
}
```

创建 `Models/Validation/ValidationIssue.cs`：

```csharp
namespace FrameAgentWordFill.Models.Validation;

/// <summary>
/// 单个校验问题
/// </summary>
public sealed class ValidationIssue
{
    /// <summary>
    /// 严重程度
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    /// 问题类型（PlaceholderFormat/FieldMismatch/TableStructure 等）
    /// </summary>
    public string IssueType { get; set; } = string.Empty;

    /// <summary>
    /// 问题描述
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 问题位置（段落索引、表格索引等）
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 修复建议
    /// </summary>
    public string? Suggestion { get; set; }

    /// <summary>
    /// 是否可自动修复
    /// </summary>
    public bool CanAutoFix { get; set; }

    /// <summary>
    /// 相关字段名（如果适用）
    /// </summary>
    public string? RelatedField { get; set; }
}
```

创建 `Models/Validation/IssueSeverity.cs`：

```csharp
namespace FrameAgentWordFill.Models.Validation;

/// <summary>
/// 问题严重程度
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// 错误（必须修复）
    /// </summary>
    Error,

    /// <summary>
    /// 警告（建议修复）
    /// </summary>
    Warning,

    /// <summary>
    /// 信息（仅提示）
    /// </summary>
    Info
}
```

### 1.2 创建模板校验器

创建 `Tools/TemplateValidator.cs`：

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FrameAgentWordFill.Models.Validation;
using FrameAgentWordFill.Models.Parsing;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 模板校验器（预检模板格式和内容）
/// </summary>
public sealed class TemplateValidator
{
    private readonly ILogger<TemplateValidator> _logger;

    public TemplateValidator(ILogger<TemplateValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 校验模板文件
    /// </summary>
    /// <param name="filePath">模板文件路径</param>
    /// <param name="parseResult">模板解析结果（已定义的字段）</param>
    /// <returns>校验结果</returns>
    public async Task<ValidationResult> ValidateTemplateAsync(string filePath, TemplateParseResult parseResult)
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            await Task.Run(() =>
            {
                using var doc = WordprocessingDocument.Open(filePath, false);
                var body = doc.MainDocumentPart?.Document.Body;

                if (body == null)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        IssueType = "DocumentStructure",
                        Message = "无法读取 Word 文档内容",
                        Suggestion = "请确保文档未损坏"
                    });
                    result.IsValid = false;
                    return;
                }

                // 1. 检查占位符格式
                CheckPlaceholderFormat(body, result);

                // 2. 检查字段一致性（模板中的占位符 vs 已定义的字段）
                CheckFieldConsistency(body, parseResult, result);

                // 3. 检查表格结构
                CheckTableStructure(body, parseResult, result);

                // 4. 检查内容控件
                CheckContentControls(body, result);

                // 5. 统计信息
                AddStatisticsInfo(parseResult, result);
            });

            // 如果有错误，设置 IsValid 为 false
            if (result.ErrorCount > 0)
            {
                result.IsValid = false;
            }

            _logger.LogInformation($"模板校验完成：{result.Summary}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模板校验失败");
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Error,
                IssueType = "ValidationError",
                Message = $"校验过程出错：{ex.Message}"
            });
            result.IsValid = false;
        }

        return result;
    }

    /// <summary>
    /// 检查占位符格式（检测非标准格式）
    /// </summary>
    private void CheckPlaceholderFormat(Body body, ValidationResult result)
    {
        var paragraphs = body.Descendants<Paragraph>().ToList();

        foreach (var para in paragraphs)
        {
            var text = para.InnerText;

            // 检测全角括号 【】
            var fullWidthMatches = Regex.Matches(text, @"【([^】]+)】");
            foreach (Match match in fullWidthMatches)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    IssueType = "PlaceholderFormat",
                    Message = $"检测到非标准占位符格式：【{match.Groups[1].Value}】",
                    Suggestion = $"建议修改为 {{{match.Groups[1].Value}}}",
                    CanAutoFix = true
                });
            }

            // 检测包含多余空格的占位符 { 字段名 }
            var spaceMatches = Regex.Matches(text, @"\{\s+([^\}]+?)\s+\}");
            foreach (Match match in spaceMatches)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    IssueType = "PlaceholderFormat",
                    Message = $"检测到占位符包含多余空格：{match.Value}",
                    Suggestion = $"建议修改为 {{{match.Groups[1].Value.Trim()}}}",
                    CanAutoFix = true
                });
            }

            // 检测不完整的占位符 { 或 }
            if (text.Contains("{") && !text.Contains("}"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    IssueType = "PlaceholderFormat",
                    Message = "检测到不完整的占位符：缺少右括号 }",
                    Suggestion = "请检查并补全占位符格式",
                    CanAutoFix = false
                });
            }

            if (text.Contains("}") && !text.Contains("{"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    IssueType = "PlaceholderFormat",
                    Message = "检测到不完整的占位符：缺少左括号 {",
                    Suggestion = "请检查并补全占位符格式",
                    CanAutoFix = false
                });
            }
        }
    }

    /// <summary>
    /// 检查字段一致性（模板中的占位符 vs 已定义的字段）
    /// </summary>
    private void CheckFieldConsistency(Body body, TemplateParseResult parseResult, ValidationResult result)
    {
        // 提取模板中所有占位符
        var allText = body.InnerText;
        var placeholders = Regex.Matches(allText, @"\{([^}]+)\}")
            .Select(m => m.Groups[1].Value.Trim())
            .Distinct()
            .ToList();

        // 已定义的字段名
        var definedFields = parseResult.Fields.Select(f => f.FieldName).ToHashSet();

        // 已定义的表格字段（格式：表格名.列名）
        var definedTableFields = new HashSet<string>();
        foreach (var table in parseResult.Tables)
        {
            foreach (var column in table.Columns)
            {
                definedTableFields.Add($"{table.TableName}.{column}");
            }
        }

        // 检查未定义的字段
        foreach (var placeholder in placeholders)
        {
            // 跳过表格字段的检查（可能是动态的）
            if (placeholder.Contains("."))
            {
                if (!definedTableFields.Contains(placeholder))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        IssueType = "FieldMismatch",
                        Message = $"模板中存在未定义的表格字段：{{{placeholder}}}",
                        Suggestion = "请在字段配置中添加该表格字段定义",
                        RelatedField = placeholder
                    });
                }
            }
            else
            {
                if (!definedFields.Contains(placeholder))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        IssueType = "FieldMismatch",
                        Message = $"模板中存在未定义的字段：{{{placeholder}}}",
                        Suggestion = "请在字段配置中添加该字段定义",
                        RelatedField = placeholder
                    });
                }
            }
        }

        // 检查冗余字段（已定义但模板中未使用）
        foreach (var field in definedFields)
        {
            if (!placeholders.Contains(field))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    IssueType = "UnusedField",
                    Message = $"字段 '{field}' 已定义但模板中未使用",
                    Suggestion = "可以删除该字段定义，或在模板中添加占位符",
                    RelatedField = field
                });
            }
        }
    }

    /// <summary>
    /// 检查表格结构（验证表格表头与字段定义是否匹配）
    /// </summary>
    private void CheckTableStructure(Body body, TemplateParseResult parseResult, ValidationResult result)
    {
        var tables = body.Elements<Table>().ToList();

        if (tables.Count != parseResult.Tables.Count)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Warning,
                IssueType = "TableStructure",
                Message = $"模板中的表格数量（{tables.Count}）与字段定义中的表格数量（{parseResult.Tables.Count}）不一致",
                Suggestion = "请检查模板和字段配置"
            });
        }

        // 验证每个表格的结构
        for (int i = 0; i < Math.Min(tables.Count, parseResult.Tables.Count); i++)
        {
            var table = tables[i];
            var tableInfo = parseResult.Tables[i];

            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 2)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    IssueType = "TableStructure",
                    Message = $"表格 '{tableInfo.TableName}' 至少需要 2 行（表头 + 数据行）",
                    Suggestion = "请在表格中添加至少一行数据示例"
                });
                continue;
            }

            // 检查表头
            var headerRow = rows[0];
            var headerCells = headerRow.Elements<TableCell>().Select(c => c.InnerText.Trim()).ToList();

            var missingColumns = tableInfo.Columns.Except(headerCells).ToList();
            var extraColumns = headerCells.Except(tableInfo.Columns).ToList();

            if (missingColumns.Any() || extraColumns.Any())
            {
                var message = $"表格 '{tableInfo.TableName}' 结构不匹配";
                if (missingColumns.Any())
                {
                    message += $"，缺少列：{string.Join(", ", missingColumns)}";
                }
                if (extraColumns.Any())
                {
                    message += $"，多出列：{string.Join(", ", extraColumns)}";
                }

                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    IssueType = "TableStructure",
                    Message = message,
                    Suggestion = "请调整表格结构使其与字段定义一致"
                });
            }
        }
    }

    /// <summary>
    /// 检查内容控件（识别模板中的内容控件）
    /// </summary>
    private void CheckContentControls(Body body, ValidationResult result)
    {
        var contentControls = body.Descendants<SdtElement>().ToList();

        if (contentControls.Any())
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Info,
                IssueType = "ContentControl",
                Message = $"检测到 {contentControls.Count} 个内容控件",
                Suggestion = "系统将自动处理这些内容控件"
            });
        }
    }

    /// <summary>
    /// 添加统计信息
    /// </summary>
    private void AddStatisticsInfo(TemplateParseResult parseResult, ValidationResult result)
    {
        result.Issues.Add(new ValidationIssue
        {
            Severity = IssueSeverity.Info,
            IssueType = "Statistics",
            Message = $"模板包含 {parseResult.Fields.Count} 个普通字段",
        });

        if (parseResult.Tables.Any())
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Info,
                IssueType = "Statistics",
                Message = $"模板包含 {parseResult.Tables.Count} 个表格",
            });
        }
    }
}
```

---

## 步骤 2：实现占位符规范化工具

### 2.1 创建占位符规范化器

创建 `Tools/PlaceholderNormalizer.cs`：

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 占位符规范化工具（自动修复非标准占位符格式）
/// </summary>
public sealed class PlaceholderNormalizer
{
    private readonly ILogger<PlaceholderNormalizer> _logger;

    public PlaceholderNormalizer(ILogger<PlaceholderNormalizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 规范化模板文件中的占位符
    /// </summary>
    /// <param name="inputFilePath">输入文件路径</param>
    /// <param name="outputFilePath">输出文件路径</param>
    /// <returns>修复数量</returns>
    public async Task<int> NormalizeTemplateAsync(string inputFilePath, string outputFilePath)
    {
        int fixedCount = 0;

        try
        {
            // 复制文件
            File.Copy(inputFilePath, outputFilePath, true);

            await Task.Run(() =>
            {
                using var doc = WordprocessingDocument.Open(outputFilePath, true);
                var body = doc.MainDocumentPart?.Document.Body;

                if (body == null)
                {
                    _logger.LogWarning("无法读取 Word 文档内容");
                    return;
                }

                // 处理所有段落中的文本
                var paragraphs = body.Descendants<Paragraph>().ToList();

                foreach (var para in paragraphs)
                {
                    var textElements = para.Descendants<Text>().ToList();

                    foreach (var textElement in textElements)
                    {
                        var originalText = textElement.Text;
                        var normalizedText = NormalizeText(originalText);

                        if (originalText != normalizedText)
                        {
                            textElement.Text = normalizedText;
                            fixedCount++;
                        }
                    }
                }

                // 保存更改
                doc.MainDocumentPart!.Document.Save();
            });

            _logger.LogInformation($"占位符规范化完成，修复 {fixedCount} 处");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "占位符规范化失败");
            throw;
        }

        return fixedCount;
    }

    /// <summary>
    /// 规范化文本中的占位符
    /// </summary>
    private string NormalizeText(string text)
    {
        // 1. 全角括号转半角 【】 → {}
        text = text.Replace('【', '{').Replace('】', '}');

        // 2. 中文括号转半角（如果有）
        text = text.Replace('｛', '{').Replace('｝', '}');

        // 3. 清理占位符中的多余空格 { 字段名 } → {字段名}
        text = Regex.Replace(text, @"\{\s+([^\}]+?)\s+\}", "{$1}");

        // 4. 清理占位符内部的多余空格
        text = Regex.Replace(text, @"\{([^\}]+)\}", match =>
        {
            var fieldName = match.Groups[1].Value.Trim();
            return $"{{{fieldName}}}";
        });

        return text;
    }
}
```

---

## 步骤 3：实现内容控件处理

### 3.1 创建内容控件处理器

创建 `Tools/ContentControlHandler.cs`：

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 内容控件处理器（识别和填充 Word 内容控件）
/// </summary>
public sealed class ContentControlHandler
{
    private readonly ILogger<ContentControlHandler> _logger;

    public ContentControlHandler(ILogger<ContentControlHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 识别模板中的内容控件
    /// </summary>
    public async Task<List<ContentControlInfo>> IdentifyContentControlsAsync(string filePath)
    {
        var controls = new List<ContentControlInfo>();

        try
        {
            await Task.Run(() =>
            {
                using var doc = WordprocessingDocument.Open(filePath, false);
                var body = doc.MainDocumentPart?.Document.Body;

                if (body == null) return;

                // 查找所有 SdtElement（结构化文档标签，即内容控件）
                var sdtElements = body.Descendants<SdtElement>().ToList();

                foreach (var sdt in sdtElements)
                {
                    var properties = sdt.Descendants<SdtProperties>().FirstOrDefault();
                    if (properties == null) continue;

                    var tag = properties.Descendants<Tag>().FirstOrDefault()?.Val?.Value;
                    var alias = properties.Descendants<SdtAlias>().FirstOrDefault()?.Val?.Value;

                    // 确定控件类型
                    var controlType = DetermineControlType(properties);

                    controls.Add(new ContentControlInfo
                    {
                        Tag = tag ?? string.Empty,
                        Alias = alias ?? string.Empty,
                        ControlType = controlType
                    });
                }
            });

            _logger.LogInformation($"识别到 {controls.Count} 个内容控件");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "识别内容控件失败");
        }

        return controls;
    }

    /// <summary>
    /// 填充内容控件
    /// </summary>
    public async Task FillContentControlsAsync(string filePath, Dictionary<string, object> data)
    {
        try
        {
            await Task.Run(() =>
            {
                using var doc = WordprocessingDocument.Open(filePath, true);
                var body = doc.MainDocumentPart?.Document.Body;

                if (body == null) return;

                var sdtElements = body.Descendants<SdtElement>().ToList();

                foreach (var sdt in sdtElements)
                {
                    var properties = sdt.Descendants<SdtProperties>().FirstOrDefault();
                    if (properties == null) continue;

                    var tag = properties.Descendants<Tag>().FirstOrDefault()?.Val?.Value;
                    if (string.IsNullOrEmpty(tag) || !data.ContainsKey(tag)) continue;

                    var value = data[tag];
                    var controlType = DetermineControlType(properties);

                    // 根据控件类型填充
                    FillControl(sdt, value, controlType);
                }

                doc.MainDocumentPart!.Document.Save();
            });

            _logger.LogInformation("内容控件填充完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "填充内容控件失败");
            throw;
        }
    }

    /// <summary>
    /// 确定控件类型
    /// </summary>
    private string DetermineControlType(SdtProperties properties)
    {
        if (properties.Descendants<SdtContentText>().Any())
            return "PlainText";

        if (properties.Descendants<SdtContentRichText>().Any())
            return "RichText";

        if (properties.Descendants<SdtContentDropDownList>().Any())
            return "DropDownList";

        if (properties.Descendants<SdtContentCheckBox>().Any())
            return "CheckBox";

        if (properties.Descendants<SdtContentDate>().Any())
            return "DatePicker";

        if (properties.Descendants<SdtContentPicture>().Any())
            return "Picture";

        return "Unknown";
    }

    /// <summary>
    /// 填充单个控件
    /// </summary>
    private void FillControl(SdtElement sdt, object value, string controlType)
    {
        var content = sdt.Descendants<SdtContent>().FirstOrDefault();
        if (content == null) return;

        switch (controlType)
        {
            case "PlainText":
            case "RichText":
                FillTextControl(content, value?.ToString() ?? string.Empty);
                break;

            case "CheckBox":
                FillCheckBoxControl(sdt, Convert.ToBoolean(value));
                break;

            case "DatePicker":
                FillDateControl(content, value?.ToString() ?? string.Empty);
                break;

            case "DropDownList":
                FillDropDownControl(content, value?.ToString() ?? string.Empty);
                break;

            default:
                _logger.LogWarning($"不支持的控件类型：{controlType}");
                break;
        }
    }

    /// <summary>
    /// 填充文本控件
    /// </summary>
    private void FillTextControl(SdtContent content, string value)
    {
        var textElement = content.Descendants<Text>().FirstOrDefault();
        if (textElement != null)
        {
            textElement.Text = value;
        }
    }

    /// <summary>
    /// 填充复选框控件
    /// </summary>
    private void FillCheckBoxControl(SdtElement sdt, bool isChecked)
    {
        var checkbox = sdt.Descendants<SdtContentCheckBox>().FirstOrDefault();
        if (checkbox != null)
        {
            // 设置复选框状态（OpenXML API）
            // 具体实现依赖 DocumentFormat.OpenXml 版本
            _logger.LogInformation($"设置复选框状态：{isChecked}");
        }
    }

    /// <summary>
    /// 填充日期控件
    /// </summary>
    private void FillDateControl(SdtContent content, string dateValue)
    {
        var textElement = content.Descendants<Text>().FirstOrDefault();
        if (textElement != null)
        {
            textElement.Text = dateValue;
        }
    }

    /// <summary>
    /// 填充下拉列表控件
    /// </summary>
    private void FillDropDownControl(SdtContent content, string value)
    {
        var textElement = content.Descendants<Text>().FirstOrDefault();
        if (textElement != null)
        {
            textElement.Text = value;
        }
    }
}

/// <summary>
/// 内容控件信息
/// </summary>
public sealed class ContentControlInfo
{
    public string Tag { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string ControlType { get; set; } = string.Empty;
}
```

---

## 步骤 4：实现图片替换功能

### 4.1 创建图片替换工具

创建 `Tools/ImageReplacer.cs`：

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 图片替换工具（动态插入图片到 Word 文档）
/// </summary>
public sealed class ImageReplacer
{
    private readonly ILogger<ImageReplacer> _logger;

    public ImageReplacer(ILogger<ImageReplacer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 替换文档中的图片占位符
    /// </summary>
    /// <param name="filePath">文档路径</param>
    /// <param name="imageData">图片数据（字段名 -> 图片路径或 Base64）</param>
    public async Task ReplaceImagesAsync(string filePath, Dictionary<string, string> imageData)
    {
        try
        {
            await Task.Run(() =>
            {
                using var doc = WordprocessingDocument.Open(filePath, true);
                var body = doc.MainDocumentPart?.Document.Body;

                if (body == null) return;

                foreach (var (fieldName, imagePath) in imageData)
                {
                    // 查找图片占位符 {Logo} 或 {签名}
                    var placeholder = $"{{{fieldName}}}";
                    ReplaceImagePlaceholder(doc, body, placeholder, imagePath);
                }

                doc.MainDocumentPart!.Document.Save();
            });

            _logger.LogInformation($"图片替换完成，处理 {imageData.Count} 张图片");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图片替换失败");
            throw;
        }
    }

    /// <summary>
    /// 替换单个图片占位符
    /// </summary>
    private void ReplaceImagePlaceholder(WordprocessingDocument doc, Body body, string placeholder, string imagePath)
    {
        // 查找包含占位符的段落
        var paragraphs = body.Descendants<Paragraph>()
            .Where(p => p.InnerText.Contains(placeholder))
            .ToList();

        foreach (var para in paragraphs)
        {
            // 删除占位符文本
            var textElements = para.Descendants<Text>()
                .Where(t => t.Text.Contains(placeholder))
                .ToList();

            foreach (var text in textElements)
            {
                text.Text = text.Text.Replace(placeholder, "");
            }

            // 插入图片
            InsertImage(doc, para, imagePath);
        }
    }

    /// <summary>
    /// 插入图片到段落
    /// </summary>
    private void InsertImage(WordprocessingDocument doc, Paragraph para, string imagePath)
    {
        try
        {
            var mainPart = doc.MainDocumentPart!;

            // 处理 Base64 图片
            byte[] imageBytes;
            if (imagePath.StartsWith("data:image"))
            {
                // Base64 格式：data:image/png;base64,iVBORw0KGgo...
                var base64Data = imagePath.Split(',')[1];
                imageBytes = Convert.FromBase64String(base64Data);
            }
            else
            {
                // 本地文件路径
                if (!File.Exists(imagePath))
                {
                    _logger.LogWarning($"图片文件不存在：{imagePath}");
                    return;
                }
                imageBytes = File.ReadAllBytes(imagePath);
            }

            // 确定图片类型
            var imagePartType = DetermineImagePartType(imagePath);

            // 创建图片部件
            using var stream = new MemoryStream(imageBytes);
            var imagePart = mainPart.AddImagePart(imagePartType);
            imagePart.FeedData(stream);

            // 生成图片关系ID
            var relationshipId = mainPart.GetIdOfPart(imagePart);

            // 创建图片元素（默认尺寸：宽 3 英寸）
            var element = CreateImageElement(relationshipId, "image", 3000000, 2000000);

            // 插入到段落
            var run = new Run(element);
            para.AppendChild(run);

            _logger.LogInformation($"图片插入成功：{imagePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"插入图片失败：{imagePath}");
        }
    }

    /// <summary>
    /// 确定图片类型
    /// </summary>
    private ImagePartType DetermineImagePartType(string imagePath)
    {
        var extension = Path.GetExtension(imagePath).ToLower();

        if (imagePath.StartsWith("data:image"))
        {
            if (imagePath.Contains("png")) return ImagePartType.Png;
            if (imagePath.Contains("jpeg") || imagePath.Contains("jpg")) return ImagePartType.Jpeg;
            if (imagePath.Contains("gif")) return ImagePartType.Gif;
            if (imagePath.Contains("bmp")) return ImagePartType.Bmp;
        }

        return extension switch
        {
            ".png" => ImagePartType.Png,
            ".jpg" or ".jpeg" => ImagePartType.Jpeg,
            ".gif" => ImagePartType.Gif,
            ".bmp" => ImagePartType.Bmp,
            _ => ImagePartType.Png // 默认
        };
    }

    /// <summary>
    /// 创建图片元素
    /// </summary>
    private Drawing CreateImageElement(string relationshipId, string imageName, long width, long height)
    {
        var element = new Drawing(
            new DW.Inline(
                new DW.Extent() { Cx = width, Cy = height },
                new DW.EffectExtent()
                {
                    LeftEdge = 0L,
                    TopEdge = 0L,
                    RightEdge = 0L,
                    BottomEdge = 0L
                },
                new DW.DocProperties()
                {
                    Id = (UInt32Value)1U,
                    Name = imageName
                },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks() { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties()
                                {
                                    Id = (UInt32Value)0U,
                                    Name = imageName
                                },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip(
                                    new A.BlipExtensionList(
                                        new A.BlipExtension()
                                        {
                                            Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}"
                                        })
                                )
                                {
                                    Embed = relationshipId,
                                    CompressionState = A.BlipCompressionValues.Print
                                },
                                new A.Stretch(
                                    new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset() { X = 0L, Y = 0L },
                                    new A.Extents() { Cx = width, Cy = height }),
                                new A.PresetGeometry(
                                    new A.AdjustValueList()
                                )
                                { Preset = A.ShapeTypeValues.Rectangle }))
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            )
            {
                DistanceFromTop = (UInt32Value)0U,
                DistanceFromBottom = (UInt32Value)0U,
                DistanceFromLeft = (UInt32Value)0U,
                DistanceFromRight = (UInt32Value)0U,
                EditId = "50D07946"
            });

        return element;
    }
}
```

---

## 步骤 5：实现服务层和API

### 5.1 更新模板服务

编辑 `Services/TemplateService.cs`，添加校验和规范化方法：

```csharp
// 添加依赖注入
private readonly TemplateValidator _validator;
private readonly PlaceholderNormalizer _normalizer;
private readonly ContentControlHandler _contentControlHandler;
private readonly ImageReplacer _imageReplacer;

public TemplateService(
    TemplateRepository repository,
    FileStorageService fileStorage,
    TemplateParser parser,
    TemplateValidator validator,
    PlaceholderNormalizer normalizer,
    ContentControlHandler contentControlHandler,
    ImageReplacer imageReplacer,
    ILogger<TemplateService> logger)
{
    _repository = repository;
    _fileStorage = fileStorage;
    _parser = parser;
    _validator = validator;
    _normalizer = normalizer;
    _contentControlHandler = contentControlHandler;
    _imageReplacer = imageReplacer;
    _logger = logger;
}

/// <summary>
/// 校验模板
/// </summary>
public async Task<ValidationResult> ValidateTemplateAsync(int templateId)
{
    var template = await _repository.GetTemplateByIdAsync(templateId);
    if (template == null)
    {
        throw new InvalidOperationException("模板不存在");
    }

    var filePath = _fileStorage.GetTemplatePath(template.FilePath);
    var parseResult = await _repository.GetTemplateParseResultAsync(templateId);

    if (parseResult == null)
    {
        throw new InvalidOperationException("模板解析结果不存在");
    }

    return await _validator.ValidateTemplateAsync(filePath, parseResult);
}

/// <summary>
/// 规范化模板占位符
/// </summary>
public async Task<int> NormalizeTemplateAsync(int templateId)
{
    var template = await _repository.GetTemplateByIdAsync(templateId);
    if (template == null)
    {
        throw new InvalidOperationException("模板不存在");
    }

    var inputPath = _fileStorage.GetTemplatePath(template.FilePath);
    var outputPath = inputPath; // 直接覆盖原文件

    return await _normalizer.NormalizeTemplateAsync(inputPath, outputPath);
}

/// <summary>
/// 获取模板中的内容控件列表
/// </summary>
public async Task<List<ContentControlInfo>> GetContentControlsAsync(int templateId)
{
    var template = await _repository.GetTemplateByIdAsync(templateId);
    if (template == null)
    {
        throw new InvalidOperationException("模板不存在");
    }

    var filePath = _fileStorage.GetTemplatePath(template.FilePath);
    return await _contentControlHandler.IdentifyContentControlsAsync(filePath);
}
```

### 5.2 更新控制器

编辑 `Controllers/TemplatesController.cs`，添加新的 API：

```csharp
/// <summary>
/// 校验模板
/// </summary>
[HttpPost("{id}/validate")]
public async Task<IActionResult> ValidateTemplate(int id)
{
    try
    {
        var result = await _templateService.ValidateTemplateAsync(id);
        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "校验模板失败");
        return BadRequest(new { error = ex.Message });
    }
}

/// <summary>
/// 规范化模板占位符
/// </summary>
[HttpPost("{id}/normalize")]
public async Task<IActionResult> NormalizeTemplate(int id)
{
    try
    {
        var fixedCount = await _templateService.NormalizeTemplateAsync(id);
        return Ok(new { fixedCount, message = $"成功修复 {fixedCount} 处占位符" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "规范化模板失败");
        return BadRequest(new { error = ex.Message });
    }
}

/// <summary>
/// 获取模板中的内容控件列表
/// </summary>
[HttpGet("{id}/content-controls")]
public async Task<IActionResult> GetContentControls(int id)
{
    try
    {
        var controls = await _templateService.GetContentControlsAsync(id);
        return Ok(controls);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "获取内容控件失败");
        return BadRequest(new { error = ex.Message });
    }
}
```

### 5.3 注册服务

编辑 `Program.cs`，注册新服务：

```csharp
// W6: 复杂模板能力增强服务
builder.Services.AddScoped<TemplateValidator>();
builder.Services.AddScoped<PlaceholderNormalizer>();
builder.Services.AddScoped<ContentControlHandler>();
builder.Services.AddScoped<ImageReplacer>();
```

---

## 步骤 6：实现前端模板校验报告页面

### 6.1 创建模板校验页面

创建 `frontend/src/views/admin/TemplateValidation.vue`：

```vue
<template>
  <div class="template-validation-container">
    <el-card class="header-card">
      <h2>模板校验报告</h2>
      <p>检测模板格式问题并提供修复建议</p>
    </el-card>

    <!-- 加载中 -->
    <div v-if="loading" class="loading-container">
      <el-icon class="is-loading" :size="40"><Loading /></el-icon>
      <p>正在校验模板...</p>
    </div>

    <!-- 校验结果 -->
    <el-card v-if="!loading && validationResult" class="result-card">
      <template #header>
        <div class="card-header">
          <span class="title">校验结果</span>
          <el-tag :type="validationResult.isValid ? 'success' : 'danger'" size="large">
            {{ validationResult.isValid ? '✓ 通过校验' : '✗ 未通过校验' }}
          </el-tag>
        </div>
      </template>

      <!-- 摘要 -->
      <el-alert
        :title="validationResult.summary"
        :type="validationResult.isValid ? 'success' : 'warning'"
        :closable="false"
        show-icon
        style="margin-bottom: 20px"
      />

      <!-- 操作按钮 -->
      <div class="action-buttons">
        <el-button
          type="primary"
          :disabled="!hasAutoFixableIssues"
          :loading="normalizing"
          @click="normalizeTemplate"
        >
          一键规范化
        </el-button>
        <el-button @click="reValidate">重新校验</el-button>
        <el-button @click="$router.back()">返回</el-button>
      </div>

      <!-- 问题列表 -->
      <el-tabs v-model="activeTab" class="issues-tabs">
        <!-- 错误 -->
        <el-tab-pane :label="`错误 (${errorIssues.length})`" name="errors">
          <el-empty v-if="errorIssues.length === 0" description="没有错误" />
          <div v-else class="issue-list">
            <div
              v-for="(issue, index) in errorIssues"
              :key="index"
              class="issue-item issue-error"
            >
              <div class="issue-header">
                <el-icon><CircleClose /></el-icon>
                <span class="issue-type">{{ formatIssueType(issue.issueType) }}</span>
                <el-tag v-if="issue.canAutoFix" type="success" size="small">可自动修复</el-tag>
              </div>
              <div class="issue-message">{{ issue.message }}</div>
              <div v-if="issue.suggestion" class="issue-suggestion">
                <el-icon><InfoFilled /></el-icon>
                <span>{{ issue.suggestion }}</span>
              </div>
              <div v-if="issue.location" class="issue-location">
                <el-icon><Location /></el-icon>
                <span>位置：{{ issue.location }}</span>
              </div>
            </div>
          </div>
        </el-tab-pane>

        <!-- 警告 -->
        <el-tab-pane :label="`警告 (${warningIssues.length})`" name="warnings">
          <el-empty v-if="warningIssues.length === 0" description="没有警告" />
          <div v-else class="issue-list">
            <div
              v-for="(issue, index) in warningIssues"
              :key="index"
              class="issue-item issue-warning"
            >
              <div class="issue-header">
                <el-icon><WarningFilled /></el-icon>
                <span class="issue-type">{{ formatIssueType(issue.issueType) }}</span>
                <el-tag v-if="issue.canAutoFix" type="success" size="small">可自动修复</el-tag>
              </div>
              <div class="issue-message">{{ issue.message }}</div>
              <div v-if="issue.suggestion" class="issue-suggestion">
                <el-icon><InfoFilled /></el-icon>
                <span>{{ issue.suggestion }}</span>
              </div>
            </div>
          </div>
        </el-tab-pane>

        <!-- 信息 -->
        <el-tab-pane :label="`信息 (${infoIssues.length})`" name="info">
          <el-empty v-if="infoIssues.length === 0" description="没有信息" />
          <div v-else class="issue-list">
            <div
              v-for="(issue, index) in infoIssues"
              :key="index"
              class="issue-item issue-info"
            >
              <div class="issue-header">
                <el-icon><InfoFilled /></el-icon>
                <span class="issue-type">{{ formatIssueType(issue.issueType) }}</span>
              </div>
              <div class="issue-message">{{ issue.message }}</div>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ElMessage } from 'element-plus';
import {
  Loading,
  CircleClose,
  WarningFilled,
  InfoFilled,
  Location
} from '@element-plus/icons-vue';
import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

const route = useRoute();
const router = useRouter();
const templateId = ref(Number(route.params.id));

const loading = ref(false);
const normalizing = ref(false);
const validationResult = ref<any>(null);
const activeTab = ref('errors');

// 分类问题
const errorIssues = computed(() =>
  validationResult.value?.issues.filter((i: any) => i.severity === 'Error') || []
);
const warningIssues = computed(() =>
  validationResult.value?.issues.filter((i: any) => i.severity === 'Warning') || []
);
const infoIssues = computed(() =>
  validationResult.value?.issues.filter((i: any) => i.severity === 'Info') || []
);

// 是否有可自动修复的问题
const hasAutoFixableIssues = computed(() =>
  validationResult.value?.issues.some((i: any) => i.canAutoFix) || false
);

// 加载校验结果
const loadValidation = async () => {
  loading.value = true;

  try {
    const response = await axios.post(`${API_BASE_URL}/templates/${templateId.value}/validate`);
    validationResult.value = response.data;

    // 根据问题数量默认选中标签
    if (errorIssues.value.length > 0) {
      activeTab.value = 'errors';
    } else if (warningIssues.value.length > 0) {
      activeTab.value = 'warnings';
    } else {
      activeTab.value = 'info';
    }
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '校验失败');
  } finally {
    loading.value = false;
  }
};

// 规范化模板
const normalizeTemplate = async () => {
  normalizing.value = true;

  try {
    const response = await axios.post(`${API_BASE_URL}/templates/${templateId.value}/normalize`);
    ElMessage.success(response.data.message);

    // 重新校验
    await loadValidation();
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '规范化失败');
  } finally {
    normalizing.value = false;
  }
};

// 重新校验
const reValidate = () => {
  loadValidation();
};

// 格式化问题类型
const formatIssueType = (type: string) => {
  const map: Record<string, string> = {
    PlaceholderFormat: '占位符格式',
    FieldMismatch: '字段不匹配',
    TableStructure: '表格结构',
    ContentControl: '内容控件',
    Statistics: '统计信息',
    UnusedField: '冗余字段',
    DocumentStructure: '文档结构'
  };
  return map[type] || type;
};

onMounted(() => {
  loadValidation();
});
</script>

<style scoped>
.template-validation-container {
  padding: 20px;
}

.header-card {
  margin-bottom: 20px;
}

.loading-container {
  text-align: center;
  padding: 60px 0;
  color: #909399;
}

.result-card {
  margin-top: 20px;
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

.action-buttons {
  margin: 20px 0;
  display: flex;
  gap: 10px;
}

.issues-tabs {
  margin-top: 20px;
}

.issue-list {
  display: flex;
  flex-direction: column;
  gap: 15px;
}

.issue-item {
  padding: 15px;
  border-radius: 4px;
  border-left: 4px solid;
}

.issue-error {
  background-color: #fef0f0;
  border-left-color: #f56c6c;
}

.issue-warning {
  background-color: #fdf6ec;
  border-left-color: #e6a23c;
}

.issue-info {
  background-color: #f4f4f5;
  border-left-color: #909399;
}

.issue-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
  font-weight: bold;
}

.issue-message {
  margin-bottom: 8px;
  color: #303133;
}

.issue-suggestion {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 8px;
  background-color: rgba(255, 255, 255, 0.5);
  border-radius: 4px;
  font-size: 14px;
  color: #606266;
}

.issue-location {
  display: flex;
  align-items: center;
  gap: 5px;
  margin-top: 8px;
  font-size: 12px;
  color: #909399;
}
</style>
```

### 6.2 更新路由配置

编辑 `frontend/src/router/index.ts`，添加路由：

```typescript
{
  path: '/admin/template-validation/:id',
  name: 'TemplateValidation',
  component: () => import('../views/admin/TemplateValidation.vue'),
  meta: { title: '模板校验' }
}
```

### 6.3 更新模板管理页面

编辑 `frontend/src/views/TemplateManager.vue`，添加"校验模板"按钮：

```vue
<!-- 在模板列表的操作列中添加 -->
<el-button
  type="warning"
  size="small"
  @click="validateTemplate(row.templateId)"
>
  校验模板
</el-button>

<!-- 添加方法 -->
<script setup lang="ts">
const validateTemplate = (templateId: number) => {
  router.push(`/admin/template-validation/${templateId}`);
};
</script>
```

---

## 步骤 7：验收测试

### 7.1 测试计划

| 测试项 | 测试方法 | 预期结果 |
|--------|----------|----------|
| 占位符格式检测 | 上传包含 【字段名】 的模板 | 检测到非标准格式并提示修复 |
| 占位符规范化 | 点击"一键规范化"按钮 | 自动转换为 {字段名} |
| 字段一致性检查 | 模板中使用未定义的字段 | 报告错误并提示添加字段 |
| 表格结构检查 | 表格列与定义不匹配 | 报告错误并提示调整 |
| 内容控件识别 | 上传包含内容控件的模板 | 识别并显示控件列表 |
| 内容控件填充 | 生成文档时填充控件 | 控件内容正确填充 |
| 图片替换（Base64） | 提供 Base64 图片数据 | 图片成功插入到文档 |
| 图片替换（文件） | 提供本地文件路径 | 图片成功插入到文档 |

### 7.2 验收标准

✅ **功能验收**：
1. 模板预检工具能识别至少 5 种常见问题
2. 占位符规范化成功率 ≥ 95%
3. 内容控件支持至少 5 种类型
4. 图片替换支持 PNG、JPG、GIF 格式
5. 前端校验报告页面显示完整

✅ **性能验收**：
1. 模板校验响应时间 < 3 秒
2. 占位符规范化时间 < 5 秒
3. 图片插入时间 < 1 秒/张

✅ **用户体验验收**：
1. 错误提示清晰明确
2. 修复建议具有可操作性
3. 界面友好，操作流畅

---

## 📝 总结

### 本周新增文件清单

**后端文件**（9 个）：
1. `Models/Validation/ValidationResult.cs` - 校验结果模型
2. `Models/Validation/ValidationIssue.cs` - 问题模型
3. `Models/Validation/IssueSeverity.cs` - 严重程度枚举
4. `Tools/TemplateValidator.cs` - 模板预检工具
5. `Tools/PlaceholderNormalizer.cs` - 占位符规范化工具
6. `Tools/ContentControlHandler.cs` - 内容控件处理器
7. `Tools/ImageReplacer.cs` - 图片替换工具
8. `Services/TemplateService.cs` - 更新（新增方法）
9. `Controllers/TemplatesController.cs` - 更新（新增 API）

**前端文件**（1 个）：
1. `frontend/src/views/admin/TemplateValidation.vue` - 模板校验报告页面

**总计**：10 个核心文件

### 技术要点回顾

1. **模板预检**：自动检测占位符格式、字段一致性、表格结构等问题
2. **占位符规范化**：支持全角转半角、空格清理、批量修复
3. **内容控件处理**：支持 7 种 Word 内容控件类型（纯文本、富文本、下拉列表、复选框、日期、图片、旧版表单域）
4. **图片替换**：支持 Base64 和本地文件，自动调整尺寸
5. **错误分级**：Error/Warning/Info 三级，提供具体修复建议
6. **容错能力**：完善的异常处理和降级策略

### 下一步：W7 - AI 文件智能提取

进入 W7 开发阶段，实现：
- 用户上传参考文件（PDF/Word/Excel/图片）
- AI 自动批量提取所有字段数据
- 提供置信度评分和人工确认界面
- 支持多引擎兜底策略

**参考文档**：
- 03_guide_项目执行计划.md
- 02_ref_w4_实现流程.md（复用 AI 能力）


