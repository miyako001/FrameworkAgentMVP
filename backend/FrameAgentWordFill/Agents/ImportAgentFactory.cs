using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using FrameAgentWordFill.Plugins;

namespace FrameAgentWordFill.Agents;

/// <summary>
/// 文件导入 Agent 工厂。
/// </summary>
public static class ImportAgentFactory
{
    public static ChatClientAgent Create(
        IChatClient chatClient,
        ImportPlugin plugin,
        ILoggerFactory? loggerFactory = null)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                plugin.BatchExtractFieldsAsync,
                new AIFunctionFactoryOptions
                {
                    Name = "BatchExtractFields",
                    Description = "从文档内容中批量提取所有模板字段的值"
                }),
            AIFunctionFactory.Create(
                plugin.MatchExtractedFieldsAsync,
                new AIFunctionFactoryOptions
                {
                    Name = "MatchFields",
                    Description = "对提取结果与模板字段做语义匹配，处理别名和同义词"
                }),
        };

        // 包装 chatClient，启用自动工具调用
        var functionInvokingClient = chatClient
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        // 构造函数: (IChatClient, id, name, instructions, tools, loggerFactory, serviceProvider)
        var agent = new ChatClientAgent(
            functionInvokingClient,
            null,
            "ImportAgent",
            SystemPrompt,
            tools,
            loggerFactory,
            null);

        return agent;
    }

    private const string SystemPrompt = """
        你是一个专业的文档字段提取助手。
        
        你的职责：
        1. 接收用户上传的文档内容（Excel/JSON/Word/PDF/图片等）
        2. 调用 BatchExtractFields 工具，从文档中批量提取模板所需字段
        3. 调用 MatchFields 工具，将提取结果与模板字段做语义匹配
        4. 对低置信度字段（confidence < 60）标记为"需人工确认"
        5. 返回结构化的字段提取报告
        
        规则：
        - 提取时优先使用语义理解，其次考虑精确匹配
        - 无法提取的字段输出 null，并说明原因
        - 不得凭空捏造字段值，不确定时置信度打低分
        - 保持返回格式结构化，方便前端展示
        """;
}
