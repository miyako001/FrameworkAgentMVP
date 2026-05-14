# W5 实现流程 - 导入填充链路

**周期**：第 5 周  
**里程碑**：M6 - 导入填充功能完整可用  
**目标**：实现用户上传 Excel/JSON/Word 文件自动填充模板的完整链路

**⚠️ 核心功能说明**：
- 本周是项目的**第二大核心功能**，与 W4 的对话填充并列
- 用户可以上传**外部数据文件**（Excel/JSON/Word），系统自动提取数据
- 实现**智能字段匹配**算法（精确匹配 + 模糊匹配 + 语义匹配）
- 提供**可视化匹配界面**，用户可调整匹配关系
- 支持**缺失字段补全**，一键生成文档

---

## 📋 实施步骤总览

```
步骤1: 设计导入数据模型和数据库表
    ↓
步骤2: 实现文件解析工具（Excel/JSON/Word）
    ↓
步骤3: 实现字段智能匹配算法
    ↓
步骤4: 实现导入会话管理
    ↓
步骤5: 实现导入服务和API
    ↓
步骤6: 实现前端导入界面
    ↓
步骤7: 实现字段匹配可视化
    ↓
步骤8: 验收测试
```

---

## 步骤 1：设计导入数据模型和数据库表

### 1.1 创建导入相关数据模型

创建 `Models/Import/ImportSession.cs`：

```csharp
namespace FrameAgentWordFill.Models.Import;

/// <summary>
/// 导入会话（记录每次导入操作的状态）
/// </summary>
public sealed class ImportSession
{
    /// <summary>
    /// 会话ID（自增主键）
    /// </summary>
    public int SessionId { get; set; }

    /// <summary>
    /// 模板ID
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// 导入文件类型（Excel/JSON/Word）
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// 导入文件路径（相对路径，存储在 /storage/uploads/）
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 导入状态（Parsing/MatchingFields/WaitingConfirm/Completed/Failed）
    /// </summary>
    public string Status { get; set; } = "Parsing";

    /// <summary>
    /// 已匹配字段数量
    /// </summary>
    public int MatchedFieldCount { get; set; } = 0;

    /// <summary>
    /// 未匹配字段数量
    /// </summary>
    public int UnmatchedFieldCount { get; set; } = 0;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 错误信息（如果导入失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

创建 `Models/Import/ImportFieldMapping.cs`：

```csharp
namespace FrameAgentWordFill.Models.Import;

/// <summary>
/// 导入字段映射（记录源字段与模板字段的匹配关系）
/// </summary>
public sealed class ImportFieldMapping
{
    /// <summary>
    /// 映射ID（自增主键）
    /// </summary>
    public int MappingId { get; set; }

    /// <summary>
    /// 导入会话ID
    /// </summary>
    public int SessionId { get; set; }

    /// <summary>
    /// 源字段名（从文件中提取的字段名）
    /// </summary>
    public string SourceFieldName { get; set; } = string.Empty;

    /// <summary>
    /// 模板字段名（模板中定义的字段名）
    /// </summary>
    public string? TemplateFieldName { get; set; }

    /// <summary>
    /// 字段值（从文件中提取的值）
    /// </summary>
    public string? FieldValue { get; set; }

    /// <summary>
    /// 匹配置信度（0-100，100表示完全匹配）
    /// </summary>
    public int MatchConfidence { get; set; } = 0;

    /// <summary>
    /// 匹配方式（Exact/Fuzzy/Semantic/Manual）
    /// </summary>
    public string MatchMethod { get; set; } = "Manual";

    /// <summary>
    /// 是否用户确认（用户手动调整后为true）
    /// </summary>
    public bool IsUserConfirmed { get; set; } = false;

    /// <summary>
    /// 字段类型（Normal/Table）
    /// </summary>
    public string FieldType { get; set; } = "Normal";

    /// <summary>
    /// 表格名称（如果是表格字段）
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// 表格列名（如果是表格字段）
    /// </summary>
    public string? ColumnName { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

创建 `Models/Import/ImportTableData.cs`：

```csharp
namespace FrameAgentWordFill.Models.Import;

/// <summary>
/// 导入表格数据（存储从文件中提取的表格数据）
/// </summary>
public sealed class ImportTableData
{
    /// <summary>
    /// 数据ID（自增主键）
    /// </summary>
    public int DataId { get; set; }

    /// <summary>
    /// 导入会话ID
    /// </summary>
    public int SessionId { get; set; }

    /// <summary>
    /// 表格名称
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// 行索引（从0开始）
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// 单元格值
    /// </summary>
    public string? CellValue { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

创建 `Models/Import/ParsedFileData.cs`（用于传递解析结果）：

```csharp
namespace FrameAgentWordFill.Models.Import;

/// <summary>
/// 文件解析结果（内存对象，不存数据库）
/// </summary>
public sealed class ParsedFileData
{
    /// <summary>
    /// 文件类型
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// 普通字段数据（字段名 -> 值）
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new();

    /// <summary>
    /// 表格数据（表格名 -> 行数据列表）
    /// </summary>
    public Dictionary<string, List<Dictionary<string, string>>> Tables { get; set; } = new();

    /// <summary>
    /// 解析错误信息（如果有）
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 解析警告信息（如果有）
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
```

### 1.2 更新数据库表结构

编辑 `Data/DatabaseSchema.sql`，添加导入相关表：

```sql
-- =============================================
-- 导入会话表（W5新增）
-- =============================================
CREATE TABLE IF NOT EXISTS fa_import_sessions (
    session_id INTEGER PRIMARY KEY AUTOINCREMENT,
    template_id INTEGER NOT NULL,
    file_type TEXT NOT NULL,  -- Excel/JSON/Word
    file_path TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'Parsing',  -- Parsing/MatchingFields/WaitingConfirm/Completed/Failed
    matched_field_count INTEGER NOT NULL DEFAULT 0,
    unmatched_field_count INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    error_message TEXT,
    FOREIGN KEY (template_id) REFERENCES fa_templates(template_id) ON DELETE CASCADE
);

-- =============================================
-- 导入字段映射表（W5新增）
-- =============================================
CREATE TABLE IF NOT EXISTS fa_import_field_mappings (
    mapping_id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL,
    source_field_name TEXT NOT NULL,
    template_field_name TEXT,
    field_value TEXT,
    match_confidence INTEGER NOT NULL DEFAULT 0,  -- 0-100
    match_method TEXT NOT NULL DEFAULT 'Manual',  -- Exact/Fuzzy/Semantic/Manual
    is_user_confirmed INTEGER NOT NULL DEFAULT 0,  -- 0=false, 1=true
    field_type TEXT NOT NULL DEFAULT 'Normal',  -- Normal/Table
    table_name TEXT,
    column_name TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (session_id) REFERENCES fa_import_sessions(session_id) ON DELETE CASCADE
);

-- =============================================
-- 导入表格数据表（W5新增）
-- =============================================
CREATE TABLE IF NOT EXISTS fa_import_table_data (
    data_id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL,
    table_name TEXT NOT NULL,
    row_index INTEGER NOT NULL,
    column_name TEXT NOT NULL,
    cell_value TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (session_id) REFERENCES fa_import_sessions(session_id) ON DELETE CASCADE
);

-- =============================================
-- 创建索引（优化查询性能）
-- =============================================
CREATE INDEX IF NOT EXISTS idx_import_sessions_template_id ON fa_import_sessions(template_id);
CREATE INDEX IF NOT EXISTS idx_import_sessions_status ON fa_import_sessions(status);
CREATE INDEX IF NOT EXISTS idx_import_field_mappings_session_id ON fa_import_field_mappings(session_id);
CREATE INDEX IF NOT EXISTS idx_import_table_data_session_id ON fa_import_table_data(session_id);
```

### 1.3 更新数据库初始化器

编辑 `Data/DatabaseInitializer.cs`，添加导入表的初始化：

```csharp
// 在 InitializeDatabaseAsync 方法中添加导入表的创建
await command.ExecuteNonQueryAsync();

// W5: 创建导入会话表
command.CommandText = @"
CREATE TABLE IF NOT EXISTS fa_import_sessions (
    session_id INTEGER PRIMARY KEY AUTOINCREMENT,
    template_id INTEGER NOT NULL,
    file_type TEXT NOT NULL,
    file_path TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'Parsing',
    matched_field_count INTEGER NOT NULL DEFAULT 0,
    unmatched_field_count INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    error_message TEXT,
    FOREIGN KEY (template_id) REFERENCES fa_templates(template_id) ON DELETE CASCADE
);";
await command.ExecuteNonQueryAsync();

// W5: 创建导入字段映射表
command.CommandText = @"
CREATE TABLE IF NOT EXISTS fa_import_field_mappings (
    mapping_id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL,
    source_field_name TEXT NOT NULL,
    template_field_name TEXT,
    field_value TEXT,
    match_confidence INTEGER NOT NULL DEFAULT 0,
    match_method TEXT NOT NULL DEFAULT 'Manual',
    is_user_confirmed INTEGER NOT NULL DEFAULT 0,
    field_type TEXT NOT NULL DEFAULT 'Normal',
    table_name TEXT,
    column_name TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (session_id) REFERENCES fa_import_sessions(session_id) ON DELETE CASCADE
);";
await command.ExecuteNonQueryAsync();

// W5: 创建导入表格数据表
command.CommandText = @"
CREATE TABLE IF NOT EXISTS fa_import_table_data (
    data_id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL,
    table_name TEXT NOT NULL,
    row_index INTEGER NOT NULL,
    column_name TEXT NOT NULL,
    cell_value TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (session_id) REFERENCES fa_import_sessions(session_id) ON DELETE CASCADE
);";
await command.ExecuteNonQueryAsync();

// W5: 创建索引
command.CommandText = "CREATE INDEX IF NOT EXISTS idx_import_sessions_template_id ON fa_import_sessions(template_id);";
await command.ExecuteNonQueryAsync();
command.CommandText = "CREATE INDEX IF NOT EXISTS idx_import_sessions_status ON fa_import_sessions(status);";
await command.ExecuteNonQueryAsync();
command.CommandText = "CREATE INDEX IF NOT EXISTS idx_import_field_mappings_session_id ON fa_import_field_mappings(session_id);";
await command.ExecuteNonQueryAsync();
command.CommandText = "CREATE INDEX IF NOT EXISTS idx_import_table_data_session_id ON fa_import_table_data(session_id);";
await command.ExecuteNonQueryAsync();

_logger.LogInformation("数据库表初始化完成（包含W5导入表）");
```

---

## 步骤 2：实现文件解析工具

### 2.1 安装依赖包

在 `FrameAgentWordFill.csproj` 中添加依赖：

```bash
cd backend/FrameAgentWordFill
dotnet add package EPPlus --version 7.0.0
# EPPlus 用于 Excel 解析，支持 .xlsx 格式
```

### 2.2 创建 Excel 解析器

创建 `Tools/FileParser/ExcelParser.cs`：

```csharp
using OfficeOpenXml;
using FrameAgentWordFill.Models.Import;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// Excel 文件解析器（支持 .xlsx 格式）
/// </summary>
public sealed class ExcelParser
{
    private readonly ILogger<ExcelParser> _logger;

    public ExcelParser(ILogger<ExcelParser> logger)
    {
        _logger = logger;
        // 设置 EPPlus 许可证（非商业用途）
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// 解析 Excel 文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>解析结果</returns>
    public async Task<ParsedFileData> ParseAsync(string filePath)
    {
        var result = new ParsedFileData { FileType = "Excel" };

        try
        {
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0]; // 读取第一个工作表

            if (worksheet.Dimension == null)
            {
                result.Errors.Add("Excel 文件为空");
                return result;
            }

            // 判断数据结构：如果第一行是表头，认为是表格数据；否则认为是键值对数据
            var firstRowCellCount = worksheet.Dimension.End.Column;
            var headerRow = new List<string>();

            for (int col = 1; col <= firstRowCellCount; col++)
            {
                var headerValue = worksheet.Cells[1, col].Text.Trim();
                headerRow.Add(headerValue);
            }

            // 策略1: 如果第一列是字段名，第二列是值，解析为普通字段
            if (firstRowCellCount == 2 && worksheet.Dimension.End.Row > 1)
            {
                await ParseAsFieldValuePairsAsync(worksheet, result);
            }
            // 策略2: 如果有多列，第一行是表头，解析为表格数据
            else if (firstRowCellCount > 2 && headerRow.Any(h => !string.IsNullOrWhiteSpace(h)))
            {
                await ParseAsTableAsync(worksheet, headerRow, result);
            }
            else
            {
                result.Warnings.Add("无法识别 Excel 数据结构，请确保格式符合要求");
            }

            _logger.LogInformation($"Excel 解析完成：{result.Fields.Count} 个字段，{result.Tables.Count} 个表格");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel 解析失败");
            result.Errors.Add($"解析失败：{ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 解析为字段-值对（第一列=字段名，第二列=值）
    /// </summary>
    private Task ParseAsFieldValuePairsAsync(ExcelWorksheet worksheet, ParsedFileData result)
    {
        for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
        {
            var fieldName = worksheet.Cells[row, 1].Text.Trim();
            var fieldValue = worksheet.Cells[row, 2].Text.Trim();

            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                result.Fields[fieldName] = fieldValue;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 解析为表格数据（第一行=表头，后续行=数据行）
    /// </summary>
    private Task ParseAsTableAsync(ExcelWorksheet worksheet, List<string> headerRow, ParsedFileData result)
    {
        var tableName = "导入表格"; // 默认表格名称，可以从 Excel 工作表名称获取
        if (!string.IsNullOrWhiteSpace(worksheet.Name))
        {
            tableName = worksheet.Name;
        }

        var tableData = new List<Dictionary<string, string>>();

        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
        {
            var rowData = new Dictionary<string, string>();

            for (int col = 0; col < headerRow.Count; col++)
            {
                var columnName = headerRow[col];
                var cellValue = worksheet.Cells[row, col + 1].Text.Trim();

                if (!string.IsNullOrWhiteSpace(columnName))
                {
                    rowData[columnName] = cellValue;
                }
            }

            if (rowData.Count > 0)
            {
                tableData.Add(rowData);
            }
        }

        if (tableData.Count > 0)
        {
            result.Tables[tableName] = tableData;
        }

        return Task.CompletedTask;
    }
}
```

### 2.3 创建 JSON 解析器

创建 `Tools/FileParser/JsonParser.cs`：

```csharp
using System.Text.Json;
using FrameAgentWordFill.Models.Import;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// JSON 文件解析器
/// </summary>
public sealed class JsonParser
{
    private readonly ILogger<JsonParser> _logger;

    public JsonParser(ILogger<JsonParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析 JSON 文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>解析结果</returns>
    public async Task<ParsedFileData> ParseAsync(string filePath)
    {
        var result = new ParsedFileData { FileType = "JSON" };

        try
        {
            var jsonContent = await File.ReadAllTextAsync(filePath);
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            // JSON 支持两种格式：
            // 格式1: { "字段名": "值", "字段名2": "值2", "表格名": [ { "列名": "值" } ] }
            // 格式2: { "fields": { ... }, "tables": { ... } }

            if (root.TryGetProperty("fields", out var fieldsElement) &&
                root.TryGetProperty("tables", out var tablesElement))
            {
                // 格式2: 显式区分字段和表格
                ParseFields(fieldsElement, result);
                ParseTables(tablesElement, result);
            }
            else
            {
                // 格式1: 自动识别（数组类型为表格，其他为字段）
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        // 表格数据
                        ParseTable(property.Name, property.Value, result);
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String ||
                             property.Value.ValueKind == JsonValueKind.Number)
                    {
                        // 普通字段
                        result.Fields[property.Name] = property.Value.ToString();
                    }
                }
            }

            _logger.LogInformation($"JSON 解析完成：{result.Fields.Count} 个字段，{result.Tables.Count} 个表格");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON 解析失败");
            result.Errors.Add($"解析失败：{ex.Message}");
        }

        return result;
    }

    private void ParseFields(JsonElement fieldsElement, ParsedFileData result)
    {
        foreach (var property in fieldsElement.EnumerateObject())
        {
            result.Fields[property.Name] = property.Value.ToString();
        }
    }

    private void ParseTables(JsonElement tablesElement, ParsedFileData result)
    {
        foreach (var tableProperty in tablesElement.EnumerateObject())
        {
            ParseTable(tableProperty.Name, tableProperty.Value, result);
        }
    }

    private void ParseTable(string tableName, JsonElement arrayElement, ParsedFileData result)
    {
        var tableData = new List<Dictionary<string, string>>();

        foreach (var rowElement in arrayElement.EnumerateArray())
        {
            var rowData = new Dictionary<string, string>();

            foreach (var cellProperty in rowElement.EnumerateObject())
            {
                rowData[cellProperty.Name] = cellProperty.Value.ToString();
            }

            if (rowData.Count > 0)
            {
                tableData.Add(rowData);
            }
        }

        if (tableData.Count > 0)
        {
            result.Tables[tableName] = tableData;
        }
    }
}
```

### 2.4 创建 Word 表格解析器

创建 `Tools/FileParser/WordTableParser.cs`：

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FrameAgentWordFill.Models.Import;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// Word 文件解析器（提取表格数据）
/// </summary>
public sealed class WordTableParser
{
    private readonly ILogger<WordTableParser> _logger;

    public WordTableParser(ILogger<WordTableParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析 Word 文件中的表格
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>解析结果</returns>
    public async Task<ParsedFileData> ParseAsync(string filePath)
    {
        var result = new ParsedFileData { FileType = "Word" };

        try
        {
            await Task.Run(() =>
            {
                using var doc = WordprocessingDocument.Open(filePath, false);
                var body = doc.MainDocumentPart?.Document.Body;

                if (body == null)
                {
                    result.Errors.Add("无法读取 Word 文档内容");
                    return;
                }

                // 提取所有表格
                var tables = body.Elements<Table>().ToList();

                if (tables.Count == 0)
                {
                    result.Warnings.Add("Word 文档中未找到表格");
                    return;
                }

                for (int i = 0; i < tables.Count; i++)
                {
                    var table = tables[i];
                    var tableName = $"表格{i + 1}"; // 默认表格名称
                    var tableData = ParseSingleTable(table);

                    if (tableData.Count > 0)
                    {
                        result.Tables[tableName] = tableData;
                    }
                }

                _logger.LogInformation($"Word 解析完成：提取到 {result.Tables.Count} 个表格");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Word 解析失败");
            result.Errors.Add($"解析失败：{ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 解析单个表格
    /// </summary>
    private List<Dictionary<string, string>> ParseSingleTable(Table table)
    {
        var tableData = new List<Dictionary<string, string>>();
        var rows = table.Elements<TableRow>().ToList();

        if (rows.Count < 2)
        {
            // 表格至少需要 2 行（表头 + 数据）
            return tableData;
        }

        // 第一行作为表头
        var headerRow = rows[0];
        var headers = headerRow.Elements<TableCell>()
            .Select(cell => cell.InnerText.Trim())
            .ToList();

        // 后续行作为数据行
        for (int i = 1; i < rows.Count; i++)
        {
            var dataRow = rows[i];
            var cells = dataRow.Elements<TableCell>().ToList();
            var rowData = new Dictionary<string, string>();

            for (int j = 0; j < Math.Min(headers.Count, cells.Count); j++)
            {
                var columnName = headers[j];
                var cellValue = cells[j].InnerText.Trim();

                if (!string.IsNullOrWhiteSpace(columnName))
                {
                    rowData[columnName] = cellValue;
                }
            }

            if (rowData.Count > 0)
            {
                tableData.Add(rowData);
            }
        }

        return tableData;
    }
}
```

---

## 步骤 3：实现字段智能匹配算法

### 3.1 创建字段匹配器

创建 `Tools/FieldMatcher.cs`：

```csharp
using FrameAgentWordFill.Models.Import;
using FrameAgentWordFill.Models.Parsing;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 字段智能匹配器
/// 实现三级匹配策略：精确匹配 > 模糊匹配 > 语义匹配
/// </summary>
public sealed class FieldMatcher
{
    private readonly ILogger<FieldMatcher> _logger;

    // 常见同义词映射（用于增强匹配效果）
    private readonly Dictionary<string, List<string>> _synonyms = new()
    {
        { "项目名称", new List<string> { "项目名", "名称", "项目", "project", "name" } },
        { "负责人", new List<string> { "负责人姓名", "项目负责人", "主管", "经理", "manager", "leader" } },
        { "电话", new List<string> { "电话号码", "手机", "联系方式", "联系电话", "phone", "tel", "mobile" } },
        { "邮箱", new List<string> { "电子邮箱", "邮件", "email", "mail", "e-mail" } },
        { "日期", new List<string> { "时间", "日期时间", "date", "time", "datetime" } },
        { "金额", new List<string> { "费用", "预算", "价格", "总额", "amount", "price", "budget" } },
        { "备注", new List<string> { "说明", "描述", "备注信息", "remark", "note", "description" } }
    };

    public FieldMatcher(ILogger<FieldMatcher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 匹配字段（自动尝试三种匹配方式）
    /// </summary>
    /// <param name="sourceFields">源字段列表（从文件中提取的字段名）</param>
    /// <param name="templateFields">模板字段列表（模板中定义的字段）</param>
    /// <returns>字段映射列表</returns>
    public List<ImportFieldMapping> MatchFields(
        Dictionary<string, string> sourceFields,
        List<FieldInfo> templateFields)
    {
        var mappings = new List<ImportFieldMapping>();

        foreach (var (sourceFieldName, fieldValue) in sourceFields)
        {
            var mapping = new ImportFieldMapping
            {
                SourceFieldName = sourceFieldName,
                FieldValue = fieldValue,
                FieldType = "Normal"
            };

            // 策略1: 精确匹配（40%权重）
            var exactMatch = ExactMatch(sourceFieldName, templateFields);
            if (exactMatch != null)
            {
                mapping.TemplateFieldName = exactMatch.FieldName;
                mapping.MatchConfidence = 100;
                mapping.MatchMethod = "Exact";
                mappings.Add(mapping);
                continue;
            }

            // 策略2: 模糊匹配（30%权重）
            var fuzzyMatch = FuzzyMatch(sourceFieldName, templateFields);
            if (fuzzyMatch.Match != null && fuzzyMatch.Confidence >= 70)
            {
                mapping.TemplateFieldName = fuzzyMatch.Match.FieldName;
                mapping.MatchConfidence = fuzzyMatch.Confidence;
                mapping.MatchMethod = "Fuzzy";
                mappings.Add(mapping);
                continue;
            }

            // 策略3: 语义匹配（30%权重）
            var semanticMatch = SemanticMatch(sourceFieldName, templateFields);
            if (semanticMatch.Match != null && semanticMatch.Confidence >= 60)
            {
                mapping.TemplateFieldName = semanticMatch.Match.FieldName;
                mapping.MatchConfidence = semanticMatch.Confidence;
                mapping.MatchMethod = "Semantic";
                mappings.Add(mapping);
                continue;
            }

            // 无法匹配，标记为手动处理
            mapping.MatchConfidence = 0;
            mapping.MatchMethod = "Manual";
            mappings.Add(mapping);
        }

        _logger.LogInformation($"字段匹配完成：{mappings.Count(m => m.MatchConfidence >= 70)} 个高置信度匹配");

        return mappings;
    }

    /// <summary>
    /// 精确匹配（字段名完全相等或在同义词表中）
    /// </summary>
    private FieldInfo? ExactMatch(string sourceFieldName, List<FieldInfo> templateFields)
    {
        var normalized = NormalizeFieldName(sourceFieldName);

        // 直接匹配
        var directMatch = templateFields.FirstOrDefault(f =>
            NormalizeFieldName(f.FieldName).Equals(normalized, StringComparison.OrdinalIgnoreCase));

        if (directMatch != null)
        {
            return directMatch;
        }

        // 同义词匹配
        foreach (var (templateField, synonymList) in _synonyms)
        {
            if (synonymList.Any(s => s.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                var templateFieldInfo = templateFields.FirstOrDefault(f =>
                    NormalizeFieldName(f.FieldName).Equals(NormalizeFieldName(templateField), StringComparison.OrdinalIgnoreCase));

                if (templateFieldInfo != null)
                {
                    return templateFieldInfo;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 模糊匹配（基于编辑距离算法）
    /// </summary>
    private (FieldInfo? Match, int Confidence) FuzzyMatch(string sourceFieldName, List<FieldInfo> templateFields)
    {
        var normalized = NormalizeFieldName(sourceFieldName);
        FieldInfo? bestMatch = null;
        int maxSimilarity = 0;

        foreach (var templateField in templateFields)
        {
            var templateNormalized = NormalizeFieldName(templateField.FieldName);
            var similarity = CalculateSimilarity(normalized, templateNormalized);

            if (similarity > maxSimilarity)
            {
                maxSimilarity = similarity;
                bestMatch = templateField;
            }
        }

        return (bestMatch, maxSimilarity);
    }

    /// <summary>
    /// 语义匹配（基于同义词和语义相似度）
    /// </summary>
    private (FieldInfo? Match, int Confidence) SemanticMatch(string sourceFieldName, List<FieldInfo> templateFields)
    {
        var normalized = NormalizeFieldName(sourceFieldName);

        // 使用同义词表进行语义匹配
        foreach (var (templateFieldName, synonymList) in _synonyms)
        {
            foreach (var synonym in synonymList)
            {
                if (normalized.Contains(synonym, StringComparison.OrdinalIgnoreCase) ||
                    synonym.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    var templateField = templateFields.FirstOrDefault(f =>
                        NormalizeFieldName(f.FieldName).Contains(NormalizeFieldName(templateFieldName), StringComparison.OrdinalIgnoreCase));

                    if (templateField != null)
                    {
                        return (templateField, 75); // 语义匹配给75分
                    }
                }
            }
        }

        return (null, 0);
    }

    /// <summary>
    /// 规范化字段名（去除空格、特殊字符）
    /// </summary>
    private string NormalizeFieldName(string fieldName)
    {
        return Regex.Replace(fieldName, @"[\s\-_、\.\(\)]", "").ToLower();
    }

    /// <summary>
    /// 计算两个字符串的相似度（基于 Levenshtein 编辑距离）
    /// </summary>
    private int CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
        {
            return 0;
        }

        var distance = LevenshteinDistance(s1, s2);
        var maxLength = Math.Max(s1.Length, s2.Length);
        var similarity = (int)((1.0 - (double)distance / maxLength) * 100);

        return Math.Max(0, similarity);
    }

    /// <summary>
    /// Levenshtein 编辑距离算法
    /// </summary>
    private int LevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost
                );
            }
        }

        return matrix[s1.Length, s2.Length];
    }
}
```

---

## 步骤 4：实现导入会话管理

### 4.1 创建导入会话仓库

创建 `Repositories/ImportSessionRepository.cs`：

```csharp
using Microsoft.Data.Sqlite;
using FrameAgentWordFill.Models.Import;

namespace FrameAgentWordFill.Repositories;

/// <summary>
/// 导入会话数据访问层
/// </summary>
public sealed class ImportSessionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ImportSessionRepository> _logger;

    public ImportSessionRepository(IConfiguration configuration, ILogger<ImportSessionRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("连接字符串未配置");
        _logger = logger;
    }

    /// <summary>
    /// 创建导入会话
    /// </summary>
    public async Task<int> CreateSessionAsync(ImportSession session)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO fa_import_sessions (template_id, file_type, file_path, status, created_at, updated_at)
            VALUES (@templateId, @fileType, @filePath, @status, @createdAt, @updatedAt);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@templateId", session.TemplateId);
        command.Parameters.AddWithValue("@fileType", session.FileType);
        command.Parameters.AddWithValue("@filePath", session.FilePath);
        command.Parameters.AddWithValue("@status", session.Status);
        command.Parameters.AddWithValue("@createdAt", session.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@updatedAt", session.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

        var sessionId = Convert.ToInt32(await command.ExecuteScalarAsync());
        _logger.LogInformation($"创建导入会话成功：SessionId={sessionId}");

        return sessionId;
    }

    /// <summary>
    /// 更新会话状态
    /// </summary>
    public async Task UpdateSessionStatusAsync(int sessionId, string status, int matchedCount, int unmatchedCount, string? errorMessage = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE fa_import_sessions
            SET status = @status,
                matched_field_count = @matchedCount,
                unmatched_field_count = @unmatchedCount,
                error_message = @errorMessage,
                updated_at = @updatedAt
            WHERE session_id = @sessionId;";

        command.Parameters.AddWithValue("@sessionId", sessionId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@matchedCount", matchedCount);
        command.Parameters.AddWithValue("@unmatchedCount", unmatchedCount);
        command.Parameters.AddWithValue("@errorMessage", (object?)errorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 获取会话信息
    /// </summary>
    public async Task<ImportSession?> GetSessionByIdAsync(int sessionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM fa_import_sessions WHERE session_id = @sessionId;";
        command.Parameters.AddWithValue("@sessionId", sessionId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ImportSession
            {
                SessionId = reader.GetInt32(0),
                TemplateId = reader.GetInt32(1),
                FileType = reader.GetString(2),
                FilePath = reader.GetString(3),
                Status = reader.GetString(4),
                MatchedFieldCount = reader.GetInt32(5),
                UnmatchedFieldCount = reader.GetInt32(6),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                UpdatedAt = DateTime.Parse(reader.GetString(8)),
                ErrorMessage = reader.IsDBNull(9) ? null : reader.GetString(9)
            };
        }

        return null;
    }

    /// <summary>
    /// 保存字段映射
    /// </summary>
    public async Task SaveFieldMappingsAsync(int sessionId, List<ImportFieldMapping> mappings)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var mapping in mappings)
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO fa_import_field_mappings 
                    (session_id, source_field_name, template_field_name, field_value, match_confidence, match_method, field_type, created_at)
                    VALUES (@sessionId, @sourceFieldName, @templateFieldName, @fieldValue, @matchConfidence, @matchMethod, @fieldType, @createdAt);";

                command.Parameters.AddWithValue("@sessionId", sessionId);
                command.Parameters.AddWithValue("@sourceFieldName", mapping.SourceFieldName);
                command.Parameters.AddWithValue("@templateFieldName", (object?)mapping.TemplateFieldName ?? DBNull.Value);
                command.Parameters.AddWithValue("@fieldValue", (object?)mapping.FieldValue ?? DBNull.Value);
                command.Parameters.AddWithValue("@matchConfidence", mapping.MatchConfidence);
                command.Parameters.AddWithValue("@matchMethod", mapping.MatchMethod);
                command.Parameters.AddWithValue("@fieldType", mapping.FieldType);
                command.Parameters.AddWithValue("@createdAt", mapping.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

                await command.ExecuteNonQueryAsync();
            }

            transaction.Commit();
            _logger.LogInformation($"保存 {mappings.Count} 个字段映射成功");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 获取字段映射列表
    /// </summary>
    public async Task<List<ImportFieldMapping>> GetFieldMappingsAsync(int sessionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM fa_import_field_mappings WHERE session_id = @sessionId ORDER BY match_confidence DESC;";
        command.Parameters.AddWithValue("@sessionId", sessionId);

        var mappings = new List<ImportFieldMapping>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            mappings.Add(new ImportFieldMapping
            {
                MappingId = reader.GetInt32(0),
                SessionId = reader.GetInt32(1),
                SourceFieldName = reader.GetString(2),
                TemplateFieldName = reader.IsDBNull(3) ? null : reader.GetString(3),
                FieldValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                MatchConfidence = reader.GetInt32(5),
                MatchMethod = reader.GetString(6),
                IsUserConfirmed = reader.GetInt32(7) == 1,
                FieldType = reader.GetString(8),
                TableName = reader.IsDBNull(9) ? null : reader.GetString(9),
                ColumnName = reader.IsDBNull(10) ? null : reader.GetString(10),
                CreatedAt = DateTime.Parse(reader.GetString(11))
            });
        }

        return mappings;
    }

    /// <summary>
    /// 更新字段映射（用户手动调整后）
    /// </summary>
    public async Task UpdateFieldMappingAsync(int mappingId, string templateFieldName)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE fa_import_field_mappings
            SET template_field_name = @templateFieldName,
                match_confidence = 100,
                match_method = 'Manual',
                is_user_confirmed = 1
            WHERE mapping_id = @mappingId;";

        command.Parameters.AddWithValue("@mappingId", mappingId);
        command.Parameters.AddWithValue("@templateFieldName", templateFieldName);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 保存表格数据
    /// </summary>
    public async Task SaveTableDataAsync(int sessionId, string tableName, List<Dictionary<string, string>> tableData)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            for (int rowIndex = 0; rowIndex < tableData.Count; rowIndex++)
            {
                var row = tableData[rowIndex];

                foreach (var (columnName, cellValue) in row)
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO fa_import_table_data 
                        (session_id, table_name, row_index, column_name, cell_value, created_at)
                        VALUES (@sessionId, @tableName, @rowIndex, @columnName, @cellValue, @createdAt);";

                    command.Parameters.AddWithValue("@sessionId", sessionId);
                    command.Parameters.AddWithValue("@tableName", tableName);
                    command.Parameters.AddWithValue("@rowIndex", rowIndex);
                    command.Parameters.AddWithValue("@columnName", columnName);
                    command.Parameters.AddWithValue("@cellValue", (object?)cellValue ?? DBNull.Value);
                    command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                    await command.ExecuteNonQueryAsync();
                }
            }

            transaction.Commit();
            _logger.LogInformation($"保存表格数据成功：{tableName}，{tableData.Count} 行");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 获取表格数据
    /// </summary>
    public async Task<Dictionary<string, List<Dictionary<string, string>>>> GetTableDataAsync(int sessionId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM fa_import_table_data WHERE session_id = @sessionId ORDER BY table_name, row_index;";
        command.Parameters.AddWithValue("@sessionId", sessionId);

        var tables = new Dictionary<string, List<Dictionary<string, string>>>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(2);
            var rowIndex = reader.GetInt32(3);
            var columnName = reader.GetString(4);
            var cellValue = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);

            if (!tables.ContainsKey(tableName))
            {
                tables[tableName] = new List<Dictionary<string, string>>();
            }

            // 确保有足够的行
            while (tables[tableName].Count <= rowIndex)
            {
                tables[tableName].Add(new Dictionary<string, string>());
            }

            tables[tableName][rowIndex][columnName] = cellValue;
        }

        return tables;
    }
}
```

---

## 步骤 5：实现导入服务和API

### 5.1 创建导入服务

创建 `Services/ImportService.cs`：

```csharp
using FrameAgentWordFill.Models.Import;
using FrameAgentWordFill.Repositories;
using FrameAgentWordFill.Tools;
using FrameAgentWordFill.Tools.FileParser;

namespace FrameAgentWordFill.Services;

/// <summary>
/// 导入服务（业务逻辑层）
/// </summary>
public sealed class ImportService
{
    private readonly ImportSessionRepository _sessionRepository;
    private readonly TemplateRepository _templateRepository;
    private readonly FileStorageService _fileStorage;
    private readonly ExcelParser _excelParser;
    private readonly JsonParser _jsonParser;
    private readonly WordTableParser _wordParser;
    private readonly FieldMatcher _fieldMatcher;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        ImportSessionRepository sessionRepository,
        TemplateRepository templateRepository,
        FileStorageService fileStorage,
        ExcelParser excelParser,
        JsonParser jsonParser,
        WordTableParser wordParser,
        FieldMatcher fieldMatcher,
        ILogger<ImportService> logger)
    {
        _sessionRepository = sessionRepository;
        _templateRepository = templateRepository;
        _fileStorage = fileStorage;
        _excelParser = excelParser;
        _jsonParser = jsonParser;
        _wordParser = wordParser;
        _fieldMatcher = fieldMatcher;
        _logger = logger;
    }

    /// <summary>
    /// 上传文件并创建导入会话
    /// </summary>
    public async Task<(int SessionId, string ErrorMessage)> UploadFileAndCreateSessionAsync(
        int templateId,
        IFormFile file)
    {
        try
        {
            // 1. 验证模板是否存在
            var template = await _templateRepository.GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                return (-1, "模板不存在");
            }

            // 2. 验证文件类型
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            var fileType = fileExtension switch
            {
                ".xlsx" or ".xls" => "Excel",
                ".json" => "JSON",
                ".docx" or ".doc" => "Word",
                _ => null
            };

            if (fileType == null)
            {
                return (-1, "不支持的文件类型，仅支持 Excel、JSON、Word");
            }

            // 3. 保存文件到 /storage/uploads/
            var filePath = await _fileStorage.SaveUploadFileAsync(file);

            // 4. 创建导入会话
            var session = new ImportSession
            {
                TemplateId = templateId,
                FileType = fileType,
                FilePath = filePath,
                Status = "Parsing"
            };

            var sessionId = await _sessionRepository.CreateSessionAsync(session);

            _logger.LogInformation($"导入会话创建成功：SessionId={sessionId}，FileType={fileType}");

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
            // 1. 获取会话信息
            var session = await _sessionRepository.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return (false, "导入会话不存在");
            }

            // 2. 获取模板信息和字段定义
            var template = await _templateRepository.GetTemplateByIdAsync(session.TemplateId);
            if (template == null)
            {
                return (false, "模板不存在");
            }

            var parseResult = await _templateRepository.GetTemplateParseResultAsync(session.TemplateId);
            if (parseResult == null)
            {
                return (false, "模板解析结果不存在");
            }

            // 3. 解析文件
            var fullPath = _fileStorage.GetUploadFilePath(session.FilePath);
            ParsedFileData parsedData;

            switch (session.FileType)
            {
                case "Excel":
                    parsedData = await _excelParser.ParseAsync(fullPath);
                    break;
                case "JSON":
                    parsedData = await _jsonParser.ParseAsync(fullPath);
                    break;
                case "Word":
                    parsedData = await _wordParser.ParseAsync(fullPath);
                    break;
                default:
                    return (false, $"不支持的文件类型：{session.FileType}");
            }

            if (parsedData.Errors.Count > 0)
            {
                var errorMessage = string.Join("; ", parsedData.Errors);
                await _sessionRepository.UpdateSessionStatusAsync(sessionId, "Failed", 0, 0, errorMessage);
                return (false, errorMessage);
            }

            // 4. 匹配普通字段
            var fieldMappings = _fieldMatcher.MatchFields(parsedData.Fields, parseResult.Fields);
            await _sessionRepository.SaveFieldMappingsAsync(sessionId, fieldMappings);

            // 5. 保存表格数据
            foreach (var (tableName, tableData) in parsedData.Tables)
            {
                await _sessionRepository.SaveTableDataAsync(sessionId, tableName, tableData);
            }

            // 6. 统计匹配结果
            var matchedCount = fieldMappings.Count(m => m.MatchConfidence >= 70);
            var unmatchedCount = fieldMappings.Count(m => m.MatchConfidence < 70);

            await _sessionRepository.UpdateSessionStatusAsync(sessionId, "WaitingConfirm", matchedCount, unmatchedCount);

            _logger.LogInformation($"文件解析和字段匹配完成：匹配 {matchedCount} 个字段，未匹配 {unmatchedCount} 个字段");

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
        _logger.LogInformation($"字段映射已更新：MappingId={mappingId}，TemplateField={templateFieldName}");
    }

    /// <summary>
    /// 生成文档（基于导入数据）
    /// </summary>
    public async Task<(bool Success, string FilePath, string ErrorMessage)> GenerateDocumentAsync(int sessionId)
    {
        try
        {
            // 1. 获取字段映射
            var mappings = await _sessionRepository.GetFieldMappingsAsync(sessionId);

            // 2. 获取表格数据
            var tableData = await _sessionRepository.GetTableDataAsync(sessionId);

            // 3. 构建生成请求数据
            var fieldData = new Dictionary<string, object>();

            foreach (var mapping in mappings.Where(m => m.TemplateFieldName != null))
            {
                fieldData[mapping.TemplateFieldName!] = mapping.FieldValue ?? string.Empty;
            }

            foreach (var (tableName, rows) in tableData)
            {
                fieldData[tableName] = rows;
            }

            // 4. 调用生成服务生成文档
            // TODO: 集成 GenerateService，实现文档生成

            _logger.LogInformation($"导入数据生成文档完成：SessionId={sessionId}");

            return (true, "output_path", string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成文档失败");
            return (false, string.Empty, ex.Message);
        }
    }
}
```

### 5.2 创建导入控制器

创建 `Controllers/ImportController.cs`：

```csharp
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
    public async Task<IActionResult> UploadFile([FromForm] int templateId, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "文件不能为空" });
        }

        var (sessionId, errorMessage) = await _importService.UploadFileAndCreateSessionAsync(templateId, file);

        if (sessionId == -1)
        {
            return BadRequest(new { error = errorMessage });
        }

        return Ok(new { sessionId, message = "文件上传成功" });
    }

    /// <summary>
    /// 解析文件并匹配字段
    /// </summary>
    [HttpPost("parse/{sessionId}")]
    public async Task<IActionResult> ParseFile(int sessionId)
    {
        var (success, errorMessage) = await _importService.ParseAndMatchFieldsAsync(sessionId);

        if (!success)
        {
            return BadRequest(new { error = errorMessage });
        }

        return Ok(new { message = "文件解析完成" });
    }

    /// <summary>
    /// 获取字段匹配结果
    /// </summary>
    [HttpGet("mappings/{sessionId}")]
    public async Task<IActionResult> GetFieldMappings(int sessionId)
    {
        var (session, mappings) = await _importService.GetFieldMappingsAsync(sessionId);

        if (session == null)
        {
            return NotFound(new { error = "导入会话不存在" });
        }

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
    [HttpPut("mappings/{mappingId}")]
    public async Task<IActionResult> UpdateFieldMapping(int mappingId, [FromBody] UpdateMappingRequest request)
    {
        await _importService.UpdateFieldMappingAsync(mappingId, request.TemplateFieldName);
        return Ok(new { message = "字段映射已更新" });
    }

    /// <summary>
    /// 生成文档
    /// </summary>
    [HttpPost("generate/{sessionId}")]
    public async Task<IActionResult> GenerateDocument(int sessionId)
    {
        var (success, filePath, errorMessage) = await _importService.GenerateDocumentAsync(sessionId);

        if (!success)
        {
            return BadRequest(new { error = errorMessage });
        }

        return Ok(new { filePath, message = "文档生成成功" });
    }
}

public sealed record UpdateMappingRequest(string TemplateFieldName);
```

### 5.3 注册服务（Program.cs）

在 `Program.cs` 中注册新服务：

```csharp
// W5: 导入填充服务
builder.Services.AddScoped<ImportSessionRepository>();
builder.Services.AddScoped<ExcelParser>();
builder.Services.AddScoped<JsonParser>();
builder.Services.AddScoped<WordTableParser>();
builder.Services.AddScoped<FieldMatcher>();
builder.Services.AddScoped<ImportService>();
```

---

## 步骤 6：实现前端导入界面

### 6.1 创建导入填充页面

创建 `frontend/src/views/user/ImportFill.vue`：

```vue
<template>
  <div class="import-fill-container">
    <el-card class="header-card">
      <h2>导入填充</h2>
      <p>上传 Excel、JSON 或 Word 文件自动填充模板</p>
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
      <h3>步骤 2：上传文件</h3>
      <el-upload
        ref="uploadRef"
        :auto-upload="false"
        :limit="1"
        :on-change="handleFileChange"
        :file-list="fileList"
        accept=".xlsx,.xls,.json,.docx,.doc"
      >
        <el-button type="primary">选择文件</el-button>
        <template #tip>
          <div class="el-upload__tip">
            支持 Excel (.xlsx/.xls)、JSON (.json)、Word (.docx/.doc) 格式
          </div>
        </template>
      </el-upload>
      <div class="button-group">
        <el-button @click="prevStep">上一步</el-button>
        <el-button type="primary" :disabled="!uploadFile" :loading="uploading" @click="uploadAndParse">
          上传并解析
        </el-button>
      </div>
    </el-card>

    <!-- 步骤3: 字段匹配 -->
    <el-card v-if="currentStep === 3">
      <h3>步骤 3：字段匹配</h3>
      <el-alert
        v-if="session"
        :title="`匹配结果：${session.matchedFieldCount} 个字段已自动匹配，${session.unmatchedFieldCount} 个字段需手动调整`"
        type="info"
        :closable="false"
        style="margin-bottom: 20px"
      />
      
      <el-table :data="fieldMappings" stripe style="width: 100%">
        <el-table-column prop="sourceFieldName" label="源字段名" width="180" />
        <el-table-column label="模板字段" width="250">
          <template #default="{ row }">
            <el-select
              v-model="row.templateFieldName"
              placeholder="选择模板字段"
              @change="handleMappingChange(row)"
            >
              <el-option
                v-for="field in templateFields"
                :key="field.fieldName"
                :label="field.fieldName"
                :value="field.fieldName"
              />
            </el-select>
          </template>
        </el-table-column>
        <el-table-column prop="fieldValue" label="字段值" show-overflow-tooltip />
        <el-table-column label="匹配置信度" width="150">
          <template #default="{ row }">
            <el-tag :type="getConfidenceType(row.matchConfidence)">
              {{ row.matchConfidence }}%
            </el-tag>
            <el-tag v-if="row.isUserConfirmed" type="success" size="small" style="margin-left: 5px">
              已确认
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="matchMethod" label="匹配方式" width="120">
          <template #default="{ row }">
            <el-tag size="small">{{ getMatchMethodText(row.matchMethod) }}</el-tag>
          </template>
        </el-table-column>
      </el-table>

      <div class="button-group">
        <el-button @click="prevStep">上一步</el-button>
        <el-button type="primary" @click="nextStep">下一步</el-button>
      </div>
    </el-card>

    <!-- 步骤4: 生成文档 -->
    <el-card v-if="currentStep === 4">
      <h3>步骤 4：生成文档</h3>
      <el-result icon="success" title="准备就绪" sub-title="点击下方按钮生成文档">
        <template #extra>
          <el-button type="primary" :loading="generating" @click="generateDocument">
            生成文档
          </el-button>
        </template>
      </el-result>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import type { UploadUserFile, UploadFile } from 'element-plus';
import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

// 状态
const currentStep = ref(1);
const selectedTemplateId = ref<number | null>(null);
const templates = ref<any[]>([]);
const templateFields = ref<any[]>([]);
const uploadFile = ref<File | null>(null);
const fileList = ref<UploadUserFile[]>([]);
const uploading = ref(false);
const sessionId = ref<number | null>(null);
const session = ref<any>(null);
const fieldMappings = ref<any[]>([]);
const generating = ref(false);

// 加载模板列表
const loadTemplates = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/templates`);
    templates.value = response.data;
  } catch (error) {
    ElMessage.error('加载模板列表失败');
  }
};

// 加载模板字段
const loadTemplateFields = async () => {
  if (!selectedTemplateId.value) return;

  try {
    const response = await axios.get(`${API_BASE_URL}/templates/${selectedTemplateId.value}/fields`);
    templateFields.value = response.data;
  } catch (error) {
    ElMessage.error('加载模板字段失败');
  }
};

// 文件选择
const handleFileChange = (file: UploadFile) => {
  uploadFile.value = file.raw || null;
};

// 上传并解析
const uploadAndParse = async () => {
  if (!uploadFile.value || !selectedTemplateId.value) return;

  uploading.value = true;

  try {
    // 1. 上传文件
    const formData = new FormData();
    formData.append('file', uploadFile.value);
    formData.append('templateId', selectedTemplateId.value.toString());

    const uploadResponse = await axios.post(`${API_BASE_URL}/import/upload`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    });

    sessionId.value = uploadResponse.data.sessionId;

    // 2. 解析文件
    await axios.post(`${API_BASE_URL}/import/parse/${sessionId.value}`);

    // 3. 获取匹配结果
    const mappingsResponse = await axios.get(`${API_BASE_URL}/import/mappings/${sessionId.value}`);
    session.value = mappingsResponse.data.session;
    fieldMappings.value = mappingsResponse.data.mappings;

    // 4. 加载模板字段（用于下拉选择）
    await loadTemplateFields();

    ElMessage.success('文件解析完成');
    currentStep.value = 3;
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '上传失败');
  } finally {
    uploading.value = false;
  }
};

// 字段匹配修改
const handleMappingChange = async (row: any) => {
  try {
    await axios.put(`${API_BASE_URL}/import/mappings/${row.mappingId}`, {
      templateFieldName: row.templateFieldName
    });
    row.isUserConfirmed = true;
    ElMessage.success('字段映射已更新');
  } catch (error) {
    ElMessage.error('更新失败');
  }
};

// 生成文档
const generateDocument = async () => {
  if (!sessionId.value) return;

  generating.value = true;

  try {
    const response = await axios.post(`${API_BASE_URL}/import/generate/${sessionId.value}`);
    ElMessage.success('文档生成成功');
    // TODO: 跳转到下载页面或自动下载
  } catch (error: any) {
    ElMessage.error(error.response?.data?.error || '生成失败');
  } finally {
    generating.value = false;
  }
};

// 步骤控制
const nextStep = () => {
  if (currentStep.value < 4) {
    currentStep.value++;
  }
};

const prevStep = () => {
  if (currentStep.value > 1) {
    currentStep.value--;
  }
};

// 置信度类型
const getConfidenceType = (confidence: number) => {
  if (confidence >= 90) return 'success';
  if (confidence >= 70) return '';
  return 'warning';
};

// 匹配方式文本
const getMatchMethodText = (method: string) => {
  const map: Record<string, string> = {
    Exact: '精确匹配',
    Fuzzy: '模糊匹配',
    Semantic: '语义匹配',
    Manual: '手动调整'
  };
  return map[method] || method;
};

onMounted(() => {
  loadTemplates();
});
</script>

<style scoped>
.import-fill-container {
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
</style>
```

### 6.2 更新路由配置

编辑 `frontend/src/router/index.ts`，添加导入填充路由：

```typescript
{
  path: '/import-fill',
  name: 'ImportFill',
  component: () => import('../views/user/ImportFill.vue'),
  meta: { title: '导入填充' }
}
```

### 6.3 更新主菜单

编辑 `frontend/src/App.vue`，添加导入填充菜单项：

```vue
<el-menu-item index="/import-fill">
  <el-icon><Upload /></el-icon>
  <span>导入填充</span>
</el-menu-item>
```

---

## 步骤 7：测试和验收

### 7.1 单元测试计划

| 测试项 | 测试方法 | 预期结果 |
|--------|----------|----------|
| Excel 解析 | 上传包含字段-值对的 Excel | 成功提取所有字段 |
| JSON 解析 | 上传标准 JSON 文件 | 成功解析字段和表格 |
| Word 表格解析 | 上传包含表格的 Word | 成功提取表格数据 |
| 精确匹配 | 字段名完全一致 | 置信度 100%，匹配方式为 Exact |
| 模糊匹配 | 字段名相似但不完全一致 | 置信度 70-89%，匹配方式为 Fuzzy |
| 语义匹配 | 字段名是同义词 | 置信度 60-79%，匹配方式为 Semantic |
| 手动调整 | 用户修改匹配关系 | 置信度变为 100%，is_user_confirmed 为 true |
| 文档生成 | 基于导入数据生成文档 | 文档内容与导入数据一致 |

### 7.2 集成测试脚本

创建 `test_import.ps1`：

```powershell
# W5 导入填充功能测试脚本

$API_BASE = "http://localhost:5000/api"

Write-Host "=== W5 导入填充功能测试 ===" -ForegroundColor Green

# 1. 创建测试 Excel 文件（使用 Python 脚本）
Write-Host "`n[1/5] 创建测试 Excel 文件..." -ForegroundColor Cyan
python -c @"
import openpyxl
wb = openpyxl.Workbook()
ws = wb.active
ws['A1'] = '项目名称'
ws['B1'] = '智能文档填充系统'
ws['A2'] = '负责人'
ws['B2'] = '张三'
ws['A3'] = '预算'
ws['B3'] = '500000'
wb.save('test_import.xlsx')
"@
Write-Host "✅ Excel 文件创建成功" -ForegroundColor Green

# 2. 上传测试文件
Write-Host "`n[2/5] 上传测试文件..." -ForegroundColor Cyan
$uploadResponse = Invoke-RestMethod -Uri "$API_BASE/import/upload" -Method Post -Form @{
    templateId = 1
    file = Get-Item -Path "test_import.xlsx"
} -ContentType "multipart/form-data"

$sessionId = $uploadResponse.sessionId
Write-Host "✅ 文件上传成功，SessionId: $sessionId" -ForegroundColor Green

# 3. 解析文件
Write-Host "`n[3/5] 解析文件..." -ForegroundColor Cyan
Invoke-RestMethod -Uri "$API_BASE/import/parse/$sessionId" -Method Post
Write-Host "✅ 文件解析完成" -ForegroundColor Green

# 4. 获取字段匹配结果
Write-Host "`n[4/5] 获取字段匹配结果..." -ForegroundColor Cyan
$mappingsResponse = Invoke-RestMethod -Uri "$API_BASE/import/mappings/$sessionId" -Method Get

Write-Host "匹配结果：" -ForegroundColor Yellow
Write-Host "  已匹配字段：$($mappingsResponse.session.matchedFieldCount)" -ForegroundColor Green
Write-Host "  未匹配字段：$($mappingsResponse.session.unmatchedFieldCount)" -ForegroundColor Yellow

foreach ($mapping in $mappingsResponse.mappings) {
    Write-Host "  - $($mapping.sourceFieldName) → $($mapping.templateFieldName) ($($mapping.matchConfidence)%)" -ForegroundColor Cyan
}

# 5. 生成文档
Write-Host "`n[5/5] 生成文档..." -ForegroundColor Cyan
$generateResponse = Invoke-RestMethod -Uri "$API_BASE/import/generate/$sessionId" -Method Post
Write-Host "✅ 文档生成成功：$($generateResponse.filePath)" -ForegroundColor Green

Write-Host "`n=== 测试完成 ===" -ForegroundColor Green
```

### 7.3 验收标准

✅ **功能验收**：
1. 支持 Excel、JSON、Word 文件上传
2. 成功解析文件中的字段和表格数据
3. 字段匹配准确率 ≥ 80%（对于标准命名的字段）
4. 用户可手动调整匹配关系
5. 基于导入数据成功生成 Word 文档

✅ **性能验收**：
1. 文件上传响应时间 < 2 秒
2. Excel/JSON 解析时间 < 3 秒（< 1000 行）
3. Word 解析时间 < 5 秒（< 10 个表格）
4. 字段匹配时间 < 1 秒（< 50 个字段）

✅ **用户体验验收**：
1. 界面清晰，步骤流程明确
2. 匹配结果可视化，用户易于理解
3. 提供置信度评分和颜色标识
4. 支持快速调整和批量确认

---

## 📝 总结

### 本周新增文件清单

**后端文件**（12 个）：
1. `Models/Import/ImportSession.cs` - 导入会话模型
2. `Models/Import/ImportFieldMapping.cs` - 字段映射模型
3. `Models/Import/ImportTableData.cs` - 表格数据模型
4. `Models/Import/ParsedFileData.cs` - 解析结果模型
5. `Tools/FileParser/ExcelParser.cs` - Excel 解析器
6. `Tools/FileParser/JsonParser.cs` - JSON 解析器
7. `Tools/FileParser/WordTableParser.cs` - Word 表格解析器
8. `Tools/FieldMatcher.cs` - 字段匹配器
9. `Repositories/ImportSessionRepository.cs` - 导入会话仓库
10. `Services/ImportService.cs` - 导入服务
11. `Controllers/ImportController.cs` - 导入控制器
12. `Data/DatabaseSchema.sql` - 数据库表结构（更新）

**前端文件**（1 个）：
1. `frontend/src/views/user/ImportFill.vue` - 导入填充页面

**总计**：13 个核心文件

### 技术要点回顾

1. **多文件格式支持**：Excel（EPPlus）、JSON（System.Text.Json）、Word（OpenXML SDK）
2. **三级匹配策略**：精确匹配 → 模糊匹配（编辑距离）→ 语义匹配（同义词表）
3. **可视化匹配界面**：置信度评分、颜色标识、手动调整
4. **数据库事务支持**：批量保存字段映射和表格数据
5. **用户体验优化**：分步引导、实时反馈、一键生成

### 下一步：W6 - 复杂模板能力增强

进入 W6 开发阶段，实现：
- 模板预检工具
- 占位符规范化
- 内容控件处理
- 图片替换功能
- 错误提示和修复建议

**参考文档**：
- 02_ref_w6_实现流程.md（待创建）
- 03_guide_项目执行计划.md


