using Microsoft.Data.Sqlite;
using FrameAgentWordFill.Models.Templates;

namespace FrameAgentWordFill.Repositories;

/// <summary>
/// 模板数据访问层（⚠️ 注意：所有表名使用 fa_ 前缀）
/// </summary>
public sealed class TemplateRepository
{
    private readonly string _connectionString;
    private readonly ILogger<TemplateRepository> _logger;

    public TemplateRepository(IConfiguration configuration, IHostEnvironment hostEnvironment, ILogger<TemplateRepository> logger)
    {
        _logger = logger;
        var dbPath = GetDatabasePath(configuration, hostEnvironment);
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
    /// 更新模板状态
    /// </summary>
    public async Task<bool> UpdateTemplateStatusAsync(string templateId, string status)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE fa_templates 
            SET status = @status, updated_at = @updatedAt
            WHERE id = @id
        ";
        command.Parameters.AddWithValue("@id", templateId);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

        var rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
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
        await using var transaction = connection.BeginTransaction();

        try
        {
            // 兼容历史库结构：fa_chat_sessions 的外键可能未配置 ON DELETE CASCADE。
            // 先删除会话字段，再删除会话，最后删除模板，避免 FK 约束失败。
            await using (var deleteSessionFields = connection.CreateCommand())
            {
                deleteSessionFields.Transaction = transaction;
                deleteSessionFields.CommandText = @"
                    DELETE FROM fa_session_fields
                    WHERE session_id IN (
                        SELECT id FROM fa_chat_sessions WHERE template_id = @templateId
                    )
                ";
                deleteSessionFields.Parameters.AddWithValue("@templateId", templateId);
                await deleteSessionFields.ExecuteNonQueryAsync();
            }

            await using (var deleteChatSessions = connection.CreateCommand())
            {
                deleteChatSessions.Transaction = transaction;
                deleteChatSessions.CommandText = "DELETE FROM fa_chat_sessions WHERE template_id = @templateId";
                deleteChatSessions.Parameters.AddWithValue("@templateId", templateId);
                await deleteChatSessions.ExecuteNonQueryAsync();
            }

            await using var deleteTemplate = connection.CreateCommand();
            deleteTemplate.Transaction = transaction;
            deleteTemplate.CommandText = "DELETE FROM fa_templates WHERE id = @id";
            deleteTemplate.Parameters.AddWithValue("@id", templateId);

            var rows = await deleteTemplate.ExecuteNonQueryAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("模板删除: {TemplateId}, 影响行数: {Rows}", templateId, rows);
            return rows > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "删除模板失败: {TemplateId}", templateId);
            return false;
        }
    }

    private static string GetDatabasePath(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        var rootPath = configuration["Storage:RootPath"] ?? "..\\..\\storage";
        var fullPath = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, rootPath));
        return Path.Combine(fullPath, "data", "frameagent.db");
    }
}
