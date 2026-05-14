# Microsoft Agent Framework 技术研究

**文档版本**：V2.0  
**撰写日期**：2026年05月14日  
**研究目的**：为 FrameworkAgentMVP 项目提供 Microsoft Agent Framework 的完整技术参考

**⚠️ 项目技术架构**：
- **编排层**：Microsoft Agent Framework（MAF）- Agent 编排、工作流管理
- **LLM 层**：GitHub Copilot SDK - 通过 `CopilotClient.AsAIAgent()` 集成
- **关键集成**：MAF 官方支持 GitHub Copilot SDK，无缝集成

---

## 📋 执行摘要

**关键发现**：
- Microsoft Agent Framework (MAF) 于 **2026年3月18日发布 1.0 正式版**
- MAF 是 **AutoGen** 和 **Semantic Kernel** 的官方合并产物
- 提供 **.NET** 和 **Python** 的完整支持，API 一致
- 专为**生产环境**设计，具备企业级特性
- **AutoGen 已进入维护模式**，新项目应使用 MAF
- **官方支持 GitHub Copilot SDK**：通过 `CopilotClient.AsAIAgent()` 扩展方法

**本项目使用场景**：
- **W1-W8 全程**：使用 MAF 进行 Agent 编排和工作流管理
- **LLM 提供者**：使用 GitHub Copilot SDK，无需 Azure OpenAI API Key
- **关键功能**：
  - W1-W3：基础 Agent 功能
  - W4：多轮对话工作流（MAF Workflow）
  - W5-W6：工具调用（MAF Tools）
  - W7：批量处理工作流

**核心包名（.NET）**：
- `Microsoft.Agents.AI` - 核心 Agent 功能
- `Microsoft.Agents.AI.Foundry` - Azure AI Foundry 集成
- `Azure.AI.Projects` - Foundry 项目客户端

**GitHub 仓库**：https://github.com/microsoft/agent-framework

---

## 1. 什么是 Microsoft Agent Framework？

### 1.1 官方定义

> Microsoft Agent Framework (MAF) is an open, multi-language framework for building production-grade AI agents and multi-agent workflows in .NET and Python.

MAF 是一个开源、多语言的框架，用于构建生产级 AI Agent 和多 Agent 工作流。

### 1.2 历史背景

| 时间 | 事件 |
|------|------|
| 2023-2024 | AutoGen 和 Semantic Kernel 分别发展 |
| 2025年10月 | 微软宣布合并两个项目，推出 MAF |
| 2026年3月18日 | MAF 1.0 正式版发布 |
| 2026年3月 | AutoGen 进入维护模式，不再接受新功能 |

### 1.3 与其他框架的关系

```
AutoGen (维护模式) ───┐
                      ├─→ Microsoft Agent Framework 1.0
Semantic Kernel ──────┘
```

- **AutoGen**：专注于多 Agent 协作和实验性功能，现已进入维护模式
- **Semantic Kernel**：专注于 LLM 集成和插件系统，部分功能迁移到 MAF
- **MAF**：结合两者优势，提供企业级、生产就绪的 Agent 框架

---

## 2. 核心特性

### 2.1 多语言支持

- **.NET (C#)**：原生支持，优先级最高
- **Python**：完整支持，API 与 .NET 保持一致

### 2.2 企业级特性

| 特性 | 说明 |
|------|------|
| **稳定 API** | 1.0 版本承诺长期支持（LTS） |
| **可观测性** | 内置 OpenTelemetry 集成 |
| **持久化** | 支持会话状态保存和恢复 |
| **人机协同** | 支持 Human-in-the-Loop 模式 |
| **工作流编排** | 图形化工作流，支持顺序、并发、切换、分组 |
| **多提供商** | 支持 Azure OpenAI、OpenAI、Foundry 等 |

### 2.3 编排模式

MAF 支持多种 Agent 编排模式：

1. **Sequential（顺序）**：Agent 按顺序执行
2. **Concurrent（并发）**：多个 Agent 并行执行
3. **Handoff（切换）**：Agent 之间交接任务
4. **Group Collaboration（分组协作）**：多个 Agent 协作完成任务

### 2.4 工具与技能

- **Tools**：Agent 可调用的函数
- **Skills**：领域特定的知识库，可从文件、代码、类库加载
- **Middleware**：请求/响应处理管道

---

## 3. .NET 快速入门

### 3.1 安装

```bash
# 核心包
dotnet add package Microsoft.Agents.AI

# Foundry 集成（推荐）
dotnet add package Microsoft.Agents.AI.Foundry
dotnet add package Azure.AI.Projects
dotnet add package Azure.Identity
```

### 3.2 最简示例（使用 Azure AI Foundry）

```csharp
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;

// 配置
string endpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")!;
string deploymentName = "gpt-4";

// 创建 Agent
AIAgent agent = new AIProjectClient(
        new Uri(endpoint), 
        new DefaultAzureCredential()
    )
    .AsAIAgent(
        model: deploymentName,
        instructions: "You are an upbeat assistant that writes beautifully.",
        name: "HaikuAgent"
    );

// 运行 Agent
string response = await agent.RunAsync("Write a haiku about Microsoft Agent Framework.");
Console.WriteLine(response);
```

### 3.3 核心类和接口

| 类/接口 | 说明 |
|---------|------|
| `AIAgent` | Agent 的核心类 |
| `AIProjectClient` | Azure AI Foundry 项目客户端 |
| `DefaultAzureCredential` | Azure 身份验证（支持 Managed Identity、CLI、环境变量等） |
| `AsAIAgent()` | 将项目客户端转换为 Agent |
| `RunAsync()` | 执行 Agent 任务 |

### 3.4 不使用 Foundry 的方式（直接使用 Azure OpenAI）

```csharp
using Microsoft.Agents.AI;
using Azure.AI.OpenAI;
using Azure;

// 创建 OpenAI 客户端
var openAIClient = new OpenAIClient(
    new Uri("https://your-resource.openai.azure.com"),
    new AzureKeyCredential("your-api-key")
);

// 创建 Agent
var agent = new AIAgent(
    name: "MyAgent",
    instructions: "You are a helpful assistant.",
    model: "gpt-4",
    client: openAIClient
);

// 运行
var response = await agent.RunAsync("Hello!");
```

---

## 4. Agent 进阶用法

### 4.1 添加 Tools（函数调用）

```csharp
using Microsoft.Agents.AI;

public class WeatherTools
{
    [Tool(Description = "Get the current weather for a location")]
    public static string GetWeather(string location)
    {
        return $"The weather in {location} is sunny, 22°C";
    }
}

var agent = new AIAgent(
    name: "WeatherAgent",
    instructions: "You are a weather assistant.",
    model: "gpt-4",
    client: openAIClient,
    tools: new[] { typeof(WeatherTools) }
);

var response = await agent.RunAsync("What's the weather in Seattle?");
// Agent 会自动调用 GetWeather("Seattle") 函数
```

### 4.2 多轮对话（带状态）

```csharp
// 创建会话
var conversation = agent.CreateConversation();

// 第一轮
await conversation.SendAsync("My name is Alice");
// Agent: "Nice to meet you, Alice!"

// 第二轮（Agent 记住上下文）
await conversation.SendAsync("What's my name?");
// Agent: "Your name is Alice."
```

### 4.3 中间件（Middleware）

```csharp
public class LoggingMiddleware : IAgentMiddleware
{
    public async Task OnRequestAsync(AgentRequest request)
    {
        Console.WriteLine($"Request: {request.Message}");
    }

    public async Task OnResponseAsync(AgentResponse response)
    {
        Console.WriteLine($"Response: {response.Message}");
    }
}

var agent = new AIAgent(
    name: "MyAgent",
    instructions: "You are helpful.",
    model: "gpt-4",
    client: openAIClient,
    middleware: new[] { new LoggingMiddleware() }
);
```

---

## 5. 工作流（Workflows）

### 5.1 什么是工作流？

工作流是多个 Agent 的有向图，定义了 Agent 之间的执行顺序和数据流。

### 5.2 简单工作流示例

```csharp
using Microsoft.Agents.AI.Workflows;

// 定义 Agent
var researchAgent = new AIAgent(/*...*/);
var writerAgent = new AIAgent(/*...*/);

// 创建工作流
var workflow = new WorkflowBuilder()
    .AddNode("research", researchAgent)
    .AddNode("write", writerAgent)
    .AddEdge("research", "write")  // research -> write
    .Build();

// 执行工作流
var result = await workflow.RunAsync("Write a blog post about AI agents");
```

### 5.3 条件分支

```csharp
var workflow = new WorkflowBuilder()
    .AddNode("triage", triageAgent)
    .AddNode("technical", technicalAgent)
    .AddNode("sales", salesAgent)
    .AddConditionalEdge("triage", (context) => {
        if (context.Result.Contains("technical"))
            return "technical";
        else
            return "sales";
    })
    .Build();
```

---

## 6. 部署与托管

### 6.1 本地开发

```bash
# 使用 Azure CLI 登录
az login

# 设置环境变量
export AZURE_AI_PROJECT_ENDPOINT="https://..."
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4"

# 运行应用
dotnet run
```

### 6.2 Azure Foundry Hosted Agents

MAF 提供一键部署到 Azure Foundry 的能力：

```csharp
// 只需添加 2 行代码
agent.EnableFoundryHosting();  // 启用 Foundry 托管
await agent.DeployAsync();     // 部署到云端
```

托管后的优势：
- 自动扩展
- 内置身份验证
- 托管会话状态
- 可观测性集成
- 版本管理

### 6.3 Azure Functions

```csharp
[Function("RunAgent")]
public async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
    FunctionContext context)
{
    var agent = new AIAgent(/*...*/);
    string message = await new StreamReader(req.Body).ReadToEndAsync();
    string response = await agent.RunAsync(message);
    return new OkObjectResult(response);
}
```

---

## 7. 可观测性与监控

### 7.1 OpenTelemetry 集成

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Microsoft.Agents.AI")
    .AddConsoleExporter()
    .AddAzureMonitorTraceExporter(options =>
    {
        options.ConnectionString = "InstrumentationKey=...";
    })
    .Build();

// Agent 自动发送遥测数据
var agent = new AIAgent(/*...*/);
await agent.RunAsync("Hello!");
```

### 7.2 监控指标

MAF 自动跟踪：
- Agent 执行时间
- LLM 调用次数和延迟
- Tool 调用成功率
- 错误率和异常
- Token 使用量

### 7.3 DevUI（开发者界面）

MAF 提供交互式 UI 用于：
- 实时查看 Agent 对话
- 调试工作流
- 查看遥测数据
- 测试 Agent 行为

```bash
# 启动 DevUI
dotnet run --project devui
# 访问 http://localhost:5000
```

---

## 8. 与 GitHub Copilot SDK 集成（本项目使用）

### 8.1 为什么选择 GitHub Copilot SDK？

**优势**：
- ✅ 无需 Azure OpenAI API Key
- ✅ 使用 GitHub 身份认证（`copilot /login`）
- ✅ 支持多种模型（GPT-5、Claude、Codex）
- ✅ MAF 官方支持，提供 `AsAIAgent()` 扩展方法
- ✅ 自动管理 Copilot CLI，无需手动调用 Process

### 8.2 集成步骤

#### 1. 安装包

```bash
# MAF 核心包
dotnet add package Microsoft.Agents.AI --version 1.5.0

# GitHub Copilot SDK
dotnet add package GitHub.Copilot.SDK --version 1.0.0-beta.2
```

#### 2. 安装 Copilot CLI

```bash
# Windows
winget install GitHub.Copilot

# 或 npm
npm install -g @github/copilot

# 登录
copilot /login
```

#### 3. 创建 AIService

```csharp
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;

public class AIService : IAsyncDisposable
{
    private readonly CopilotClient _copilotClient;
    private readonly AIAgent _agent;

    public AIService(ILogger<AIService> logger)
    {
        // 创建 Copilot 客户端
        _copilotClient = new CopilotClient(new CopilotClientOptions
        {
            AutoStart = true,
            LogLevel = "info"
        });
        await _copilotClient.StartAsync();

        // 创建会话配置
        var sessionConfig = new SessionConfig
        {
            Model = "gpt-5", // 或 "claude-sonnet-4.5"
            Streaming = false,
            OnPermissionRequest = (request, invocation) =>
            {
                // 权限处理逻辑
                return Task.FromResult(new PermissionRequestResult
                {
                    Kind = PermissionRequestResultKind.Approved
                });
            }
        };

        // 关键：将 CopilotClient 转换为 AIAgent
        _agent = _copilotClient.AsAIAgent(
            sessionConfig,
            ownsClient: false,
            id: "my-agent",
            name: "My Agent",
            description: "A helpful AI assistant"
        );
    }

    // 使用标准的 MAF AIAgent 接口
    public async Task<string> GetCompletionAsync(string prompt)
    {
        var response = await _agent.RunAsync(prompt);
        return response.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        await _copilotClient.StopAsync();
    }
}
```

### 8.3 关键 API

| API | 说明 |
|-----|------|
| `CopilotClient` | GitHub Copilot SDK 的核心客户端 |
| `SessionConfig` | 会话配置（模型、流式、权限） |
| `AsAIAgent()` | 扩展方法，将 CopilotClient 转换为 MAF 的 AIAgent |
| `OnPermissionRequest` | 权限处理器，Copilot 执行工具前需要确认 |
| `Model` | 选择模型：`"gpt-5"`, `"claude-sonnet-4.5"`, `"gpt-5-4-mini"` |

### 8.4 模型选择

```csharp
// GPT-5（默认）
Model = "gpt-5"

// Claude Sonnet 4.5（更强推理）
Model = "claude-sonnet-4.5"

// GPT-5-4-mini（轻量级）
Model = "gpt-5-4-mini"

// Codex（代码生成）
Model = "codex"
```

### 8.5 流式响应

```csharp
// 启用流式
var sessionConfig = new SessionConfig
{
    Streaming = true,
    // ...
};

// 使用流式 API
await foreach (var update in _agent.RunStreamingAsync(prompt))
{
    Console.Write(update.Text);
}
```

### 8.6 权限管理

```csharp
OnPermissionRequest = (request, invocation) =>
{
    // request.Kind 可能的值：
    // - "shell": 执行 shell 命令
    // - "write": 写入文件
    // - "read": 读取文件
    // - "mcp": MCP 工具调用
    // - "custom-tool": 自定义工具
    
    // 自动批准（开发阶段）
    return Task.FromResult(new PermissionRequestResult
    {
        Kind = PermissionRequestResultKind.Approved
    });
    
    // 或交互式确认（生产环境）
    Console.WriteLine($"Permission requested: {request.Kind}");
    Console.Write("Approve? (y/n): ");
    var input = Console.ReadLine()?.ToLower();
    
    return Task.FromResult(new PermissionRequestResult
    {
        Kind = input == "y"
            ? PermissionRequestResultKind.Approved
            : PermissionRequestResultKind.Rejected
    });
}
```

### 8.7 完整集成示例（本项目使用）

详见 `02_ref_w1_实现流程.md` 中的 `AIService.cs` 完整实现。

**关键点**：
1. `CopilotClient` 管理 Copilot CLI
2. `SessionConfig` 配置模型和权限
3. `AsAIAgent()` 转换为 MAF 标准接口
4. 所有 MAF 功能（Tools、Workflow、Middleware）都可用

---

## 9. 与本项目的集成建议（已实施）

### 9.1 推荐架构

```
┌─────────────────────────────────────────┐
│  前端 (Vue3)                            │
│  - 用户对话界面                         │
│  - 模板管理界面                         │
└────────────────┬────────────────────────┘
                 │ HTTP / SSE
┌────────────────▼────────────────────────┐
│  后端 (ASP.NET Core)                    │
│  ┌─────────────────────────────────────┐│
│  │ Microsoft Agent Framework           ││
│  │ ┌─────────────────────────────────┐ ││
│  │ │ WordFillAgent (主 Agent)        │ ││
│  │ │ - 对话理解                      │ ││
│  │ │ - 字段提取                      │ ││
│  │ │ - 文档生成调度                  │ ││
│  │ └─────────────────────────────────┘ ││
│  │                                      ││
│  │ Tools:                               ││
│  │ - TemplateParser                     ││
│  │ - FieldExtractor                     ││
│  │ - DocGenerator                       ││
│  │ - FileHandler                        ││
│  └─────────────────────────────────────┘││
│                                          │
│  GitHub Copilot SDK集成层              │
│  ┌─────────────────────────────────────┐│
│  │ CopilotClient.AsAIAgent()           ││
│  │ - 自动管理 Copilot CLI              ││
│  │ - 会话管理                          ││
│  │ - 权限控制                          ││
│  └─────────────────────────────────────┘│
└────────────────┬────────────────────────┘
                 │
┌────────────────▼────────────────────────┐
│  GitHub Copilot CLI                     │
│  - gpt-5 / claude-sonnet-4.5            │
└──────────────────────────────────────────┘
```

### 9.2 完整实现请参考 02_ref_w1_实现流程.md

详见 `02_ref_w1_实现流程.md` 中的完整 `AIService.cs` 实现。

**关键代码结构**：
```csharp
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;

public class AIService : IAsyncDisposable
{
    private readonly CopilotClient _copilotClient;
    private readonly AIAgent _agent;

    public AIService(...)
    {
        // 1. 创建 Copilot 客户端
        _copilotClient = new CopilotClient(new CopilotClientOptions { ... });
        await _copilotClient.StartAsync();

        // 2. 配置会话
        var sessionConfig = new SessionConfig
        {
            Model = "gpt-5",
            OnPermissionRequest = HandlePermissionRequest
        };

        // 3. 转换为 AIAgent（关键）
        _agent = _copilotClient.AsAIAgent(sessionConfig, ...);
    }

    // 4. 使用 MAF 标准接口
    public async Task<string> GetCompletionAsync(string prompt)
    {
        var response = await _agent.RunAsync(prompt);
        return response.ToString();
    }
}
```

---

## 10. 常见问题（FAQ）

### Q1: MAF 与 Semantic Kernel 有什么区别？

**A**: 
- **Semantic Kernel** 仍然存在，专注于 LLM 集成和插件系统
- **MAF** 是更高层的框架，内部可以使用 Semantic Kernel
- 如果只需要简单的 LLM 调用，用 Semantic Kernel
- 如果需要复杂的 Agent 编排和工作流，用 MAF

### Q2: 必须使用 Azure Foundry 吗？

**A**: 不是必须的。MAF 支持：
- Azure OpenAI（推荐）
- OpenAI API
- GitHub Copilot SDK
- 其他兼容的 LLM 提供商

### Q3: 如何处理长时间运行的任务？

**A**: MAF 支持后台响应（Background Responses）：

```csharp
var task = agent.RunAsyncInBackground("Long task...");
string taskId = task.Id;

// 稍后检查状态
var status = await agent.GetTaskStatusAsync(taskId);
if (status.IsCompleted)
{
    string result = await task.GetResultAsync();
}
```

### Q4: 如何限制 Agent 的行为？

**A**: 使用 Filters 和 Middleware：

```csharp
public class ContentFilter : IAgentFilter
{
    public async Task<bool> OnBeforeToolCallAsync(ToolCall tool)
    {
        // 阻止危险操作
        if (tool.Name == "DeleteFile")
            return false;
        return true;
    }
}
```

### Q5: 如何处理敏感数据？

**A**: 
1. 使用 Azure Key Vault 存储密钥
2. 启用 PII（个人身份信息）检测和脱敏
3. 使用私有 Foundry 部署
4. 启用审计日志

---

## 11. 最佳实践

### 11.1 Agent 设计

✅ **好的做法**：
- 给 Agent 明确、具体的指令
- 使用 System Message 定义 Agent 角色
- 分解复杂任务为多个专门的 Agent
- 使用 Tools 而不是让 LLM 生成代码

❌ **避免**：
- 模糊的指令
- 一个 Agent 做所有事情
- 过度依赖 LLM 的推理能力
- 没有错误处理

### 11.2 提示词工程

```csharp
// 好的提示词
var goodInstructions = @"
You are a Word document assistant.
Your role is to help users fill in Word templates.

Steps:
1. Ask for the template name
2. Identify required fields
3. Collect field values through conversation
4. Validate input
5. Generate document

Always be polite and clear.
";

// 不好的提示词
var badInstructions = "Help with Word docs";
```

### 11.3 错误处理

```csharp
try
{
    var response = await agent.RunAsync(message);
}
catch (AgentTimeoutException ex)
{
    // Agent 超时
    logger.LogWarning("Agent timeout: {Error}", ex.Message);
}
catch (LLMQuotaExceededException ex)
{
    // LLM 配额用尽
    logger.LogError("LLM quota exceeded");
}
catch (AgentException ex)
{
    // 通用 Agent 错误
    logger.LogError("Agent error: {Error}", ex.Message);
}
```

### 11.4 性能优化

1. **缓存常见响应**
2. **批量处理请求**
3. **使用流式响应**提升用户体验
4. **限制对话历史长度**以减少 Token 消耗
5. **选择合适的模型**（gpt-4 vs gpt-3.5）

---

## 12. 参考资源

### 12.1 官方资源

- **文档**：https://learn.microsoft.com/en-us/agent-framework/
- **GitHub**：https://github.com/microsoft/agent-framework
- **博客**：https://devblogs.microsoft.com/agent-framework/
- **Discord**：https://discord.gg/b5zjErwbQM

### 12.2 示例代码

- **Python 示例**：https://github.com/microsoft/agent-framework/tree/main/python/samples
- **.NET 示例**：https://github.com/microsoft/agent-framework/tree/main/dotnet/samples

### 12.3 迁移指南

- **从 AutoGen 迁移**：https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-autogen/
- **从 Semantic Kernel 迁移**：https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-semantic-kernel/

---

## 13. 版本历史

| 版本 | 发布日期 | 主要变化 |
|------|----------|----------|
| 1.5.0 | 2026年5月 | DevUI 增强、A2A v1 支持 |
| 1.0.0 | 2026年3月18日 | 正式版发布，API 稳定 |
| 0.7.x | 2025年12月 | 预览版，Foundry 集成 |

---

## 附录 A：完整代码示例

### A.1 完整的 Agent 服务

```csharp
using Microsoft.Agents.AI;
using Azure.AI.Projects;
using Azure.Identity;

namespace FrameAgentWordFill.Services;

public class AgentService : IDisposable
{
    private readonly AIAgent _agent;
    private readonly ILogger<AgentService> _logger;
    private readonly Dictionary<string, ConversationContext> _conversations = new();

    public AgentService(
        IConfiguration configuration,
        ILogger<AgentService> logger)
    {
        _logger = logger;

        // 从配置读取
        var endpoint = configuration["AzureAI:ProjectEndpoint"]
            ?? throw new InvalidOperationException("AzureAI:ProjectEndpoint is required");
        var model = configuration["AzureAI:ModelDeploymentName"] ?? "gpt-4";

        // 创建 Agent
        var projectClient = new AIProjectClient(
            new Uri(endpoint),
            new DefaultAzureCredential()
        );

        _agent = projectClient.AsAIAgent(
            model: model,
            name: "WordFillAgent",
            instructions: LoadInstructions()
        );

        _logger.LogInformation(
            "Agent initialized with model {Model}",
            model
        );
    }

    public async Task<string> ChatAsync(
        string sessionId,
        string message,
        CancellationToken cancellationToken = default)
    {
        // 获取或创建会话
        if (!_conversations.TryGetValue(sessionId, out var conversation))
        {
            conversation = _agent.CreateConversation();
            _conversations[sessionId] = conversation;
            _logger.LogInformation("Created new conversation: {SessionId}", sessionId);
        }

        // 发送消息
        var response = await conversation.SendAsync(message, cancellationToken);

        return response.Message;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string sessionId,
        string message)
    {
        if (!_conversations.TryGetValue(sessionId, out var conversation))
        {
            conversation = _agent.CreateConversation();
            _conversations[sessionId] = conversation;
        }

        await foreach (var chunk in conversation.SendStreamAsync(message))
        {
            yield return chunk.Content;
        }
    }

    private string LoadInstructions()
    {
        return @"
你是一个 Word 文档填充助手。

你的职责是：
1. 通过自然对话理解用户需求
2. 从用户的回答中智能提取字段值
3. 对缺失或不清楚的字段进行追问
4. 在所有字段收集完成后生成文档

对话风格：
- 友好、耐心、专业
- 使用简洁明了的语言
- 一次只问一个或相关的几个问题
- 对用户的输入给予确认

字段提取：
- 从自然语言中提取结构化数据
- 处理各种表达方式（同义词、缩写等）
- 验证数据格式（日期、电话、邮箱等）
";
    }

    public void Dispose()
    {
        foreach (var conversation in _conversations.Values)
        {
            conversation?.Dispose();
        }
        _conversations.Clear();
    }
}
```

---

**文档维护者**：FrameworkAgentMVP 项目组  
**最后更新**：2026-05-14  
**许可证**：MIT License

---

## 结论

Microsoft Agent Framework 为构建生产级 AI Agent 提供了完整、稳定、企业就绪的解决方案。其 1.0 版本的发布标志着微软在 Agent 框架领域的正式承诺。

**对于 FrameworkAgentMVP 项目**：
- **架构选择**：MAF（编排）+ GitHub Copilot SDK（LLM）
- **关键集成**：`CopilotClient.AsAIAgent()` 扩展方法
- **优势**：
  - ✅ 满足需求中的"编排流程"要求
  - ✅ 无需 Azure OpenAI API Key
  - ✅ MAF 的所有高级功能（工具、工作流、中间件）都可用
  - ✅ 企业级特性（可观测性、审计、状态管理）

**技术架构**：
```
前端 (Vue3)
    ↓
后端 (ASP.NET Core)
    ↓
Microsoft Agent Framework（编排层）
├─ Agent 管理
├─ Workflow 编排
├─ Tools 调用
├─ Middleware 处理
    ↓
GitHub Copilot SDK（LLM 层）
├─ CopilotClient.AsAIAgent()
├─ 自动管理 Copilot CLI
    ↓
GitHub Copilot CLI（底层执行）
```


