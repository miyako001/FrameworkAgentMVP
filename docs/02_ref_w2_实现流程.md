# W2 实现流程 - 模板管理与解析

**周期**：第 2 周  
**里程碑**：M2 - 模板管理可用 + M3 - 模板解析与字段抽取  
**目标**：实现管理员上传模板、自动解析占位符、字段配置和模板管理的完整功能

---

## 📋 实施步骤总览

```
步骤1: 安装 Word 文档处理库
    ↓
步骤2: 实现模板解析工具（占位符提取）
    ↓
步骤3: 实现模板数据访问层（Repository）
    ↓
步骤4: 实现模板管理服务（Service）
    ↓
步骤5: 实现模板管理 API（Controller）
    ↓
步骤6: 实现前端管理后台（Vue）
    ↓
步骤7: 验收测试
```

---

## 步骤 1：安装 Word 文档处理库

### 1.1 安装 OpenXML SDK

```powershell
cd c:\gitrepos\FrameworkAgentMVP\backend\FrameAgentWordFill

# 安装 OpenXML SDK（微软官方 Word 文档处理库）
dotnet add package DocumentFormat.OpenXml --version 3.1.0

# 安装辅助工具
dotnet add package System.Text.Json --version 8.0.0
```

### 1.2 验证安装

编辑 `FrameAgentWordFill.csproj`，确认包引用：

```xml
<ItemGroup>
  <PackageReference Include="DocumentFormat.OpenXml" Version="3.1.0" />
  <PackageReference Include="System.Text.Json" Version="8.0.0" />
  <!-- 其他已有包 -->
</ItemGroup>
```

---

## 步骤 2：实现模板解析工具

### 2.1 创建模板解析结果模型

创建 `Models/TemplateParseResult.cs`：

📁 **文件位置**：`backend/FrameAgentWordFill/Models/Parsing/TemplateParseResult.cs`
📁 **同时创建**：`backend/FrameAgentWordFill/Models/Parsing/FieldInfo.cs`、`backend/FrameAgentWordFill/Models/Parsing/TableInfo.cs`

```csharp
namespace FrameAgentWordFill.Models;

/// <summary>
/// 模板解析结果
/// </summary>
public sealed class TemplateParseResult
{
    /// <summary>
    /// 解析是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 普通字段列表
    /// </summary>
    public List<FieldInfo> Fields { get; set; } = new();

    /// <summary>
    /// 表格列表
    /// </summary>
    public List<TableInfo> Tables { get; set; } = new();

    /// <summary>
    /// 警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 字段信息
/// </summary>
public sealed class FieldInfo
{
    /// <summary>
    /// 字段名称（例如：项目名称）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型（text/phone/email/date/number）
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// 在模板中的位置（段落索引）
    /// </summary>
    public int Position { get; set; }
}

/// <summary>
/// 表格信息
/// </summary>
public sealed class TableInfo
{
    /// <summary>
    /// 表格名称（例如：成员列表）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 表格列（字段）
    /// </summary>
    public List<TableColumnInfo> Columns { get; set; } = new();

    /// <summary>
    /// 行类型（fixed/dynamic）
    /// </summary>
    public string RowType { get; set; } = "dynamic";

    /// <summary>
    /// 最大行数
    /// </summary>
    public int MaxRows { get; set; } = 10;
}

/// <summary>
/// 表格列信息
/// </summary>
public sealed class TableColumnInfo
{
    /// <summary>
    /// 列名（例如：姓名）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 列类型
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// 列顺序
    /// </summary>
    public int Order { get; set; }
}
```

### 2.2 创建模板解析工具

创建 `Tools/TemplateParser.cs`：

📁 **文件位置**：`backend/FrameAgentWordFill/Tools/TemplateParser.cs`

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FrameAgentWordFill.Models;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 模板解析工具（提取占位符和表格结构）
/// </summary>
public sealed class TemplateParser
{
    private readonly ILogger<TemplateParser> _logger;

    // 占位符正则表达式（支持标准格式和容错格式）
    // 标准: {字段名}、{表格名.字段名}
    // 容错: 【字段名】、［字段名］、{ 字段名 }
    private static readonly Regex PlaceholderRegex = new(
        @"[\{【［]\s*([a-zA-Z0-9_\u4e00-\u9fa5]+(?:\.[a-zA-Z0-9_\u4e00-\u9fa5]+)?)\s*[\}】］]",
        RegexOptions.Compiled
    );

    public TemplateParser(ILogger<TemplateParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析 Word 模板文件
    /// </summary>
    /// <param name="filePath">模板文件路径</param>
    /// <returns>解析结果</returns>
    public async Task<TemplateParseResult> ParseTemplateAsync(string filePath)
    {
        var result = new TemplateParseResult();

        try
        {
            await Task.Run(() =>
            {
                using var document = WordprocessingDocument.Open(filePath, false);
                if (document.MainDocumentPart == null)
                {
                    result.Errors.Add("无法打开文档：MainDocumentPart 为空");
                    result.Success = false;
                    return;
                }

                var body = document.MainDocumentPart.Document.Body;
                if (body == null)
                {
                    result.Errors.Add("文档内容为空");
                    result.Success = false;
                    return;
                }

                // 提取普通字段（从段落文本中）
                ExtractFieldsFromParagraphs(body, result);

                // 提取表格字段（从表格中）
                ExtractFieldsFromTables(body, result);

                result.Success = result.Errors.Count == 0;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析模板文件失败: {FilePath}", filePath);
            result.Errors.Add($"解析失败: {ex.Message}");
            result.Success = false;
        }

        return result;
    }

    /// <summary>
    /// 从段落中提取普通字段
    /// </summary>
    private void ExtractFieldsFromParagraphs(Body body, TemplateParseResult result)
    {
        var paragraphs = body.Elements<Paragraph>().ToList();
        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var text = GetParagraphText(paragraph);

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var matches = PlaceholderRegex.Matches(text);
            foreach (Match match in matches)
            {
                var placeholder = match.Groups[1].Value.Trim();

                // 跳过表格字段（包含点号）
                if (placeholder.Contains('.'))
                    continue;

                // 规范化占位符（去除空格、转换全角符号）
                var normalizedName = NormalizePlaceholder(placeholder, match.Value);
                
                // 检查是否需要规范化警告
                if (match.Value != $"{{{normalizedName}}}")
                {
                    result.Warnings.Add($"第 {i + 1} 段：占位符 '{match.Value}' 已自动规范化为 '{{{normalizedName}}}'");
                }

                // 去重
                if (fieldNames.Contains(normalizedName))
                    continue;

                fieldNames.Add(normalizedName);
                result.Fields.Add(new FieldInfo
                {
                    Name = normalizedName,
                    Type = InferFieldType(normalizedName),
                    Required = false,
                    Position = i
                });
            }
        }

        _logger.LogInformation("提取普通字段: {Count} 个", result.Fields.Count);
    }

    /// <summary>
    /// 从表格中提取表格字段
    /// </summary>
    private void ExtractFieldsFromTables(Body body, TemplateParseResult result)
    {
        var tables = body.Elements<Table>().ToList();

        foreach (var table in tables)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0)
                continue;

            // 第一行作为表头
            var headerRow = rows[0];
            var headerCells = headerRow.Elements<TableCell>().ToList();

            var tableInfo = new TableInfo();
            var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int colIndex = 0; colIndex < headerCells.Count; colIndex++)
            {
                var cellText = GetCellText(headerCells[colIndex]);
                var matches = PlaceholderRegex.Matches(cellText);

                foreach (Match match in matches)
                {
                    var placeholder = match.Groups[1].Value.Trim();

                    // 只处理表格字段（格式：表格名.字段名）
                    if (!placeholder.Contains('.'))
                        continue;

                    var parts = placeholder.Split('.');
                    if (parts.Length != 2)
                    {
                        result.Warnings.Add($"表格占位符格式错误: {match.Value}，应为 {{表格名.字段名}}");
                        continue;
                    }

                    var tableName = parts[0].Trim();
                    var columnName = parts[1].Trim();

                    // 设置表格名称（第一次遇到时）
                    if (string.IsNullOrEmpty(tableInfo.Name))
                    {
                        tableInfo.Name = tableName;
                    }
                    else if (tableInfo.Name != tableName)
                    {
                        result.Warnings.Add($"表格中发现不同的表格名: {tableName}，已忽略");
                        continue;
                    }

                    // 避免重复列
                    if (tableNames.Contains(columnName))
                        continue;

                    tableNames.Add(columnName);
                    tableInfo.Columns.Add(new TableColumnInfo
                    {
                        Name = columnName,
                        Type = InferFieldType(columnName),
                        Required = false,
                        Order = colIndex
                    });
                }
            }

            // 只添加有效的表格
            if (!string.IsNullOrEmpty(tableInfo.Name) && tableInfo.Columns.Count > 0)
            {
                result.Tables.Add(tableInfo);
                _logger.LogInformation("提取表格: {TableName}, 列数: {ColumnCount}", 
                    tableInfo.Name, tableInfo.Columns.Count);
            }
        }
    }

    /// <summary>
    /// 获取段落文本
    /// </summary>
    private static string GetParagraphText(Paragraph paragraph)
    {
        return paragraph.InnerText;
    }

    /// <summary>
    /// 获取单元格文本
    /// </summary>
    private static string GetCellText(TableCell cell)
    {
        return cell.InnerText;
    }

    /// <summary>
    /// 规范化占位符（去除空格、转换全角符号）
    /// </summary>
    private static string NormalizePlaceholder(string placeholder, string originalText)
    {
        // 去除前后空格
        var normalized = placeholder.Trim();

        // 替换全角符号（如果原文本包含全角括号）
        if (originalText.Contains('【') || originalText.Contains('】') || 
            originalText.Contains('［') || originalText.Contains('］'))
        {
            normalized = normalized.Replace("【", "").Replace("】", "")
                                   .Replace("［", "").Replace("］", "");
        }

        return normalized;
    }

    /// <summary>
    /// 推断字段类型（基于字段名称）
    /// </summary>
    private static string InferFieldType(string fieldName)
    {
        var name = fieldName.ToLower();

        if (name.Contains("电话") || name.Contains("手机") || name.Contains("phone"))
            return "phone";

        if (name.Contains("邮箱") || name.Contains("email") || name.Contains("mail"))
            return "email";

        if (name.Contains("日期") || name.Contains("时间") || name.Contains("date"))
            return "date";

        if (name.Contains("金额") || name.Contains("数量") || name.Contains("预算") || 
            name.Contains("number") || name.Contains("amount"))
            return "number";

        return "text";
    }
}
```

---

## 步骤 3：实现模板数据访问层

### 3.1 创建模板实体模型

创建 `Models/Template.cs`：

📁 **文件位置**：`backend/FrameAgentWordFill/Models/Templates/Template.cs`
📁 **同时创建**：`backend/FrameAgentWordFill/Models/Templates/Field.cs`、`backend/FrameAgentWordFill/Models/Templates/TableDefinition.cs`、`backend/FrameAgentWordFill/Models/Templates/TableColumn.cs`

```csharp
namespace FrameAgentWordFill.Models;

/// <summary>
/// 模板实体
/// </summary>
public sealed class Template
{
    /// <summary>
    /// 模板ID（GUID）
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 模板名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 存储的文件名（GUID_原始文件名）
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 原始文件名
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 状态（enabled/disabled）
    /// </summary>
    public string Status { get; set; } = "enabled";

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 字段列表（导航属性）
    /// </summary>
    public List<Field> Fields { get; set; } = new();

    /// <summary>
    /// 表格列表（导航属性）
    /// </summary>
    public List<TableDefinition> Tables { get; set; } = new();
}

/// <summary>
/// 字段实体
/// </summary>
public sealed class Field
{
    public int Id { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public bool Required { get; set; } = false;
    public int FieldOrder { get; set; } = 0;
    public string? GuidePrompt { get; set; }
    public string? MissingPrompt { get; set; }
    public string? InvalidPrompt { get; set; }
}

/// <summary>
/// 表格定义实体
/// </summary>
public sealed class TableDefinition
{
    public int Id { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RowType { get; set; } = "dynamic";
    public int MaxRows { get; set; } = 10;
    public string? GuidePrompt { get; set; }
    public List<TableColumn> Columns { get; set; } = new();
}

/// <summary>
/// 表格列实体
/// </summary>
public sealed class TableColumn
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ColumnOrder { get; set; } = 0;
}
```

### 3.2 创建模板仓储

创建 `Repositories/TemplateRepository.cs`：

```csharp
using Microsoft.Data.Sqlite;
using FrameAgentWordFill.Models;
using System.Text.Json;

namespace FrameAgentWordFill.Repositories;

/// <summary>
/// 模板数据访问层（⚠️ 注意：所有表名使用 fa_ 前缀）
/// </summary>
public sealed class TemplateRepository
{
    private readonly string _connectionString;
    private readonly ILogger<TemplateRepository> _logger;

    public TemplateRepository(IConfiguration configuration, ILogger<TemplateRepository> logger)
    {
        _logger = logger;
        var dbPath = GetDatabasePath(configuration);
        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// 创建模板（含字段和表格）
    /// </summary>
    public async Task<bool> CreateTemplateAsync(Template template)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            // 1. 插入模板主表（fa_templates）
            await using var cmdTemplate = connection.CreateCommand();
            cmdTemplate.Transaction = transaction;
            cmdTemplate.CommandText = @"
                INSERT INTO fa_templates (id, name, file_name, original_file_name, description, status, created_at, updated_at)
                VALUES (@id, @name, @fileName, @originalFileName, @description, @status, @createdAt, @updatedAt)
            ";
            cmdTemplate.Parameters.AddWithValue("@id", template.Id);
            cmdTemplate.Parameters.AddWithValue("@name", template.Name);
            cmdTemplate.Parameters.AddWithValue("@fileName", template.FileName);
            cmdTemplate.Parameters.AddWithValue("@originalFileName", template.OriginalFileName);
            cmdTemplate.Parameters.AddWithValue("@description", template.Description ?? (object)DBNull.Value);
            cmdTemplate.Parameters.AddWithValue("@status", template.Status);
            cmdTemplate.Parameters.AddWithValue("@createdAt", template.CreatedAt.ToString("o"));
            cmdTemplate.Parameters.AddWithValue("@updatedAt", template.UpdatedAt.ToString("o"));
            await cmdTemplate.ExecuteNonQueryAsync();

            // 2. 插入字段（fa_fields）
            foreach (var field in template.Fields)
            {
                await using var cmdField = connection.CreateCommand();
                cmdField.Transaction = transaction;
                cmdField.CommandText = @"
                    INSERT INTO fa_fields (template_id, name, field_type, required, field_order, guide_prompt, missing_prompt, invalid_prompt)
                    VALUES (@templateId, @name, @fieldType, @required, @fieldOrder, @guidePrompt, @missingPrompt, @invalidPrompt)
                ";
                cmdField.Parameters.AddWithValue("@templateId", template.Id);
                cmdField.Parameters.AddWithValue("@name", field.Name);
                cmdField.Parameters.AddWithValue("@fieldType", field.FieldType);
                cmdField.Parameters.AddWithValue("@required", field.Required ? 1 : 0);
                cmdField.Parameters.AddWithValue("@fieldOrder", field.FieldOrder);
                cmdField.Parameters.AddWithValue("@guidePrompt", field.GuidePrompt ?? (object)DBNull.Value);
                cmdField.Parameters.AddWithValue("@missingPrompt", field.MissingPrompt ?? (object)DBNull.Value);
                cmdField.Parameters.AddWithValue("@invalidPrompt", field.InvalidPrompt ?? (object)DBNull.Value);
                await cmdField.ExecuteNonQueryAsync();
            }

            // 3. 插入表格（fa_tables 和 fa_table_columns）
            foreach (var table in template.Tables)
            {
                await using var cmdTable = connection.CreateCommand();
                cmdTable.Transaction = transaction;
                cmdTable.CommandText = @"
                    INSERT INTO fa_tables (template_id, name, row_type, max_rows, guide_prompt)
                    VALUES (@templateId, @name, @rowType, @maxRows, @guidePrompt);
                    SELECT last_insert_rowid();
                ";
                cmdTable.Parameters.AddWithValue("@templateId", template.Id);
                cmdTable.Parameters.AddWithValue("@name", table.Name);
                cmdTable.Parameters.AddWithValue("@rowType", table.RowType);
                cmdTable.Parameters.AddWithValue("@maxRows", table.MaxRows);
                cmdTable.Parameters.AddWithValue("@guidePrompt", table.GuidePrompt ?? (object)DBNull.Value);
                
                var tableId = Convert.ToInt32(await cmdTable.ExecuteScalarAsync());

                // 插入表格列（fa_table_columns）
                foreach (var column in table.Columns)
                {
                    await using var cmdColumn = connection.CreateCommand();
                    cmdColumn.Transaction = transaction;
                    cmdColumn.CommandText = @"
                        INSERT INTO fa_table_columns (table_id, name, column_order)
                        VALUES (@tableId, @name, @columnOrder)
                    ";
                    cmdColumn.Parameters.AddWithValue("@tableId", tableId);
                    cmdColumn.Parameters.AddWithValue("@name", column.Name);
                    cmdColumn.Parameters.AddWithValue("@columnOrder", column.ColumnOrder);
                    await cmdColumn.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            _logger.LogInformation("模板创建成功: {TemplateId}", template.Id);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "创建模板失败: {TemplateId}", template.Id);
            return false;
        }
    }

    /// <summary>
    /// 获取所有模板列表（不含字段详情）
    /// </summary>
    public async Task<List<Template>> GetAllTemplatesAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, file_name, original_file_name, description, status, created_at, updated_at FROM fa_templates ORDER BY created_at DESC";

        var templates = new List<Template>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            templates.Add(new Template
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                FileName = reader.GetString(2),
                OriginalFileName = reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                Status = reader.GetString(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                UpdatedAt = DateTime.Parse(reader.GetString(7))
            });
        }

        return templates;
    }

    /// <summary>
    /// 获取模板详情（含字段和表格）
    /// </summary>
    public async Task<Template?> GetTemplateByIdAsync(string templateId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // 1. 获取模板基本信息
        await using var cmdTemplate = connection.CreateCommand();
        cmdTemplate.CommandText = "SELECT id, name, file_name, original_file_name, description, status, created_at, updated_at FROM fa_templates WHERE id = @id";
        cmdTemplate.Parameters.AddWithValue("@id", templateId);

        Template? template = null;
        await using (var reader = await cmdTemplate.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                template = new Template
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    FileName = reader.GetString(2),
                    OriginalFileName = reader.GetString(3),
                    Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Status = reader.GetString(5),
                    CreatedAt = DateTime.Parse(reader.GetString(6)),
                    UpdatedAt = DateTime.Parse(reader.GetString(7))
                };
            }
        }

        if (template == null)
            return null;

        // 2. 获取字段
        await using var cmdFields = connection.CreateCommand();
        cmdFields.CommandText = "SELECT id, name, field_type, required, field_order, guide_prompt, missing_prompt, invalid_prompt FROM fa_fields WHERE template_id = @templateId ORDER BY field_order";
        cmdFields.Parameters.AddWithValue("@templateId", templateId);

        await using (var reader = await cmdFields.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                template.Fields.Add(new Field
                {
                    Id = reader.GetInt32(0),
                    TemplateId = templateId,
                    Name = reader.GetString(1),
                    FieldType = reader.GetString(2),
                    Required = reader.GetInt32(3) == 1,
                    FieldOrder = reader.GetInt32(4),
                    GuidePrompt = reader.IsDBNull(5) ? null : reader.GetString(5),
                    MissingPrompt = reader.IsDBNull(6) ? null : reader.GetString(6),
                    InvalidPrompt = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
        }

        // 3. 获取表格和列
        await using var cmdTables = connection.CreateCommand();
        cmdTables.CommandText = "SELECT id, name, row_type, max_rows, guide_prompt FROM fa_tables WHERE template_id = @templateId";
        cmdTables.Parameters.AddWithValue("@templateId", templateId);

        await using (var reader = await cmdTables.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var table = new TableDefinition
                {
                    Id = reader.GetInt32(0),
                    TemplateId = templateId,
                    Name = reader.GetString(1),
                    RowType = reader.GetString(2),
                    MaxRows = reader.GetInt32(3),
                    GuidePrompt = reader.IsDBNull(4) ? null : reader.GetString(4)
                };

                // 获取表格列
                await using var cmdColumns = connection.CreateCommand();
                cmdColumns.CommandText = "SELECT id, name, column_order FROM fa_table_columns WHERE table_id = @tableId ORDER BY column_order";
                cmdColumns.Parameters.AddWithValue("@tableId", table.Id);

                await using (var colReader = await cmdColumns.ExecuteReaderAsync())
                {
                    while (await colReader.ReadAsync())
                    {
                        table.Columns.Add(new TableColumn
                        {
                            Id = colReader.GetInt32(0),
                            TableId = table.Id,
                            Name = colReader.GetString(1),
                            ColumnOrder = colReader.GetInt32(2)
                        });
                    }
                }

                template.Tables.Add(table);
            }
        }

        return template;
    }

    /// <summary>
    /// 更新字段配置
    /// </summary>
    public async Task<bool> UpdateFieldAsync(Field field)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE fa_fields 
            SET name = @name, field_type = @fieldType, required = @required, field_order = @fieldOrder,
                guide_prompt = @guidePrompt, missing_prompt = @missingPrompt, invalid_prompt = @invalidPrompt
            WHERE id = @id
        ";
        command.Parameters.AddWithValue("@id", field.Id);
        command.Parameters.AddWithValue("@name", field.Name);
        command.Parameters.AddWithValue("@fieldType", field.FieldType);
        command.Parameters.AddWithValue("@required", field.Required ? 1 : 0);
        command.Parameters.AddWithValue("@fieldOrder", field.FieldOrder);
        command.Parameters.AddWithValue("@guidePrompt", field.GuidePrompt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@missingPrompt", field.MissingPrompt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@invalidPrompt", field.InvalidPrompt ?? (object)DBNull.Value);

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    /// <summary>
    /// 删除模板（级联删除字段和表格）
    /// </summary>
    public async Task<bool> DeleteTemplateAsync(string templateId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM fa_templates WHERE id = @id";
        command.Parameters.AddWithValue("@id", templateId);

        var rows = await command.ExecuteNonQueryAsync();
        _logger.LogInformation("模板删除: {TemplateId}, 影响行数: {Rows}", templateId, rows);
        return rows > 0;
    }

    private static string GetDatabasePath(IConfiguration configuration)
    {
        var rootPath = configuration["Storage:RootPath"] ?? "..\\..\\storage";
        var fullPath = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rootPath));
        return Path.Combine(fullPath, "data", "frameagent.db");
    }
}
```

---

## 步骤 4：实现模板管理服务

创建 `Services/TemplateService.cs`：

```csharp
using FrameAgentWordFill.Models;
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
                return (false, null, parseResult);
            }

            _logger.LogInformation("模板上传成功: {TemplateId}, {TemplateName}", templateId, templateName);
            return (true, templateId, parseResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传模板失败: {FileName}", file.FileName);
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
    /// 获取模板详情
    /// </summary>
    public async Task<Template?> GetTemplateByIdAsync(string templateId)
    {
        return await _repository.GetTemplateByIdAsync(templateId);
    }

    /// <summary>
    /// 获取模板文件路径
    /// </summary>
    public string? GetTemplateFilePath(Template template)
    {
        var path = Path.Combine(_fileStorage.GetTemplatesPath(), template.FileName);
        return File.Exists(path) ? path : null;
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
        var template = await _repository.GetTemplateByIdAsync(templateId);
        if (template == null)
            return false;

        // 删除文件
        var filePath = Path.Combine(_fileStorage.GetTemplatesPath(), template.FileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        // 删除数据库记录
        return await _repository.DeleteTemplateAsync(templateId);
    }
}
```

---

## 步骤 5：实现模板管理 API

创建 `Controllers/TemplateController.cs`：

```csharp
using Microsoft.AspNetCore.Mvc;
using FrameAgentWordFill.Services;
using FrameAgentWordFill.Models;

namespace FrameAgentWordFill.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemplateController : ControllerBase
{
    private readonly TemplateService _templateService;
    private readonly ILogger<TemplateController> _logger;

    public TemplateController(TemplateService templateService, ILogger<TemplateController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// 上传模板
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadTemplate(
        [FromForm] IFormFile file,
        [FromForm] string name,
        [FromForm] string? description)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "文件不能为空" });
        }

        var (success, templateId, parseResult) = await _templateService.UploadTemplateAsync(file, name, description);

        if (!success)
        {
            return BadRequest(new
            {
                error = "模板上传失败",
                parseResult = parseResult
            });
        }

        return Ok(new
        {
            success = true,
            templateId = templateId,
            parseResult = new
            {
                fields = parseResult!.Fields,
                tables = parseResult.Tables,
                warnings = parseResult.Warnings,
                errors = parseResult.Errors
            }
        });
    }

    /// <summary>
    /// 获取所有模板列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllTemplates()
    {
        var templates = await _templateService.GetAllTemplatesAsync();
        return Ok(templates);
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
            return NotFound(new { error = "模板不存在" });
        }

        return Ok(template);
    }

    /// <summary>
    /// 下载模板文件
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadTemplate(string id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            return NotFound(new { error = "模板不存在" });
        }

        var filePath = _templateService.GetTemplateFilePath(template);
        if (filePath == null || !System.IO.File.Exists(filePath))
        {
            return NotFound(new { error = "模板文件不存在" });
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", template.OriginalFileName);
    }

    /// <summary>
    /// 更新字段配置
    /// </summary>
    [HttpPut("field/{fieldId}")]
    public async Task<IActionResult> UpdateField(int fieldId, [FromBody] Field field)
    {
        field.Id = fieldId;
        var success = await _templateService.UpdateFieldAsync(field);
        
        if (!success)
        {
            return NotFound(new { error = "字段不存在" });
        }

        return Ok(new { success = true });
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
            return NotFound(new { error = "模板不存在" });
        }

        return Ok(new { success = true });
    }
}
```

---

## 步骤 6：注册服务到 Program.cs

编辑 `Program.cs`，添加新服务注册：

```csharp
// 在 builder.Services 部分添加以下注册：

// 注册工具
builder.Services.AddSingleton<TemplateParser>();

// 注册仓储
builder.Services.AddSingleton<TemplateRepository>();

// 注册服务
builder.Services.AddSingleton<TemplateService>();
```

完整的服务注册部分应该是：

```csharp
// 注册自定义服务
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddSingleton<AIService>();

// W2 新增服务
builder.Services.AddSingleton<TemplateParser>();
builder.Services.AddSingleton<TemplateRepository>();
builder.Services.AddSingleton<TemplateService>();
```

---

## 步骤 7：实现前端管理后台

### 7.1 安装前端依赖

```powershell
cd c:\gitrepos\FrameworkAgentMVP\frontend
npm install element-plus @element-plus/icons-vue vue-router axios
```

### 7.2 配置路由

创建 `frontend/src/router/index.ts`：

📁 **文件位置**：`frontend/src/router/index.ts`

```typescript
import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      redirect: '/admin/templates'
    },
    {
      path: '/admin',
      children: [
        {
          path: 'templates',
          name: 'TemplateList',
          component: () => import('../views/admin/TemplateList.vue')
        },
        {
          path: 'templates/:id/config',
          name: 'TemplateConfig',
          component: () => import('../views/admin/TemplateConfig.vue')
        }
      ]
    }
  ]
})

export default router
```

### 7.3 创建模板列表页

创建 `frontend/src/views/admin/TemplateList.vue`：

📁 **文件位置**：`frontend/src/views/admin/TemplateList.vue`

```vue
<template>
  <div class="template-list">
    <el-page-header title="返回" @back="() => {}" content="模板管理" />

    <div class="toolbar">
      <el-button type="primary" @click="showUploadDialog = true" :icon="Upload">
        上传模板
      </el-button>
    </div>

    <el-table :data="templates" style="width: 100%" v-loading="loading">
      <el-table-column prop="name" label="模板名称" width="200" />
      <el-table-column prop="originalFileName" label="文件名" width="250" />
      <el-table-column label="字段数" width="100">
        <template #default="scope">
          <el-tag>{{ getFieldCount(scope.row) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="100">
        <template #default="scope">
          <el-tag :type="scope.row.status === 'enabled' ? 'success' : 'danger'">
            {{ scope.row.status === 'enabled' ? '启用' : '禁用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="scope">
          {{ formatDate(scope.row.createdAt) }}
        </template>
      </el-table-column>
      <el-table-column label="操作" fixed="right" width="300">
        <template #default="scope">
          <el-button size="small" @click="viewTemplate(scope.row)">
            查看
          </el-button>
          <el-button size="small" type="primary" @click="configTemplate(scope.row)">
            配置
          </el-button>
          <el-button size="small" @click="downloadTemplate(scope.row)">
            下载
          </el-button>
          <el-button size="small" type="danger" @click="deleteTemplate(scope.row)">
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 上传对话框 -->
    <el-dialog v-model="showUploadDialog" title="上传模板" width="600px">
      <el-form :model="uploadForm" label-width="100px">
        <el-form-item label="模板名称" required>
          <el-input v-model="uploadForm.name" placeholder="请输入模板名称" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input
            v-model="uploadForm.description"
            type="textarea"
            :rows="3"
            placeholder="请输入模板描述"
          />
        </el-form-item>
        <el-form-item label="模板文件" required>
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :limit="1"
            accept=".docx"
            :on-change="handleFileChange"
          >
            <el-button type="primary">选择文件</el-button>
            <template #tip>
              <div class="el-upload__tip">
                仅支持 .docx 格式，占位符格式：{字段名}、{表格名.字段名}
              </div>
            </template>
          </el-upload>
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showUploadDialog = false">取消</el-button>
        <el-button type="primary" @click="handleUpload" :loading="uploading">
          上传
        </el-button>
      </template>
    </el-dialog>

    <!-- 解析结果对话框 -->
    <el-dialog v-model="showParseResultDialog" title="模板解析结果" width="700px">
      <div v-if="parseResult">
        <el-alert
          v-if="parseResult.warnings && parseResult.warnings.length > 0"
          title="警告"
          type="warning"
          :closable="false"
        >
          <ul>
            <li v-for="(warning, index) in parseResult.warnings" :key="index">
              {{ warning }}
            </li>
          </ul>
        </el-alert>

        <h3>普通字段 ({{ parseResult.fields?.length || 0 }} 个)</h3>
        <el-table :data="parseResult.fields" size="small">
          <el-table-column prop="name" label="字段名" />
          <el-table-column prop="type" label="类型" width="100" />
        </el-table>

        <h3 style="margin-top: 20px">
          表格 ({{ parseResult.tables?.length || 0 }} 个)
        </h3>
        <div v-for="table in parseResult.tables" :key="table.name" style="margin-bottom: 20px">
          <h4>{{ table.name }}</h4>
          <el-table :data="table.columns" size="small">
            <el-table-column prop="name" label="列名" />
            <el-table-column prop="type" label="类型" width="100" />
          </el-table>
        </div>
      </div>

      <template #footer>
        <el-button type="primary" @click="closeParseResultDialog">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Upload } from '@element-plus/icons-vue'
import axios from 'axios'

const router = useRouter()
const loading = ref(false)
const templates = ref<any[]>([])
const showUploadDialog = ref(false)
const showParseResultDialog = ref(false)
const uploading = ref(false)
const parseResult = ref<any>(null)
const uploadedTemplateId = ref<string>('')

const uploadForm = ref({
  name: '',
  description: '',
  file: null as File | null
})

const handleFileChange = (file: any) => {
  uploadForm.value.file = file.raw
}

const handleUpload = async () => {
  if (!uploadForm.value.name || !uploadForm.value.file) {
    ElMessage.warning('请填写完整信息')
    return
  }

  uploading.value = true
  const formData = new FormData()
  formData.append('file', uploadForm.value.file)
  formData.append('name', uploadForm.value.name)
  if (uploadForm.value.description) {
    formData.append('description', uploadForm.value.description)
  }

  try {
    const response = await axios.post('/api/template/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })

    if (response.data.success) {
      ElMessage.success('模板上传成功')
      showUploadDialog.value = false
      
      // 显示解析结果
      parseResult.value = response.data.parseResult
      uploadedTemplateId.value = response.data.templateId
      showParseResultDialog.value = true

      // 重置表单
      uploadForm.value = { name: '', description: '', file: null }
      
      // 刷新列表
      await loadTemplates()
    }
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '上传失败')
  } finally {
    uploading.value = false
  }
}

const closeParseResultDialog = () => {
  showParseResultDialog.value = false
  // 自动跳转到配置页面
  if (uploadedTemplateId.value) {
    router.push(`/admin/templates/${uploadedTemplateId.value}/config`)
  }
}

const loadTemplates = async () => {
  loading.value = true
  try {
    const response = await axios.get('/api/template')
    templates.value = response.data
  } catch (error) {
    ElMessage.error('加载模板列表失败')
  } finally {
    loading.value = false
  }
}

const viewTemplate = async (template: any) => {
  try {
    const response = await axios.get(`/api/template/${template.id}`)
    ElMessageBox.alert(
      JSON.stringify(response.data, null, 2),
      '模板详情',
      {
        confirmButtonText: '确定'
      }
    )
  } catch (error) {
    ElMessage.error('获取模板详情失败')
  }
}

const configTemplate = (template: any) => {
  router.push(`/admin/templates/${template.id}/config`)
}

const downloadTemplate = async (template: any) => {
  try {
    const response = await axios.get(`/api/template/${template.id}/download`, {
      responseType: 'blob'
    })
    
    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', template.originalFileName)
    document.body.appendChild(link)
    link.click()
    link.remove()
    
    ElMessage.success('下载成功')
  } catch (error) {
    ElMessage.error('下载失败')
  }
}

const deleteTemplate = async (template: any) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除模板"${template.name}"吗？`,
      '确认删除',
      {
        type: 'warning'
      }
    )

    await axios.delete(`/api/template/${template.id}`)
    ElMessage.success('删除成功')
    await loadTemplates()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error('删除失败')
    }
  }
}

const getFieldCount = (template: any) => {
  // 注意：列表中不包含字段信息，需要查看详情
  return '查看详情'
}

const formatDate = (dateStr: string) => {
  const date = new Date(dateStr)
  return date.toLocaleString('zh-CN')
}

onMounted(() => {
  loadTemplates()
})
</script>

<style scoped>
.template-list {
  padding: 20px;
}

.toolbar {
  margin: 20px 0;
}

h3, h4 {
  margin: 10px 0;
}
</style>
```

### 7.4 创建字段配置页（简化版）

创建 `frontend/src/views/admin/TemplateConfig.vue`：

📁 **文件位置**：`frontend/src/views/admin/TemplateConfig.vue`

```vue
<template>
  <div class="template-config">
    <el-page-header title="返回" @back="goBack" :content="`配置模板: ${template?.name}`" />

    <el-card v-loading="loading" style="margin-top: 20px">
      <template #header>
        <span>字段配置</span>
      </template>

      <el-table :data="template?.fields" style="width: 100%">
        <el-table-column prop="name" label="字段名" width="150" />
        <el-table-column prop="fieldType" label="类型" width="120">
          <template #default="scope">
            <el-select v-model="scope.row.fieldType" size="small">
              <el-option label="文本" value="text" />
              <el-option label="电话" value="phone" />
              <el-option label="邮箱" value="email" />
              <el-option label="日期" value="date" />
              <el-option label="数字" value="number" />
            </el-select>
          </template>
        </el-table-column>
        <el-table-column prop="required" label="必填" width="80">
          <template #default="scope">
            <el-switch v-model="scope.row.required" />
          </template>
        </el-table-column>
        <el-table-column prop="guidePrompt" label="引导话术">
          <template #default="scope">
            <el-input v-model="scope.row.guidePrompt" size="small" />
          </template>
        </el-table-column>
        <el-table-column label="操作" width="120">
          <template #default="scope">
            <el-button size="small" type="primary" @click="saveField(scope.row)">
              保存
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-divider />

      <h3>表格配置</h3>
      <div v-for="table in template?.tables" :key="table.id" style="margin-bottom: 20px">
        <h4>{{ table.name }}</h4>
        <el-descriptions :column="2" border>
          <el-descriptions-item label="行类型">{{ table.rowType }}</el-descriptions-item>
          <el-descriptions-item label="最大行数">{{ table.maxRows }}</el-descriptions-item>
          <el-descriptions-item label="引导话术">{{ table.guidePrompt }}</el-descriptions-item>
        </el-descriptions>
        
        <el-table :data="table.columns" size="small" style="margin-top: 10px">
          <el-table-column prop="name" label="列名" />
          <el-table-column prop="columnOrder" label="顺序" width="80" />
        </el-table>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import axios from 'axios'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const template = ref<any>(null)

const templateId = route.params.id as string

const loadTemplate = async () => {
  loading.value = true
  try {
    const response = await axios.get(`/api/template/${templateId}`)
    template.value = response.data
  } catch (error) {
    ElMessage.error('加载模板详情失败')
  } finally {
    loading.value = false
  }
}

const saveField = async (field: any) => {
  try {
    await axios.put(`/api/template/field/${field.id}`, field)
    ElMessage.success('保存成功')
  } catch (error) {
    ElMessage.error('保存失败')
  }
}

const goBack = () => {
  router.push('/admin/templates')
}

onMounted(() => {
  loadTemplate()
})
</script>

<style scoped>
.template-config {
  padding: 20px;
}

h3, h4 {
  margin: 15px 0 10px 0;
}
</style>
```

### 7.5 更新主应用入口

编辑 `frontend/src/main.ts`：

```typescript
import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import App from './App.vue'
import router from './router'

const app = createApp(App)

// 注册所有图标
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.use(ElementPlus)
app.use(router)
app.mount('#app')
```

编辑 `frontend/src/App.vue`：

```vue
<template>
  <div id="app">
    <router-view />
  </div>
</template>

<style>
#app {
  font-family: 'Microsoft YaHei', Avenir, Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}
</style>
```

---

## 步骤 8：验收测试

### 8.1 启动后端

```powershell
cd c:\gitrepos\FrameworkAgentMVP\backend\FrameAgentWordFill
dotnet run
```

### 8.2 启动前端

```powershell
cd c:\gitrepos\FrameworkAgentMVP\frontend
npm run dev
```

### 8.3 测试模板上传

1. 访问 `http://localhost:5173/admin/templates`
2. 点击「上传模板」按钮
3. 填写模板名称：「项目申请表」
4. 上传一个包含以下内容的 Word 文档：
   ```
   项目名称：{项目名称}
   负责人：{负责人}
   预算：{预算}

   成员列表：
   | 姓名 | 职务 |
   |------|------|
   | {成员列表.姓名} | {成员列表.职务} |
   ```
5. 点击「上传」

**预期结果**：
- 上传成功，显示解析结果对话框
- 显示 3 个普通字段：项目名称、负责人、预算
- 显示 1 个表格：成员列表（2 列）
- 自动跳转到字段配置页

### 8.4 测试字段配置

1. 在字段配置页修改「项目名称」的引导话术为：「请输入项目的完整名称」
2. 勾选「必填」选项
3. 点击「保存」按钮

**预期结果**：
- 显示「保存成功」提示
- 刷新页面后，配置仍然保留

### 8.5 测试模板下载

1. 返回模板列表页
2. 点击「下载」按钮

**预期结果**：
- 浏览器自动下载原始 Word 文档
- 文件名与上传时一致

### 8.6 测试模板删除

1. 在模板列表中点击「删除」按钮
2. 确认删除

**预期结果**：
- 显示「删除成功」提示
- 模板从列表中消失
- 文件从 `storage/templates` 目录删除
- 数据库中的相关记录被删除

---

## ✅ W2 验收清单

- [ ] OpenXML SDK 安装成功
- [ ] 模板解析工具实现完成（提取普通字段和表格字段）
- [ ] 占位符规范化功能正常（支持全角括号、多余空格）
- [ ] 模板数据访问层（Repository）实现完成
- [ ] 模板管理服务（Service）实现完成
- [ ] 模板上传 API 正常工作
- [ ] 模板列表 API 正常工作
- [ ] 模板详情 API 正常工作
- [ ] 模板下载 API 正常工作
- [ ] 字段更新 API 正常工作
- [ ] 模板删除 API 正常工作
- [ ] 前端模板列表页显示正常
- [ ] 前端模板上传功能正常
- [ ] 前端字段配置页功能正常
- [ ] 解析结果对话框显示正常
- [ ] 数据库中的数据正确存储（使用 fa_ 前缀）

---

## 🔧 常见问题排查

### 问题 1：Word 文档解析失败

**现象**：上传模板后报错「无法打开文档」

**原因**：文件格式问题或文档损坏

**解决方案**：
1. 确保文件是 .docx 格式（不是 .doc）
2. 尝试用 Word 打开文件并另存为新文件
3. 检查文件是否被加密或受保护

### 问题 2：占位符识别不准确

**现象**：模板中的占位符没有被识别

**排查步骤**：
1. 检查占位符格式是否正确：`{字段名}` 或 `{表格名.字段名}`
2. 检查是否有多余空格或全角符号
3. 查看解析结果中的警告信息

**解决方案**：
- 使用标准的半角花括号 `{}`
- 避免在占位符中使用特殊字符
- 使用规范化功能自动修复

### 问题 3：表格字段识别失败

**现象**：表格中的占位符没有被识别为表格字段

**原因**：占位符格式不正确

**解决方案**：
- 确保表格字段使用 `{表格名.字段名}` 格式
- 确保所有表格字段使用同一个表格名
- 检查表格是否在 Word 中正确创建（不是用空格或Tab模拟的表格）

### 问题 4：前端上传失败（CORS 错误）

**现象**：浏览器控制台显示 CORS 错误

**解决方案**：
确保后端 `Program.cs` 中的 CORS 配置正确：
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 并且在中间件中启用
app.UseCors();
```

### 问题 5：文件上传大小限制

**现象**：上传大文件时报错

**解决方案**：
在 `Program.cs` 中配置文件上传限制：
```csharp
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
});
```

### 问题 6：数据库表名错误

**现象**：执行 SQL 时报错「no such table: templates」

**原因**：忘记使用 `fa_` 前缀

**解决方案**：
- 检查所有 SQL 语句，确保表名使用 `fa_` 前缀
- 例如：`fa_templates`、`fa_fields`、`fa_tables` 等

---

## 📝 下一步（W3）

W2 完成后，可以进入 W3：文档生成核心闭环。下一步需要：
1. 安装和配置 OpenXML SDK（已完成）
2. 实现普通字段替换逻辑
3. 实现表格字段填充逻辑
4. 实现文档生成接口
5. 实现文档下载功能
6. 前端生成结果页

---

## 🎯 W2 总结

本周完成的核心功能：
1. ✅ 模板上传与文件存储
2. ✅ Word 文档解析（OpenXML SDK）
3. ✅ 占位符提取与规范化
4. ✅ 字段和表格识别
5. ✅ 数据库存储（SQLite with fa_ prefix）
6. ✅ 完整的 CRUD API
7. ✅ 管理后台界面（Vue3 + Element Plus）
8. ✅ 字段配置与管理

技术要点：
- 使用 OpenXML SDK 解析 Word 文档
- 正则表达式提取占位符
- 支持容错处理（全角符号、多余空格）
- SQLite 事务保证数据一致性
- 前后端分离架构
- RESTful API 设计


