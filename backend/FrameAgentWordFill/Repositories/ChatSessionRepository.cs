using Microsoft.Data.Sqlite;
using FrameAgentWordFill.Models.Chat;

namespace FrameAgentWordFill.Repositories;

/// <summary>
/// 对话会话数据访问层（⚠️ 注意：使用 fa_ 前缀）
/// </summary>
public sealed class ChatSessionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ChatSessionRepository> _logger;

    public ChatSessionRepository(IConfiguration configuration, IHostEnvironment hostEnvironment, ILogger<ChatSessionRepository> logger)
    {
        _logger = logger;
        var dbPath = GetDatabasePath(configuration, hostEnvironment);
        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// 创建会话
    /// </summary>
    public async Task<bool> CreateSessionAsync(ChatSession session)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO fa_chat_sessions (id, template_id, user_id, status, created_at, updated_at)
                VALUES (@id, @templateId, @userId, @status, @createdAt, @updatedAt)
            ";
            command.Parameters.AddWithValue("@id", session.Id);
            command.Parameters.AddWithValue("@templateId", session.TemplateId);
            command.Parameters.AddWithValue("@userId", session.UserId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@status", session.Status);
            command.Parameters.AddWithValue("@createdAt", session.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("@updatedAt", session.UpdatedAt.ToString("o"));

            var rows = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("创建会话: {SessionId}, 影响行数: {Rows}", session.Id, rows);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败: {SessionId}", session.Id);
            return false;
        }
    }

    /// <summary>
    /// 获取会话
    /// </summary>
    public async Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, template_id, user_id, status, created_at, updated_at
                FROM fa_chat_sessions
                WHERE id = @id
            ";
            command.Parameters.AddWithValue("@id", sessionId);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                _logger.LogWarning("会话不存在: {SessionId}", sessionId);
                return null;
            }

            var session = new ChatSession
            {
                Id = reader.GetString(0),
                TemplateId = reader.GetString(1),
                UserId = reader.IsDBNull(2) ? null : reader.GetString(2),
                Status = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                UpdatedAt = DateTime.Parse(reader.GetString(5))
            };

            // 加载会话字段
            await LoadSessionFieldsAsync(connection, session);

            _logger.LogDebug("获取会话成功: {SessionId}, 字段数: {FieldCount}", 
                sessionId, session.CollectedFields.Count);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话失败: {SessionId}", sessionId);
            return null;
        }
    }

    /// <summary>
    /// 保存会话字段
    /// </summary>
    public async Task<bool> SaveSessionFieldAsync(string sessionId, SessionField field)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // 先删除旧值
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = @"
                DELETE FROM fa_session_fields WHERE session_id = @sessionId AND field_name = @fieldName
            ";
            deleteCommand.Parameters.AddWithValue("@sessionId", sessionId);
            deleteCommand.Parameters.AddWithValue("@fieldName", field.Name);
            await deleteCommand.ExecuteNonQueryAsync();

            // 插入新值
            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO fa_session_fields (session_id, field_name, field_value, confidence, created_at)
                VALUES (@sessionId, @fieldName, @fieldValue, @confidence, @createdAt)
            ";
            insertCommand.Parameters.AddWithValue("@sessionId", sessionId);
            insertCommand.Parameters.AddWithValue("@fieldName", field.Name);
            insertCommand.Parameters.AddWithValue("@fieldValue", field.Value);
            insertCommand.Parameters.AddWithValue("@confidence", field.Confidence);
            insertCommand.Parameters.AddWithValue("@createdAt", field.CollectedAt.ToString("o"));

            var rows = await insertCommand.ExecuteNonQueryAsync();
            _logger.LogDebug("保存字段: {SessionId}, {FieldName} = {FieldValue}", 
                sessionId, field.Name, field.Value);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存字段失败: {SessionId}, {FieldName}", sessionId, field.Name);
            return false;
        }
    }

    /// <summary>
    /// 更新会话状态
    /// </summary>
    public async Task<bool> UpdateSessionStatusAsync(string sessionId, string status)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE fa_chat_sessions 
                SET status = @status, updated_at = @updatedAt
                WHERE id = @id
            ";
            command.Parameters.AddWithValue("@id", sessionId);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

            var rows = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("更新会话状态: {SessionId}, 新状态: {Status}", sessionId, status);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新会话状态失败: {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// 删除会话字段（用于"修改字段"快捷指令）
    /// </summary>
    public async Task<bool> DeleteSessionFieldAsync(string sessionId, string fieldName)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM fa_session_fields 
                WHERE session_id = @sessionId AND field_name = @fieldName
            ";
            command.Parameters.AddWithValue("@sessionId", sessionId);
            command.Parameters.AddWithValue("@fieldName", fieldName);

            var rows = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("删除字段: {SessionId}, {FieldName}", sessionId, fieldName);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除字段失败: {SessionId}, {FieldName}", sessionId, fieldName);
            return false;
        }
    }

    /// <summary>
    /// 清空会话所有字段（用于"重新开始"快捷指令）
    /// </summary>
    public async Task<bool> ClearSessionFieldsAsync(string sessionId)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM fa_session_fields WHERE session_id = @sessionId
            ";
            command.Parameters.AddWithValue("@sessionId", sessionId);

            var rows = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("清空会话字段: {SessionId}, 删除 {Rows} 个字段", sessionId, rows);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空会话字段失败: {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// 加载会话字段
    /// </summary>
    private async Task LoadSessionFieldsAsync(SqliteConnection connection, ChatSession session)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT field_name, field_value, confidence, created_at
            FROM fa_session_fields
            WHERE session_id = @sessionId
            ORDER BY created_at
        ";
        command.Parameters.AddWithValue("@sessionId", session.Id);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var field = new SessionField
            {
                Name = reader.GetString(0),
                Value = reader.GetString(1),
                Confidence = reader.GetDouble(2),
                CollectedAt = DateTime.Parse(reader.GetString(3))
            };

            session.CollectedFields[field.Name] = field;
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
