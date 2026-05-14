-- 模板主表（统一前缀：fa_）
CREATE TABLE fa_templates (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    file_name TEXT NOT NULL,
    original_file_name TEXT NOT NULL,
    description TEXT,
    status TEXT NOT NULL DEFAULT 'enabled',
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

-- 字段定义表
CREATE TABLE fa_fields (
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
);

-- 表格定义表
CREATE TABLE fa_tables (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    template_id TEXT NOT NULL,
    name TEXT NOT NULL,
    row_type TEXT NOT NULL DEFAULT 'dynamic',
    max_rows INTEGER NOT NULL DEFAULT 10,
    guide_prompt TEXT,
    FOREIGN KEY (template_id) REFERENCES fa_templates(id) ON DELETE CASCADE
);

-- 表格列定义表
CREATE TABLE fa_table_columns (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    table_id INTEGER NOT NULL,
    name TEXT NOT NULL,
    column_order INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (table_id) REFERENCES fa_tables(id) ON DELETE CASCADE
);

-- 对话会话表
CREATE TABLE fa_chat_sessions (
    id TEXT PRIMARY KEY,
    template_id TEXT NOT NULL,
    user_id TEXT,
    status TEXT NOT NULL DEFAULT 'active',
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (template_id) REFERENCES fa_templates(id) ON DELETE CASCADE
);

-- 会话字段数据表
CREATE TABLE fa_session_fields (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL,
    field_name TEXT NOT NULL,
    field_value TEXT,
    confidence REAL DEFAULT 1.0,
    created_at TEXT NOT NULL,
    FOREIGN KEY (session_id) REFERENCES fa_chat_sessions(id) ON DELETE CASCADE
);
