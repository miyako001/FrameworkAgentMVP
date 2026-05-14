# W1 实现流程 - 基础设施搭建

**周期**：第 1 周  
**里程碑**：M1 - 后端最小闭环可启动  
**目标**：搭建可运行的后端骨架，集成 MAF + Copilot SDK，确保基础设施就绪

**⚠️ 实验性质说明**：
- 本项目采用 **Microsoft Agent Framework (MAF)** 作为核心框架
- 配合 **GitHub Copilot SDK** 作为 LLM 提供者
- MAF 通过 `CopilotClient.AsAIAgent()` 扩展方法无缝集成 Copilot SDK
- 所有数据库表统一使用 **`fa_`** 前缀（frame agent），便于识别和管理

---

## 📋 实施步骤总览

```
步骤1: 创建 .NET 8 后端项目
    ↓
步骤2: 配置 SQLite 数据库（fa_ 前缀）
    ↓
步骤3: 实现本地文件存储
    ↓
步骤4: 集成 Microsoft Agent Framework
    ↓
步骤5: 集成 GitHub Copilot SDK
    ↓
步骤6: 配置 Program.cs 和测试接口
    ↓
步骤7: 搭建前端骨架
    ↓
步骤8: 验收测试
```

---

## 步骤 1：创建 .NET 8 后端项目

### 1.1 创建项目结构

```powershell
# 在项目根目录执行
cd c:\gitrepos\FrameworkAgentMVP
New-Item -Path backend -ItemType Directory -Force
cd backend

# 创建 Web API 项目
dotnet new webapi -n FrameAgentWordFill --framework net8.0
cd FrameAgentWordFill

# 删除默认的 WeatherForecast 文件
Remove-Item WeatherForecast.cs -Force
Remove-Item Controllers\WeatherForecastController.cs -Force
```

### 1.2 安装必要的 NuGet 包

```bash
# SQLite 数据库
dotnet add package Microsoft.Data.Sqlite --version 8.0.8

# Microsoft Agent Framework（实验性质项目核心框架）
dotnet add package Microsoft.Agents.AI --version 1.5.0

# GitHub Copilot SDK
dotnet add package GitHub.Copilot.SDK --version 1.0.0-beta.2

# 日志和配置
dotnet add package Serilog.AspNetCore --version 8.0.0
dotnet add package Serilog.Sinks.Console --version 5.0.0
dotnet add package Serilog.Sinks.File --version 5.0.0
```

### 1.3 调整项目文件

编辑 `FrameAgentWordFill.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>FrameAgentWordFill</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.8" />
    <PackageReference Include="Microsoft.Agents.AI" Version="1.5.0" />
    <PackageReference Include="GitHub.Copilot.SDK" Version="1.0.0-beta.2" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  </ItemGroup>
</Project>
```

---

## 步骤 2：配置 SQLite 数据库

### 2.1 设计数据库结构

**⚠️ 重要**：所有表统一使用 `fa_` 前缀（frame agent），后续所有查询都必须使用带前缀的表名。

创建 `Data/DatabaseSchema.sql`（仅供参考）：

📁 **文件位置**：`backend/FrameAgentWordFill/Data/DatabaseSchema.sql`

```sql
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
    FOREIGN KEY (template_id) REFERENCES fa_templates(id)
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
```

### 2.2 实现数据库初始化类

**注意事项**：
1. 所有表名使用 `fa_` 前缀
2. 外键引用也要使用带前缀的表名
3. 确保事务一致性

创建 `Data/DatabaseInitializer.cs`：

📁 **文件位置**：`backend/FrameAgentWordFill/Data/DatabaseInitializer.cs`

```csharp
using Microsoft.Data.Sqlite;

namespace FrameAgentWordFill.Data;

public sealed class DatabaseInitializer
{
    private readonly string _databasePath;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IConfiguration configuration, ILogger<DatabaseInitializer> logger)
    {
        _logger = logger;
        _databasePath = BuildDatabasePath(configuration);
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

    private static string BuildDatabasePath(IConfiguration configuration)
    {
        var rootPath = configuration["Storage:RootPath"] ?? "..\\..\\storage";
        var fullPath = Path.IsPathRooted(rootPath) 
            ? rootPath 
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rootPath));
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
                FOREIGN KEY (template_id) REFERENCES fa_templates(id)
            );",
            
            @"CREATE TABLE IF NOT EXISTS fa_session_fields (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                field_name TEXT NOT NULL,
                field_value TEXT,
                confidence REAL DEFAULT 1.0,
                created_at TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES fa_chat_sessions(id) ON DELETE CASCADE
            );"
        };
    }
}
```

---

## 步骤 3：实现本地文件存储

### 3.1 创建存储服务

创建 `Services/FileStorageService.cs`：

📁 **文件位置**：`backend/FrameAgentWordFill/Services/FileStorageService.cs`

```csharp
namespace FrameAgentWordFill.Services;

public sealed class FileStorageService
{
    private readonly string _storageRoot;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger)
    {
        _logger = logger;
        var rootPath = configuration["Storage:RootPath"] ?? "..\\..\\storage";
        _storageRoot = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rootPath));
        
        InitializeDirectories();
    }

    private void InitializeDirectories()
    {
        var directories = new[]
        {
            Path.Combine(_storageRoot, "data"),
            Path.Combine(_storageRoot, "templates"),
            Path.Combine(_storageRoot, "output"),
            Path.Combine(_storageRoot, "uploads")
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                _logger.LogInformation("Created storage directory: {Directory}", dir);
            }
        }
    }

    public string GetTemplatesPath() => Path.Combine(_storageRoot, "templates");
    public string GetOutputPath() => Path.Combine(_storageRoot, "output");
    public string GetUploadsPath() => Path.Combine(_storageRoot, "uploads");

    public async Task<string> SaveTemplateAsync(IFormFile file, string fileName)
    {
        var path = Path.Combine(GetTemplatesPath(), fileName);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
        _logger.LogInformation("Saved template file: {FileName}", fileName);
        return path;
    }

    public bool TemplateExists(string fileName)
    {
        var path = Path.Combine(GetTemplatesPath(), fileName);
        return File.Exists(path);
    }
}
```

---

## 步骤 4-5：集成 Microsoft Agent Framework + GitHub Copilot SDK

### 4.1 创建 AIService

**⚠️ 重要说明**：本项目是实验性质，使用 MAF + Copilot SDK 集成。

创建 `Services/AIService.cs`：

📁 **文件位置**：`backend/FrameAgentWordFill/Services/AIService.cs`

```csharp
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;

namespace FrameAgentWordFill.Services;

public sealed class AIService : IAsyncDisposable
{
    private readonly CopilotClient _copilotClient;
    private readonly AIAgent _agent;
    private readonly ILogger<AIService> _logger;

    public AIService(IConfiguration configuration, ILogger<AIService> logger)
    {
        _logger = logger;
        
        // 创建 GitHub Copilot 客户端
        var copilotOptions = new CopilotClientOptions
        {
            AutoStart = true,
            LogLevel = "info"
        };
        
        _copilotClient = new CopilotClient(copilotOptions);
        _copilotClient.StartAsync().GetAwaiter().GetResult();
        
        // 配置会话
        var model = configuration["GitHubCopilot:Model"] ?? "gpt-5";
        var sessionConfig = new SessionConfig
        {
            Model = model,
            Streaming = false,
            OnPermissionRequest = HandlePermissionRequest
        };
        
        // 关键：将 CopilotClient 转换为 MAF 的 AIAgent
        _agent = _copilotClient.AsAIAgent(
            sessionConfig,
            ownsClient: false,
            id: "frameagent-ai",
            name: "FrameAgent AI",
            description: "AI assistant for Word document filling"
        );
        
        _logger.LogInformation("AIService initialized with MAF + Copilot SDK (Model: {Model})", model);
    }

    public async Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            // 使用 MAF 的 AIAgent 接口
            var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
            return response?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AI completion");
            throw;
        }
    }

    public async Task<string> TestConnectionAsync()
    {
        return await GetCompletionAsync("Say 'Hello, FrameAgent!' in Chinese.");
    }
    
    /// <summary>
    /// 获取 Agent 实例（供后续 W4 使用）
    /// </summary>
    public AIAgent GetAgent() => _agent;

    private Task<PermissionRequestResult> HandlePermissionRequest(
        PermissionRequest request,
        PermissionInvocation invocation)
    {
        // W1: 自动批准（开发阶段）
        _logger.LogInformation("Permission requested: {Kind}", request.Kind);
        
        return Task.FromResult(new PermissionRequestResult
        {
            Kind = PermissionRequestResultKind.Approved
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _copilotClient.StopAsync();
    }
}
```

---

## 步骤 6：配置 appsettings.json 和 Program.cs

### 6.1 配置文件

编辑 `appsettings.json`：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  
  "Storage": {
    "RootPath": "..\\..\\storage"
  },
  
  "GitHubCopilot": {
    "Model": "gpt-5"
  }
}
```

创建 `appsettings.Development.json`：

📁 **文件位置**：`backend/FrameAgentWordFill/appsettings.Development.json`

📁 **文件位置**：`backend/FrameAgentWordFill/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

**⚠️ 安全提示**：
- 不要将真实的配置提交到 Git
- 在 `.gitignore` 中添加 `appsettings.*.json`
- 确保已安装 GitHub Copilot CLI 并登录（`copilot /login`）

### 6.2 配置 Program.cs

编辑 `Program.cs`：

```csharp
using FrameAgentWordFill.Data;
using FrameAgentWordFill.Services;
using Serilog;

// 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/frameagent-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 使用 Serilog
    builder.Host.UseSerilog();

    // 添加服务
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // 注册自定义服务
    builder.Services.AddSingleton<DatabaseInitializer>();
    builder.Services.AddSingleton<FileStorageService>();
    builder.Services.AddSingleton<AIService>();

    // CORS（用于前端调用）
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:5173") // Vue 默认端口
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    // 初始化数据库
    var dbInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
    await dbInitializer.InitializeAsync();

    // 验证文件存储目录
    var fileStorage = app.Services.GetRequiredService<FileStorageService>();
    Log.Information("Storage root: {Root}", fileStorage.GetTemplatesPath());

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseAuthorization();

    // ===== 测试接口 =====

    // 健康检查
    app.MapGet("/health", () => Results.Ok(new 
    { 
        status = "ok", 
        timestamp = DateTime.UtcNow 
    }))
    .WithName("HealthCheck")
    .WithOpenApi();

    // LLM 连接测试
    app.MapGet("/test/llm", async (AIService aiService) =>
    {
        try
        {
            var response = await aiService.TestConnectionAsync();
            return Results.Ok(new { success = true, message = response });
        }
        catch (Exception ex)
        {
            return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
        }
    })
    .WithName("TestLLM")
    .WithOpenApi();

    // 数据库连接测试
    app.MapGet("/test/db", (DatabaseInitializer dbInit) =>
    {
        var exists = File.Exists(dbInit.DatabasePath);
        return Results.Ok(new 
        { 
            success = exists, 
            databasePath = dbInit.DatabasePath,
            size = exists ? new FileInfo(dbInit.DatabasePath).Length : 0
        });
    })
    .WithName("TestDatabase")
    .WithOpenApi();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

---

## 步骤 7：搭建前端骨架

### 7.1 创建 Vue3 项目

```powershell
# 回到项目根目录
cd c:\gitrepos\FrameworkAgentMVP

# 使用 Vite 创建 Vue3 项目
npm create vite@latest frontend -- --template vue-ts

cd frontend
npm install

# 安装必要依赖
npm install axios
npm install vue-router
npm install element-plus
npm install @element-plus/icons-vue
```

### 7.2 配置 Vite 代理

编辑 `frontend/vite.config.ts`：

```typescript
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, '')
      }
    }
  }
})
```

### 7.3 创建基础测试页面

创建 `frontend/src/views/Home.vue`：

```vue
<template>
  <div class="home">
    <h1>FrameAgent WordFill - MVP</h1>
    <p>技术栈：Microsoft Agent Framework + GitHub Copilot SDK</p>
    
    <div class="status-panel">
      <h2>系统状态</h2>
      
      <div class="status-item">
        <span>后端健康检查：</span>
        <el-tag :type="healthStatus ? 'success' : 'danger'">
          {{ healthStatus ? '正常' : '异常' }}
        </el-tag>
        <el-button @click="checkHealth" size="small">刷新</el-button>
      </div>
      
      <div class="status-item">
        <span>数据库连接（fa_ 表）：</span>
        <el-tag :type="dbStatus ? 'success' : 'danger'">
          {{ dbStatus ? '正常' : '异常' }}
        </el-tag>
        <el-button @click="checkDatabase" size="small">刷新</el-button>
      </div>
      
      <div class="status-item">
        <span>MAF + Copilot SDK：</span>
        <el-tag :type="llmStatus ? 'success' : 'danger'">
          {{ llmStatus ? '正常' : '异常' }}
        </el-tag>
        <el-button @click="checkLLM" size="small">测试</el-button>
      </div>
      
      <div v-if="llmMessage" class="llm-response">
        <strong>AI 回复：</strong> {{ llmMessage }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import axios from 'axios'
import { ElMessage } from 'element-plus'

const healthStatus = ref(false)
const dbStatus = ref(false)
const llmStatus = ref(false)
const llmMessage = ref('')

const checkHealth = async () => {
  try {
    const response = await axios.get('/api/health')
    healthStatus.value = response.data.status === 'ok'
    ElMessage.success('健康检查通过')
  } catch (error) {
    healthStatus.value = false
    ElMessage.error('健康检查失败')
  }
}

const checkDatabase = async () => {
  try {
    const response = await axios.get('/api/test/db')
    dbStatus.value = response.data.success
    ElMessage.success(`数据库正常 (${response.data.databasePath})`)
  } catch (error) {
    dbStatus.value = false
    ElMessage.error('数据库连接失败')
  }
}

const checkLLM = async () => {
  try {
    const response = await axios.get('/api/test/llm')
    llmStatus.value = response.data.success
    llmMessage.value = response.data.message
    ElMessage.success('MAF + Copilot SDK 正常工作')
  } catch (error: any) {
    llmStatus.value = false
    llmMessage.value = error.response?.data?.error || '连接失败'
    ElMessage.error('AI 服务异常')
  }
}

onMounted(async () => {
  await checkHealth()
  await checkDatabase()
})
</script>

<style scoped>
.home {
  padding: 20px;
}

.status-panel {
  margin-top: 20px;
  padding: 20px;
  border: 1px solid #ddd;
  border-radius: 4px;
}

.status-item {
  margin: 10px 0;
  display: flex;
  align-items: center;
  gap: 10px;
}

.llm-response {
  margin-top: 15px;
  padding: 10px;
  background: #f5f5f5;
  border-radius: 4px;
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

预期输出：
```
info: FrameAgentWordFill.Data.DatabaseInitializer[0]
      SQLite database initialized at C:\gitrepos\FrameworkAgentMVP\storage\data\frameagent.db
info: FrameAgentWordFill.Services.AIService[0]
      AIService initialized with MAF + Copilot SDK (Model: gpt-5)
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### 8.2 测试健康检查

```powershell
curl http://localhost:5000/health
```

预期响应：
```json
{
  "status": "ok",
  "timestamp": "2026-05-14T08:30:00.123Z"
}
```

### 8.3 测试数据库

```powershell
curl http://localhost:5000/test/db
```

预期响应：
```json
{
  "success": true,
  "databasePath": "C:\\gitrepos\\FrameworkAgentMVP\\storage\\data\\frameagent.db",
  "size": 32768
}
```

### 8.4 测试 MAF + Copilot SDK

```powershell
curl http://localhost:5000/test/llm
```

预期响应：
```json
{
  "success": true,
  "message": "你好，FrameAgent！"
}
```

### 8.5 启动前端

```powershell
cd c:\gitrepos\FrameworkAgentMVP\frontend
npm run dev
```

访问 `http://localhost:5173`，应该看到：
- 后端健康检查：✅ 正常
- 数据库连接（fa_ 表）：✅ 正常
- MAF + Copilot SDK：✅ 正常
- AI 回复："你好，FrameAgent！"

---

## ✅ W1 验收清单

- [ ] 后端服务启动成功（`dotnet run`）
- [ ] SQLite 数据库文件已创建（`storage/data/frameagent.db`）
- [ ] **数据库表已创建且都使用 `fa_` 前缀**
- [ ] 存储目录已创建（`storage/templates`、`storage/output`、`storage/uploads`）
- [ ] `/health` 接口返回正常
- [ ] `/test/db` 接口返回数据库路径和大小
- [ ] **Microsoft Agent Framework 已成功初始化**
- [ ] **GitHub Copilot SDK 已成功集成（通过 AsAIAgent）**
- [ ] `/test/llm` 接口返回 AI 回复（验证 MAF + Copilot 工作正常）
- [ ] 前端服务启动成功（`npm run dev`）
- [ ] 前端可以访问后端接口并显示状态

---

## 🔧 常见问题排查

### 问题 1：GitHub Copilot CLI 未安装

**现象**：`CopilotClient` 启动失败

**解决方案**：
```powershell
# Windows
winget install GitHub.Copilot

# 或 npm
npm install -g @github/copilot

# 登录
copilot /login
```

### 问题 2：NuGet 包找不到

**现象**：`Microsoft.Agents.AI` 或 `GitHub.Copilot.SDK` 找不到

**原因**：包在预览阶段或需要特殊源

**解决方案**：
```bash
# 添加预览源
dotnet nuget add source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json

# 或联系项目负责人获取最新包信息
```

### 问题 3：LLM 连接失败

**现象**：`/test/llm` 返回 500 错误

**排查步骤**：
1. 确认 GitHub Copilot CLI 已安装并登录
2. 检查日志文件 `logs/frameagent-*.log`
3. 尝试直接运行 `copilot` 命令测试

**解决方案**：
```powershell
# 测试 Copilot CLI
copilot "Say hello"
```

### 问题 4：数据库表名错误

**现象**：后续查询失败，提示"no such table: templates"

**原因**：忘记使用 `fa_` 前缀

**解决方案**：
- 所有 SQL 查询必须使用带前缀的表名：`fa_templates`、`fa_fields` 等
- 检查所有查询语句，确保表名正确

### 问题 5：数据库无法创建

**现象**：`storage/data` 目录不存在或数据库文件未创建

**排查步骤**：
1. 检查存储路径配置：`appsettings.json` → `Storage:RootPath`
2. 检查目录权限
3. 查看启动日志

**解决方案**：
```powershell
# 手动创建目录
New-Item -Path c:\gitrepos\FrameworkAgentMVP\storage\data -ItemType Directory -Force
```

### 问题 6：前端 CORS 错误

**现象**：浏览器控制台显示 CORS 错误

**解决方案**：
确保 `Program.cs` 中配置了正确的 CORS：
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
```

---

## 📝 下一步（W2）

W1 完成后，可以进入 W2：模板管理与解析。下一步需要：
1. 实现模板上传接口
2. 实现 Word 文档解析
3. 实现占位符提取逻辑
4. 实现管理后台界面

**重要提醒**：所有数据库查询都必须使用 `fa_` 前缀的表名！

---

**文档维护者**：开发团队  
**最后更新**：2026-05-14  
**版本**：V3.0（MAF + Copilot SDK 集成版）


