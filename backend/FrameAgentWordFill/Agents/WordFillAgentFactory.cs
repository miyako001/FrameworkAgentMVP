using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using FrameAgentWordFill.Plugins;

namespace FrameAgentWordFill.Agents;

/// <summary>
/// 对话填充 Agent 工厂。
/// </summary>
public static class WordFillAgentFactory
{
    public static ChatClientAgent Create(
        IChatClient chatClient,
        WordFillPlugin plugin,
        ILoggerFactory? loggerFactory = null)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                plugin.ExtractFieldsFromMessageAsync,
                new AIFunctionFactoryOptions
                {
                    Name = "ExtractFields",
                    Description = "从用户自然语言消息中提取字段值"
                }),
            AIFunctionFactory.Create(
                plugin.DetectShortcutCommandAsync,
                new AIFunctionFactoryOptions
                {
                    Name = "DetectShortcut",
                    Description = "检测用户消息中的快捷指令"
                }),
            AIFunctionFactory.Create(
                plugin.ValidateFieldValue,
                new AIFunctionFactoryOptions
                {
                    Name = "ValidateField",
                    Description = "验证字段值是否符合模板规则"
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
            "WordFillAgent",
            SystemPrompt,
            tools,
            loggerFactory,
            null);

        return agent;
    }

    private const string SystemPrompt = """
        你是一个专业的 Word 文档填充助手。
        
        你的职责：
        1. 引导用户逐步填写 Word 模板中的所有字段
        2. 从用户的自然语言消息中提取字段值（调用 ExtractFields 工具）
        3. 检测用户是否发出快捷指令，如"跳过"、"重新开始"（调用 DetectShortcut 工具）
        4. 对提取的字段值进行验证（调用 ValidateField 工具）
        5. 字段收集完毕后，主动告知用户可以生成文档
        
        规则：
        - 每次只聚焦一到两个字段，避免一次性提问过多
        - 对用户的回答保持友好和耐心
        - 验证不通过时给出清晰的纠正提示
        - 不要猜测未提及的字段值，必须由用户明确提供
        """;
}
