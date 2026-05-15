using FrameAgentWordFill.Data;
using FrameAgentWordFill.Services;
using FrameAgentWordFill.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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
    builder.Services.AddHttpClient();

    // 注册自定义服务
    builder.Services.AddSingleton<DatabaseInitializer>();
    builder.Services.AddSingleton<FileStorageService>();
    builder.Services.AddSingleton<AIService>();
    
    // W2: 模板管理服务
    builder.Services.AddSingleton<FrameAgentWordFill.Repositories.TemplateRepository>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.TemplatePlaceholderNormalizer>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.TemplateParser>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.TemplateAiParser>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.TemplateParseComparer>();
    builder.Services.AddSingleton<FrameAgentWordFill.Services.TemplateService>();

    // W3: 文档生成服务
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.DocGenerator>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.DataValidator>();
    builder.Services.AddSingleton<FrameAgentWordFill.Services.GenerateService>();

    // W4: AI 对话填充服务
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.AIFieldExtractor>();
    builder.Services.AddSingleton<FrameAgentWordFill.Repositories.ChatSessionRepository>();
    builder.Services.AddSingleton<FrameAgentWordFill.Services.ChatService>();

    // W5: 导入填充服务
    builder.Services.AddSingleton<FrameAgentWordFill.Repositories.ImportSessionRepository>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.FileParser.ExcelParser>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.FileParser.JsonParser>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.FileParser.WordTableParser>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.FileParser.PdfParser>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.FileParser.PlainTextParser>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.FileParser.ImageOcrParser>();
    builder.Services.AddSingleton<FrameAgentWordFill.Services.MultiEngineLLMService>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.AIBatchExtractor>();
    builder.Services.AddSingleton<FrameAgentWordFill.Tools.FieldMatcher>();
    builder.Services.AddSingleton<FrameAgentWordFill.Services.ImportService>();

    // W10: 标准化 LLM 接入层 + Agent 架构
    builder.Services.AddSingleton<IChatClient, FallbackChatClient>();
    builder.Services.AddSingleton<FrameAgentWordFill.Plugins.WordFillPlugin>();
    builder.Services.AddSingleton<FrameAgentWordFill.Plugins.ImportPlugin>();
    builder.Services.AddSingleton<AIAgent>(sp =>
    {
        var chatClient = sp.GetRequiredService<IChatClient>();
        var plugin = sp.GetRequiredService<FrameAgentWordFill.Plugins.WordFillPlugin>();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        return FrameAgentWordFill.Agents.WordFillAgentFactory.Create(chatClient, plugin, loggerFactory);
    });
    builder.Services.AddKeyedSingleton<AIAgent>("import", (sp, _) =>
    {
        var chatClient = sp.GetRequiredService<IChatClient>();
        var plugin = sp.GetRequiredService<FrameAgentWordFill.Plugins.ImportPlugin>();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        return FrameAgentWordFill.Agents.ImportAgentFactory.Create(chatClient, plugin, loggerFactory);
    });

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

    // 查看数据库表结构
    app.MapGet("/test/db/tables", async (DatabaseInitializer dbInit) =>
    {
        try
        {
            var tables = await DbInspector.GetTablesAsync(dbInit.DatabasePath);
            var tableDetails = new Dictionary<string, object>();
            
            foreach (var table in tables)
            {
                tableDetails[table] = await DbInspector.GetTableInfo(dbInit.DatabasePath, table);
            }
            
            return Results.Ok(new
            {
                success = true,
                databasePath = dbInit.DatabasePath,
                tables = tableDetails
            });
        }
        catch (Exception ex)
        {
            return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
        }
    })
    .WithName("TestDatabaseTables")
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
