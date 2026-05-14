namespace FrameAgentWordFill.Agents;

public static class AIExtractionAgentConfig
{
    public const string BatchExtractionPrompt = @"
你是一个专业的文档信息提取助手。

任务：从下方文档内容中提取字段值，并输出 JSON。

文档内容：
{DOCUMENT_CONTENT}

需要提取的字段：
{FIELD_LIST}

规则：
1. 只能返回 JSON，不要输出解释文本。
2. 对每个字段都输出一条记录。
3. 无法确认时 fieldValue 输出 null，confidence 输出较低分。
4. matchMethod 仅允许 Exact、Fuzzy、Semantic。

输出格式：
{
  ""fields"": [
    {
      ""fieldName"": ""字段名"",
      ""fieldValue"": ""提取值或null"",
      ""confidence"": 0,
      ""sourceText"": ""文档原文片段"",
      ""matchMethod"": ""Exact|Fuzzy|Semantic""
    }
  ]
}
";

    public const string SemanticMatchPrompt = @"
任务：判断字段标签是否语义等价。
文档字段：{DOCUMENT_LABEL}
模板字段：{TEMPLATE_FIELD}

仅返回 JSON：
{
  ""isMatch"": true,
  ""confidence"": 0,
  ""reason"": ""简短原因""
}
";
}
