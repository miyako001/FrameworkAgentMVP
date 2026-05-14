using Microsoft.Data.Sqlite;

namespace FrameAgentWordFill.Tools;

public static class DbInspector
{
    public static async Task<List<string>> GetTablesAsync(string databasePath)
    {
        var tables = new List<string>();
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
        
        return tables;
    }
    
    public static async Task<Dictionary<string, object>> GetTableInfo(string databasePath, string tableName)
    {
        var info = new Dictionary<string, object>();
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        
        // 获取表结构
        await using var schemaCmd = connection.CreateCommand();
        schemaCmd.CommandText = $"PRAGMA table_info({tableName})";
        var columns = new List<Dictionary<string, object>>();
        
        await using var schemaReader = await schemaCmd.ExecuteReaderAsync();
        while (await schemaReader.ReadAsync())
        {
            columns.Add(new Dictionary<string, object>
            {
                ["cid"] = schemaReader.GetInt32(0),
                ["name"] = schemaReader.GetString(1),
                ["type"] = schemaReader.GetString(2),
                ["notnull"] = schemaReader.GetInt32(3),
                ["pk"] = schemaReader.GetInt32(5)
            });
        }
        
        // 获取行数
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
        var count = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);
        
        info["columns"] = columns;
        info["rowCount"] = count;
        
        return info;
    }
}
