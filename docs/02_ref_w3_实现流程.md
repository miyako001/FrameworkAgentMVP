# W3 实现流程 - 文档生成核心闭环

**周期**：第 3 周  
**里程碑**：M4 - 文档生成闭环  
**目标**：实现"模板 + 数据 → Word 输出"的核心功能，支持普通字段和表格字段填充

---

## 📋 实施步骤总览

```
步骤1: 实现占位符替换引擎
    ↓
步骤2: 实现表格填充引擎
    ↓
步骤3: 实现文档生成服务
    ↓
步骤4: 实现文档生成 API
    ↓
步骤5: 实现前端测试界面
    ↓
步骤6: 端到端联调测试
    ↓
步骤7: 验收测试
```

---

## 步骤 1：实现占位符替换引擎

### 1.1 创建文档生成工具

创建 `Tools/DocGenerator.cs`：

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FrameAgentWordFill.Models;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// Word 文档生成工具（普通字段替换 + 表格填充）
/// </summary>
public sealed class DocGenerator
{
    private readonly ILogger<DocGenerator> _logger;

    public DocGenerator(ILogger<DocGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成填充后的文档
    /// </summary>
    /// <param name="templatePath">模板文件路径</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="fieldData">字段数据（键值对）</param>
    /// <param name="tableData">表格数据（表格名 → 行数据列表）</param>
    /// <returns>生成是否成功</returns>
    public async Task<(bool Success, string? ErrorMessage)> GenerateDocumentAsync(
        string templatePath,
        string outputPath,
        Dictionary<string, string> fieldData,
        Dictionary<string, List<Dictionary<string, string>>> tableData)
    {
        try
        {
            // 1. 复制模板文件到输出路径
            await Task.Run(() => File.Copy(templatePath, outputPath, true));

            // 2. 打开文档进行编辑
            using var document = WordprocessingDocument.Open(outputPath, true);
            if (document.MainDocumentPart == null)
            {
                return (false, "无法打开文档：MainDocumentPart 为空");
            }

            var body = document.MainDocumentPart.Document.Body;
            if (body == null)
            {
                return (false, "文档内容为空");
            }

            // 3. 替换普通字段
            ReplaceFieldsInDocument(body, fieldData);

            // 4. 填充表格
            FillTablesInDocument(body, tableData);

            // 5. 保存文档
            document.MainDocumentPart.Document.Save();

            _logger.LogInformation("文档生成成功: {OutputPath}", outputPath);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成文档失败: {TemplatePath} -> {OutputPath}", templatePath, outputPath);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 替换文档中的普通字段占位符
    /// </summary>
    private void ReplaceFieldsInDocument(Body body, Dictionary<string, string> fieldData)
    {
        // 获取所有文本元素（Text nodes）
        var textElements = body.Descendants<Text>().ToList();

        foreach (var textElement in textElements)
        {
            if (string.IsNullOrWhiteSpace(textElement.Text))
                continue;

            var originalText = textElement.Text;
            var replacedText = originalText;

            // 替换所有匹配的占位符
            foreach (var kvp in fieldData)
            {
                var placeholder = $"{{{kvp.Key}}}";
                if (replacedText.Contains(placeholder))
                {
                    replacedText = replacedText.Replace(placeholder, kvp.Value);
                    _logger.LogDebug("替换字段: {Placeholder} -> {Value}", placeholder, kvp.Value);
                }
            }

            // 如果有替换，更新文本
            if (replacedText != originalText)
            {
                textElement.Text = replacedText;
            }
        }
    }

    /// <summary>
    /// 填充文档中的表格
    /// </summary>
    private void FillTablesInDocument(Body body, Dictionary<string, List<Dictionary<string, string>>> tableData)
    {
        var tables = body.Elements<Table>().ToList();

        foreach (var table in tables)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 2) // 至少需要表头 + 1个数据行模板
                continue;

            // 第一行是表头，第二行是数据行模板
            var headerRow = rows[0];
            var templateRow = rows[1];

            // 识别表格名称（从表头第一个单元格的占位符提取）
            var tableName = ExtractTableNameFromHeader(headerRow);
            if (string.IsNullOrEmpty(tableName) || !tableData.ContainsKey(tableName))
                continue;

            _logger.LogInformation("填充表格: {TableName}, 数据行数: {RowCount}", 
                tableName, tableData[tableName].Count);

            // 获取列映射（占位符 → 列索引）
            var columnMapping = GetColumnMapping(headerRow, tableName);

            // 删除原有的数据行（保留表头）
            for (int i = rows.Count - 1; i >= 1; i--)
            {
                rows[i].Remove();
            }

            // 根据数据创建新行
            var dataRows = tableData[tableName];
            foreach (var rowData in dataRows)
            {
                // 克隆模板行
                var newRow = (TableRow)templateRow.CloneNode(true);
                
                // 填充单元格数据
                FillTableRow(newRow, rowData, columnMapping);
                
                // 添加到表格
                table.AppendChild(newRow);
            }
        }
    }

    /// <summary>
    /// 从表头提取表格名称
    /// </summary>
    private string? ExtractTableNameFromHeader(TableRow headerRow)
    {
        var cells = headerRow.Elements<TableCell>().ToList();
        if (cells.Count == 0)
            return null;

        var firstCellText = GetCellText(cells[0]);
        
        // 匹配 {表格名.字段名} 格式
        var match = System.Text.RegularExpressions.Regex.Match(
            firstCellText, 
            @"\{([a-zA-Z0-9_\u4e00-\u9fa5]+)\.([a-zA-Z0-9_\u4e00-\u9fa5]+)\}"
        );

        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 获取列映射（字段名 → 列索引）
    /// </summary>
    private Dictionary<string, int> GetColumnMapping(TableRow headerRow, string tableName)
    {
        var mapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cells = headerRow.Elements<TableCell>().ToList();

        for (int i = 0; i < cells.Count; i++)
        {
            var cellText = GetCellText(cells[i]);
            
            // 匹配 {表格名.字段名} 格式
            var match = System.Text.RegularExpressions.Regex.Match(
                cellText, 
                @"\{" + tableName + @"\.([a-zA-Z0-9_\u4e00-\u9fa5]+)\}"
            );

            if (match.Success)
            {
                var columnName = match.Groups[1].Value;
                mapping[columnName] = i;
                _logger.LogDebug("列映射: {ColumnName} -> 列索引 {Index}", columnName, i);
            }
        }

        return mapping;
    }

    /// <summary>
    /// 填充表格行数据
    /// </summary>
    private void FillTableRow(
        TableRow row, 
        Dictionary<string, string> rowData, 
        Dictionary<string, int> columnMapping)
    {
        var cells = row.Elements<TableCell>().ToList();

        foreach (var kvp in rowData)
        {
            var columnName = kvp.Key;
            var value = kvp.Value;

            if (columnMapping.TryGetValue(columnName, out var columnIndex))
            {
                if (columnIndex < cells.Count)
                {
                    SetCellText(cells[columnIndex], value);
                }
            }
        }
    }

    /// <summary>
    /// 获取单元格文本
    /// </summary>
    private static string GetCellText(TableCell cell)
    {
        return cell.InnerText;
    }

    /// <summary>
    /// 设置单元格文本
    /// </summary>
    private static void SetCellText(TableCell cell, string text)
    {
        // 清空原有内容
        cell.RemoveAllChildren<Paragraph>();

        // 创建新段落和文本
        var paragraph = new Paragraph();
        var run = new Run();
        var textElement = new Text(text);
        
        run.AppendChild(textElement);
        paragraph.AppendChild(run);
        cell.AppendChild(paragraph);
    }
}
```

---

## 步骤 2：实现数据验证工具

创建 `Tools/DataValidator.cs`：

```csharp
using FrameAgentWordFill.Models;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 数据验证工具（验证字段值是否符合类型要求）
/// </summary>
public sealed class DataValidator
{
    private static readonly Regex PhoneRegex = new(@"^1[3-9]\d{9}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

    /// <summary>
    /// 验证字段值
    /// </summary>
    /// <param name="field">字段定义</param>
    /// <param name="value">字段值</param>
    /// <returns>验证结果（成功/失败，错误消息）</returns>
    public (bool IsValid, string? ErrorMessage) ValidateField(Field field, string value)
    {
        // 必填验证
        if (field.Required && string.IsNullOrWhiteSpace(value))
        {
            return (false, field.MissingPrompt ?? $"{field.Name}不能为空");
        }

        // 类型验证
        return field.FieldType switch
        {
            "phone" => ValidatePhone(value, field.InvalidPrompt),
            "email" => ValidateEmail(value, field.InvalidPrompt),
            "date" => ValidateDate(value, field.InvalidPrompt),
            "number" => ValidateNumber(value, field.InvalidPrompt),
            _ => (true, null)
        };
    }

    private (bool, string?) ValidatePhone(string value, string? customMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (true, null);

        if (!PhoneRegex.IsMatch(value))
        {
            return (false, customMessage ?? "电话号码格式不正确");
        }

        return (true, null);
    }

    private (bool, string?) ValidateEmail(string value, string? customMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (true, null);

        if (!EmailRegex.IsMatch(value))
        {
            return (false, customMessage ?? "邮箱格式不正确");
        }

        return (true, null);
    }

    private (bool, string?) ValidateDate(string value, string? customMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (true, null);

        if (!DateTime.TryParse(value, out _))
        {
            return (false, customMessage ?? "日期格式不正确");
        }

        return (true, null);
    }

    private (bool, string?) ValidateNumber(string value, string? customMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (true, null);

        if (!decimal.TryParse(value, out _))
        {
            return (false, customMessage ?? "数字格式不正确");
        }

        return (true, null);
    }
}
```

---

## 步骤 3：实现文档生成服务

创建 `Services/GenerateService.cs`：

```csharp
using FrameAgentWordFill.Models;
using FrameAgentWordFill.Repositories;
using FrameAgentWordFill.Tools;
using System.Text.Json;

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
```

---

## 步骤 4：实现文档生成 API

创建 `Controllers/GenerateController.cs`：

```csharp
using Microsoft.AspNetCore.Mvc;
using FrameAgentWordFill.Services;

namespace FrameAgentWordFill.Controllers;

[ApiController]
[Route("api/[controller]")]
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
```

---

## 步骤 5：注册服务到 Program.cs

编辑 `Program.cs`，添加新服务注册：

```csharp
// 在 builder.Services 部分添加以下注册：

// W3 新增工具和服务
builder.Services.AddSingleton<DocGenerator>();
builder.Services.AddSingleton<DataValidator>();
builder.Services.AddSingleton<GenerateService>();
```

---

## 步骤 6：实现前端测试界面

创建 `frontend/src/views/user/TestGenerate.vue`：

```vue
<template>
  <div class="test-generate">
    <el-page-header title="返回" @back="goBack" content="文档生成测试" />

    <el-card style="margin-top: 20px">
      <template #header>
        <span>选择模板</span>
      </template>

      <el-select v-model="selectedTemplateId" placeholder="请选择模板" @change="loadTemplate">
        <el-option
          v-for="template in templates"
          :key="template.id"
          :label="template.name"
          :value="template.id"
        />
      </el-select>
    </el-card>

    <el-card v-if="currentTemplate" style="margin-top: 20px">
      <template #header>
        <span>填写字段</span>
      </template>

      <el-form :model="formData" label-width="150px">
        <el-form-item
          v-for="field in currentTemplate.fields"
          :key="field.id"
          :label="field.name"
          :required="field.required"
        >
          <el-input
            v-model="formData.fields[field.name]"
            :placeholder="field.guidePrompt || `请输入${field.name}`"
            :type="getInputType(field.fieldType)"
          />
          <div class="field-hint">类型: {{ field.fieldType }}</div>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card v-if="currentTemplate && currentTemplate.tables.length > 0" style="margin-top: 20px">
      <template #header>
        <span>填写表格数据</span>
      </template>

      <div v-for="table in currentTemplate.tables" :key="table.id" style="margin-bottom: 30px">
        <h4>{{ table.name }}</h4>
        
        <el-button type="primary" size="small" @click="addTableRow(table.name)">
          添加行
        </el-button>

        <el-table
          :data="formData.tables[table.name] || []"
          style="width: 100%; margin-top: 10px"
          border
        >
          <el-table-column
            v-for="column in table.columns"
            :key="column.id"
            :label="column.name"
          >
            <template #default="scope">
              <el-input
                v-model="scope.row[column.name]"
                :placeholder="`请输入${column.name}`"
                size="small"
              />
            </template>
          </el-table-column>
          <el-table-column label="操作" width="100">
            <template #default="scope">
              <el-button
                type="danger"
                size="small"
                @click="deleteTableRow(table.name, scope.$index)"
              >
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>
    </el-card>

    <div style="margin-top: 20px; text-align: center">
      <el-button type="primary" size="large" @click="generateDocument" :loading="generating">
        生成文档
      </el-button>
    </div>

    <!-- 生成结果对话框 -->
    <el-dialog v-model="showResultDialog" title="生成结果" width="500px">
      <div v-if="generateResult.success">
        <el-result icon="success" title="生成成功">
          <template #extra>
            <el-button type="primary" @click="downloadDocument">下载文档</el-button>
          </template>
        </el-result>
      </div>
      <div v-else>
        <el-result icon="error" :title="generateResult.error">
          <template #sub-title>
            <ul v-if="generateResult.validationErrors">
              <li v-for="(err, index) in generateResult.validationErrors" :key="index">
                {{ err }}
              </li>
            </ul>
          </template>
        </el-result>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axios from 'axios'

const router = useRouter()
const templates = ref<any[]>([])
const selectedTemplateId = ref('')
const currentTemplate = ref<any>(null)
const generating = ref(false)
const showResultDialog = ref(false)

const formData = reactive<{
  fields: Record<string, string>
  tables: Record<string, any[]>
}>({
  fields: {},
  tables: {}
})

const generateResult = reactive({
  success: false,
  error: '',
  validationErrors: [] as string[],
  downloadUrl: ''
})

const loadTemplates = async () => {
  try {
    const response = await axios.get('/api/template')
    templates.value = response.data.filter((t: any) => t.status === 'enabled')
  } catch (error) {
    ElMessage.error('加载模板列表失败')
  }
}

const loadTemplate = async () => {
  if (!selectedTemplateId.value) return

  try {
    const response = await axios.get(`/api/template/${selectedTemplateId.value}`)
    currentTemplate.value = response.data

    // 初始化表单数据
    formData.fields = {}
    formData.tables = {}

    currentTemplate.value.fields.forEach((field: any) => {
      formData.fields[field.name] = ''
    })

    currentTemplate.value.tables.forEach((table: any) => {
      formData.tables[table.name] = []
    })
  } catch (error) {
    ElMessage.error('加载模板详情失败')
  }
}

const addTableRow = (tableName: string) => {
  if (!formData.tables[tableName]) {
    formData.tables[tableName] = []
  }

  const table = currentTemplate.value.tables.find((t: any) => t.name === tableName)
  const newRow: Record<string, string> = {}
  table.columns.forEach((col: any) => {
    newRow[col.name] = ''
  })

  formData.tables[tableName].push(newRow)
}

const deleteTableRow = (tableName: string, rowIndex: number) => {
  formData.tables[tableName].splice(rowIndex, 1)
}

const generateDocument = async () => {
  if (!selectedTemplateId.value) {
    ElMessage.warning('请选择模板')
    return
  }

  generating.value = true

  try {
    // 构建请求数据
    const request = {
      templateId: selectedTemplateId.value,
      fields: Object.keys(formData.fields).map((key) => ({
        name: key,
        value: formData.fields[key]
      })),
      tables: Object.keys(formData.tables).map((key) => ({
        name: key,
        rows: formData.tables[key]
      }))
    }

    const response = await axios.post('/api/generate', request)

    if (response.data.success) {
      generateResult.success = true
      generateResult.downloadUrl = response.data.downloadUrl
      showResultDialog.value = true
    }
  } catch (error: any) {
    generateResult.success = false
    generateResult.error = error.response?.data?.error || '生成失败'
    generateResult.validationErrors = error.response?.data?.validationErrors || []
    showResultDialog.value = true
  } finally {
    generating.value = false
  }
}

const downloadDocument = async () => {
  try {
    const response = await axios.get(generateResult.downloadUrl, {
      responseType: 'blob'
    })

    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', generateResult.downloadUrl.split('/').pop() || 'document.docx')
    document.body.appendChild(link)
    link.click()
    link.remove()

    ElMessage.success('下载成功')
    showResultDialog.value = false
  } catch (error) {
    ElMessage.error('下载失败')
  }
}

const getInputType = (fieldType: string) => {
  switch (fieldType) {
    case 'email':
      return 'email'
    case 'number':
      return 'number'
    case 'date':
      return 'date'
    default:
      return 'text'
  }
}

const goBack = () => {
  router.push('/')
}

onMounted(() => {
  loadTemplates()
})
</script>

<style scoped>
.test-generate {
  padding: 20px;
}

.field-hint {
  font-size: 12px;
  color: #999;
  margin-top: 5px;
}

h4 {
  margin: 15px 0 10px 0;
}
</style>
```

### 添加路由

编辑 `frontend/src/router/index.ts`，添加测试路由：

```typescript
{
  path: '/test',
  children: [
    {
      path: 'generate',
      name: 'TestGenerate',
      component: () => import('../views/user/TestGenerate.vue')
    }
  ]
}
```

---

## 步骤 7：验收测试

### 7.1 准备测试数据

创建一个测试用的 Word 模板，包含以下内容：

```
项目申请表

项目名称：{项目名称}
项目负责人：{负责人}
负责人电话：{负责人电话}
负责人邮箱：{负责人邮箱}
项目预算：{预算}
开始日期：{开始日期}

团队成员：

| 姓名 | 职务 | 联系方式 |
|------|------|----------|
| {成员列表.姓名} | {成员列表.职务} | {成员列表.联系方式} |
```

### 7.2 上传模板

1. 访问 `http://localhost:5173/admin/templates`
2. 上传准备好的模板
3. 确认解析结果正确

### 7.3 测试文档生成

1. 访问 `http://localhost:5173/test/generate`
2. 选择刚上传的模板
3. 填写字段数据：
   ```
   项目名称：智能文档填充系统
   负责人：张三
   负责人电话：13800138000
   负责人邮箱：zhangsan@example.com
   预算：500000
   开始日期：2026-05-15
   ```
4. 添加表格数据（2行）：
   ```
   行1: 姓名=李四, 职务=技术负责人, 联系方式=13800138001
   行2: 姓名=王五, 职务=UI设计师, 联系方式=13800138002
   ```
5. 点击「生成文档」
6. 下载并打开生成的文档

**预期结果**：
- ✅ 所有普通字段被正确替换
- ✅ 表格包含 2 行数据（不包括表头）
- ✅ 表格中的数据填充正确
- ✅ 文档格式保持完整（字体、段落、表格样式）

### 7.4 测试数据验证

1. 故意输入错误的数据：
   - 负责人电话：`12345`（格式错误）
   - 负责人邮箱：`invalid-email`（格式错误）
2. 点击「生成文档」

**预期结果**：
- ❌ 生成失败
- 显示验证错误信息：
  - "负责人电话: 电话号码格式不正确"
  - "负责人邮箱: 邮箱格式不正确"

### 7.5 测试必填字段

1. 在模板配置中将「项目名称」设置为必填
2. 回到测试页面，不填写「项目名称」
3. 点击「生成文档」

**预期结果**：
- ❌ 生成失败
- 显示验证错误信息："项目名称: 项目名称不能为空"

---

## ✅ W3 验收清单

- [ ] DocGenerator 工具实现完成（普通字段替换）
- [ ] 表格填充功能实现完成（动态行数）
- [ ] 数据验证工具实现完成（类型验证、必填验证）
- [ ] 文档生成服务实现完成
- [ ] 文档生成 API 正常工作
- [ ] 文档下载 API 正常工作
- [ ] 前端测试界面实现完成
- [ ] 普通字段替换测试通过
- [ ] 表格填充测试通过（多行数据）
- [ ] 数据验证测试通过（电话、邮箱、日期、数字）
- [ ] 必填字段验证测试通过
- [ ] 生成的文档格式保持完整
- [ ] 生成的文档可以正常下载和打开

---

## 🔧 常见问题排查

### 问题 1：占位符未被替换

**现象**：生成的文档中仍然显示 `{字段名}`

**排查步骤**：
1. 检查字段名是否完全匹配（大小写敏感）
2. 检查占位符格式是否标准（半角花括号）
3. 查看日志中的替换记录

**解决方案**：
- 使用标准的占位符格式
- 确保传入的字段名与模板中的占位符一致
- 在 `DocGenerator` 中添加日志记录

### 问题 2：表格未填充或格式错乱

**现象**：表格数据未填充或表格格式变形

**原因**：
- 表格名称不匹配
- 列映射错误
- 表格结构不标准

**解决方案**：
- 确保表格第一行（表头）使用标准格式：`{表格名.字段名}`
- 确保所有列使用相同的表格名
- 检查日志中的表格识别记录

### 问题 3：中文字体显示异常

**现象**：生成的文档中中文显示为方块或乱码

**原因**：字体设置丢失

**解决方案**：
在 `SetCellText` 方法中保留原有样式：
```csharp
private static void SetCellText(TableCell cell, string text)
{
    var paragraph = cell.Elements<Paragraph>().FirstOrDefault();
    if (paragraph == null)
    {
        paragraph = new Paragraph();
        cell.AppendChild(paragraph);
    }
    else
    {
        paragraph.RemoveAllChildren<Run>();
    }

    var run = new Run(new Text(text));
    
    // 保留原有字体设置
    var runProperties = paragraph.Elements<Run>().FirstOrDefault()?.RunProperties;
    if (runProperties != null)
    {
        run.RunProperties = (RunProperties)runProperties.CloneNode(true);
    }
    
    paragraph.AppendChild(run);
}
```

### 问题 4：表格行数限制

**现象**：表格数据过多时生成失败

**解决方案**：
在 `FillTablesInDocument` 中添加行数限制检查：
```csharp
// 检查行数限制
var tableDef = template.Tables.FirstOrDefault(t => t.Name == tableName);
if (tableDef != null && dataRows.Count > tableDef.MaxRows)
{
    _logger.LogWarning("表格 {TableName} 数据行数 {RowCount} 超过最大限制 {MaxRows}", 
        tableName, dataRows.Count, tableDef.MaxRows);
    dataRows = dataRows.Take(tableDef.MaxRows).ToList();
}
```

### 问题 5：文件权限错误

**现象**：生成文档时报"文件被占用"或"权限不足"

**原因**：
- 输出文件被其他程序打开
- 目录权限不足

**解决方案**：
```csharp
// 在生成前检查文件是否存在并可写
if (File.Exists(outputPath))
{
    try
    {
        File.Delete(outputPath);
    }
    catch (IOException)
    {
        outputPath = Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            $"{Path.GetFileNameWithoutExtension(outputPath)}_{Guid.NewGuid()}.docx"
        );
    }
}
```

---

## 📝 下一步（W4）

W3 完成后，可以进入 W4：AI 智能对话填充。下一步需要：
1. 设计 WordFillAgent 的 Prompt 和工具集
2. 实现 AI 字段提取工具（基于 LLM）
3. 实现 AI 对话引导逻辑
4. 实现对话会话管理
5. 实现快捷指令识别
6. 实现对话消息流式接口（SSE）
7. 实现对话界面

---

## 🎯 W3 总结

本周完成的核心功能：
1. ✅ 普通字段替换引擎（支持批量替换）
2. ✅ 表格填充引擎（支持动态行数）
3. ✅ 数据验证工具（类型验证、必填验证）
4. ✅ 文档生成服务（完整业务逻辑）
5. ✅ 文档生成和下载 API
6. ✅ 前端测试界面

技术要点：
- 使用 OpenXML SDK 操作 Word 文档
- 支持段落文本和表格单元格的内容替换
- 保留原有样式和格式
- 数据验证确保生成质量
- 文件存储和下载管理

**🎉 至此，文档生成的核心闭环已完成！用户可以通过手动填写字段生成文档。**


