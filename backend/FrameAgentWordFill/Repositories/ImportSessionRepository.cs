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

    public ImportSessionRepository(IConfiguration configuration, IHostEnvironment hostEnvironment, ILogger<ImportSessionRepository> logger)
    {
        _logger = logger;
        var dbPath = GetDatabasePath(configuration, hostEnvironment);
        _connectionString = $"Data Source={dbPath}";
    }

    private static string GetDatabasePath(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        var rootPath = configuration["Storage:RootPath"] ?? "..\\..\\storage";
        var fullPath = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, rootPath));
        return Path.Combine(fullPath, "data", "frameagent.db");
    }

    public async Task<int> CreateSessionAsync(ImportSession session)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO fa_import_sessions (template_id, file_type, file_path, status, created_at, updated_at)
            VALUES (@templateId, @fileType, @filePath, @status, @createdAt, @updatedAt);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@templateId", session.TemplateId);
        cmd.Parameters.AddWithValue("@fileType", session.FileType);
        cmd.Parameters.AddWithValue("@filePath", session.FilePath);
        cmd.Parameters.AddWithValue("@status", session.Status);
        cmd.Parameters.AddWithValue("@createdAt", session.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@updatedAt", session.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        _logger.LogInformation("创建导入会话成功：SessionId={SessionId}", id);
        return id;
    }

    public async Task UpdateSessionStatusAsync(int sessionId, string status, int matchedCount, int unmatchedCount, string? errorMessage = null)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE fa_import_sessions
            SET status = @status,
                matched_field_count = @matchedCount,
                unmatched_field_count = @unmatchedCount,
                error_message = @errorMessage,
                updated_at = @updatedAt
            WHERE session_id = @sessionId;";
        cmd.Parameters.AddWithValue("@sessionId", sessionId);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@matchedCount", matchedCount);
        cmd.Parameters.AddWithValue("@unmatchedCount", unmatchedCount);
        cmd.Parameters.AddWithValue("@errorMessage", (object?)errorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<ImportSession?> GetSessionByIdAsync(int sessionId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM fa_import_sessions WHERE session_id = @sessionId;";
        cmd.Parameters.AddWithValue("@sessionId", sessionId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ImportSession
            {
                SessionId = reader.GetInt32(0),
                TemplateId = reader.GetString(1),
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

    public async Task SaveFieldMappingsAsync(int sessionId, List<ImportFieldMapping> mappings)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var mapping in mappings)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO fa_import_field_mappings
                    (session_id, source_field_name, template_field_name, field_value, match_confidence, match_method, field_type, created_at)
                    VALUES (@sessionId, @sourceFieldName, @templateFieldName, @fieldValue, @matchConfidence, @matchMethod, @fieldType, @createdAt);";
                cmd.Parameters.AddWithValue("@sessionId", sessionId);
                cmd.Parameters.AddWithValue("@sourceFieldName", mapping.SourceFieldName);
                cmd.Parameters.AddWithValue("@templateFieldName", (object?)mapping.TemplateFieldName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fieldValue", (object?)mapping.FieldValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@matchConfidence", mapping.MatchConfidence);
                cmd.Parameters.AddWithValue("@matchMethod", mapping.MatchMethod);
                cmd.Parameters.AddWithValue("@fieldType", mapping.FieldType);
                cmd.Parameters.AddWithValue("@createdAt", mapping.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                await cmd.ExecuteNonQueryAsync();
            }
            transaction.Commit();
            _logger.LogInformation("保存 {Count} 个字段映射成功", mappings.Count);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<List<ImportFieldMapping>> GetFieldMappingsAsync(int sessionId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM fa_import_field_mappings WHERE session_id = @sessionId ORDER BY match_confidence DESC;";
        cmd.Parameters.AddWithValue("@sessionId", sessionId);

        var mappings = new List<ImportFieldMapping>();
        await using var reader = await cmd.ExecuteReaderAsync();
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

    public async Task UpdateFieldMappingAsync(int mappingId, string templateFieldName)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE fa_import_field_mappings
            SET template_field_name = @templateFieldName,
                match_confidence = 100,
                match_method = 'Manual',
                is_user_confirmed = 1
            WHERE mapping_id = @mappingId;";
        cmd.Parameters.AddWithValue("@mappingId", mappingId);
        cmd.Parameters.AddWithValue("@templateFieldName", templateFieldName);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveTableDataAsync(int sessionId, string tableName, List<Dictionary<string, string>> tableData)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            for (int rowIndex = 0; rowIndex < tableData.Count; rowIndex++)
            {
                foreach (var (columnName, cellValue) in tableData[rowIndex])
                {
                    await using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO fa_import_table_data
                        (session_id, table_name, row_index, column_name, cell_value, created_at)
                        VALUES (@sessionId, @tableName, @rowIndex, @columnName, @cellValue, @createdAt);";
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    cmd.Parameters.AddWithValue("@rowIndex", rowIndex);
                    cmd.Parameters.AddWithValue("@columnName", columnName);
                    cmd.Parameters.AddWithValue("@cellValue", (object?)cellValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            transaction.Commit();
            _logger.LogInformation("保存表格数据成功：{TableName}，{RowCount} 行", tableName, tableData.Count);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<Dictionary<string, List<Dictionary<string, string>>>> GetTableDataAsync(int sessionId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM fa_import_table_data WHERE session_id = @sessionId ORDER BY table_name, row_index;";
        cmd.Parameters.AddWithValue("@sessionId", sessionId);

        var tables = new Dictionary<string, List<Dictionary<string, string>>>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(2);
            var rowIndex = reader.GetInt32(3);
            var columnName = reader.GetString(4);
            var cellValue = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);

            if (!tables.ContainsKey(tableName))
                tables[tableName] = new List<Dictionary<string, string>>();

            while (tables[tableName].Count <= rowIndex)
                tables[tableName].Add(new Dictionary<string, string>());

            tables[tableName][rowIndex][columnName] = cellValue;
        }
        return tables;
    }
}
