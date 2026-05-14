using Microsoft.Data.Sqlite;

namespace FrameAgentWordFill.Data;

public sealed class DatabaseInitializer
{
    private readonly string _databasePath;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IConfiguration configuration, IHostEnvironment hostEnvironment, ILogger<DatabaseInitializer> logger)
    {
        _logger = logger;
        _databasePath = BuildDatabasePath(configuration, hostEnvironment);
    }

    public async Task InitializeAsync()
    {
        // 确保目录存在
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        // 执行建表语句
        var commands = GetCreateTableCommands();
        foreach (var commandText in commands)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("SQLite database initialized at {DatabasePath}", _databasePath);
    }

    public string DatabasePath => _databasePath;

    private static string BuildDatabasePath(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        var rootPath = configuration["Storage:RootPath"] ?? "..\\..\\storage";
        var fullPath = Path.IsPathRooted(rootPath) 
            ? rootPath 
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, rootPath));
        return Path.Combine(fullPath, "data", "frameagent.db");
    }

    private static string[] GetCreateTableCommands()
    {
        return new[]
        {
            @"CREATE TABLE IF NOT EXISTS fa_templates (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                file_name TEXT NOT NULL,
                original_file_name TEXT NOT NULL,
                description TEXT,
                status TEXT NOT NULL DEFAULT 'enabled',
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );",
            
            @"CREATE TABLE IF NOT EXISTS fa_fields (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                template_id TEXT NOT NULL,
                name TEXT NOT NULL,
                field_type TEXT NOT NULL DEFAULT 'text',
                required INTEGER NOT NULL DEFAULT 0,
                field_order INTEGER NOT NULL DEFAULT 0,
                guide_prompt TEXT,
                missing_prompt TEXT,
                invalid_prompt TEXT,
                FOREIGN KEY (template_id) REFERENCES fa_templates(id) ON DELETE CASCADE
            );",
            
            @"CREATE TABLE IF NOT EXISTS fa_tables (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                template_id TEXT NOT NULL,
                name TEXT NOT NULL,
                row_type TEXT NOT NULL DEFAULT 'dynamic',
                max_rows INTEGER NOT NULL DEFAULT 10,
                guide_prompt TEXT,
                FOREIGN KEY (template_id) REFERENCES fa_templates(id) ON DELETE CASCADE
            );",
            
            @"CREATE TABLE IF NOT EXISTS fa_table_columns (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                table_id INTEGER NOT NULL,
                name TEXT NOT NULL,
                column_order INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (table_id) REFERENCES fa_tables(id) ON DELETE CASCADE
            );",
            
            @"CREATE TABLE IF NOT EXISTS fa_chat_sessions (
                id TEXT PRIMARY KEY,
                template_id TEXT NOT NULL,
                user_id TEXT,
                status TEXT NOT NULL DEFAULT 'active',
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY (template_id) REFERENCES fa_templates(id) ON DELETE CASCADE
            );",
            
            @"CREATE TABLE IF NOT EXISTS fa_session_fields (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                field_name TEXT NOT NULL,
                field_value TEXT,
                confidence REAL DEFAULT 1.0,
                created_at TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES fa_chat_sessions(id) ON DELETE CASCADE
            );",

            // W5: 导入会话表
            @"CREATE TABLE IF NOT EXISTS fa_import_sessions (
                session_id INTEGER PRIMARY KEY AUTOINCREMENT,
                template_id TEXT NOT NULL,
                file_type TEXT NOT NULL,
                file_path TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'Parsing',
                matched_field_count INTEGER NOT NULL DEFAULT 0,
                unmatched_field_count INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now')),
                error_message TEXT,
                FOREIGN KEY (template_id) REFERENCES fa_templates(id) ON DELETE CASCADE
            );",

            // W5: 导入字段映射表
            @"CREATE TABLE IF NOT EXISTS fa_import_field_mappings (
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
            );",

            // W5: 导入表格数据表
            @"CREATE TABLE IF NOT EXISTS fa_import_table_data (
                data_id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL,
                table_name TEXT NOT NULL,
                row_index INTEGER NOT NULL,
                column_name TEXT NOT NULL,
                cell_value TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (session_id) REFERENCES fa_import_sessions(session_id) ON DELETE CASCADE
            );",

            // W5: 索引
            "CREATE INDEX IF NOT EXISTS idx_import_sessions_template_id ON fa_import_sessions(template_id);",
            "CREATE INDEX IF NOT EXISTS idx_import_sessions_status ON fa_import_sessions(status);",
            "CREATE INDEX IF NOT EXISTS idx_import_field_mappings_session_id ON fa_import_field_mappings(session_id);",
            "CREATE INDEX IF NOT EXISTS idx_import_table_data_session_id ON fa_import_table_data(session_id);"
        };
    }
}
