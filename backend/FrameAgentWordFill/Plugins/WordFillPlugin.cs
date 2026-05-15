using System.ComponentModel;
using FrameAgentWordFill.Models.Templates;
using FrameAgentWordFill.Tools;
using System.Text.Json;

namespace FrameAgentWordFill.Plugins;

/// <summary>
/// 对话填充场景工具集，由 WordFillAgent 按需调用。
/// </summary>
public sealed class WordFillPlugin
{
    private readonly AIFieldExtractor _fieldExtractor;
    private readonly DataValidator _dataValidator;

    public WordFillPlugin(AIFieldExtractor fieldExtractor, DataValidator dataValidator)
    {
        _fieldExtractor = fieldExtractor;
        _dataValidator = dataValidator;
    }

    [Description("从用户自然语言消息中提取字段值。返回 JSON 格式的字段名到值的映射。")]
    public async Task<string> ExtractFieldsFromMessageAsync(
        [Description("用户输入的原始消息")] string userMessage,
        [Description("需要提取的字段列表，JSON 数组，每项包含 name/fieldType/required")] string fieldsJson)
    {
        var fields = JsonSerializer.Deserialize<List<Field>>(fieldsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<Field>();

        var result = await _fieldExtractor.ExtractFieldsAsync(userMessage, fields);
        return JsonSerializer.Serialize(result);
    }

    [Description("\u68c0\u6d4b\u7528\u6237\u6d88\u606f\u4e2d\u662f\u5426\u542b\u6709\u5feb\u6377\u6307\u4ee4\uff0c\u8fd4\u56de JSON\uff0c\u5305\u542b hasShortcut \u548c commandType\u3002")]
    public async Task<string> DetectShortcutCommandAsync(
        [Description("用户输入的消息")] string userMessage)
    {
        var result = await _fieldExtractor.DetectShortcutAsync(userMessage);
        return JsonSerializer.Serialize(result);
    }

    [Description("验证单个字段值是否符合模板规则（类型、必填、格式约束）。返回 isValid 和 errorMessage。")]
    public string ValidateFieldValue(
        [Description("字段定义，JSON 格式，包含 name/fieldType/required/validationRule")] string fieldJson,
        [Description("待验证的字段值")] string value)
    {
        var field = JsonSerializer.Deserialize<Field>(fieldJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (field == null) return JsonSerializer.Serialize(new { isValid = false, errorMessage = "字段定义无效" });

        var (isValid, errorMessage) = _dataValidator.ValidateField(field, value);
        return JsonSerializer.Serialize(new { isValid, errorMessage });
    }
}
