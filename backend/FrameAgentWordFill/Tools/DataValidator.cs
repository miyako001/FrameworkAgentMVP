using FrameAgentWordFill.Models.Templates;
using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 数据验证工具（验证字段值是否符合类型要求）
/// </summary>
public sealed class DataValidator
{
    private static readonly Regex PhoneRegex = new(@"^1[3-9]\d{9}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

    /// <summary>
    /// 验证字段值
    /// </summary>
    /// <param name="field">字段定义</param>
    /// <param name="value">字段值</param>
    /// <returns>验证结果（成功/失败，错误消息）</returns>
    public (bool IsValid, string? ErrorMessage) ValidateField(Field field, string value)
    {
        // 必填验证
        if (field.Required && string.IsNullOrWhiteSpace(value))
        {
            return (false, field.MissingPrompt ?? $"{field.Name}不能为空");
        }

        // 如果不是必填且值为空，则通过验证
        if (string.IsNullOrWhiteSpace(value))
        {
            return (true, null);
        }

        // 类型验证
        return field.FieldType switch
        {
            "phone" => ValidatePhone(value, field.InvalidPrompt),
            "email" => ValidateEmail(value, field.InvalidPrompt),
            "date" => ValidateDate(value, field.InvalidPrompt),
            "number" => ValidateNumber(value, field.InvalidPrompt),
            _ => (true, null)
        };
    }

    private (bool, string?) ValidatePhone(string value, string? customMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (true, null);

        if (!PhoneRegex.IsMatch(value))
        {
            return (false, customMessage ?? "电话号码格式不正确");
        }

        return (true, null);
    }

    private (bool, string?) ValidateEmail(string value, string? customMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (true, null);

        if (!EmailRegex.IsMatch(value))
        {
            return (false, customMessage ?? "邮箱格式不正确");
        }

        return (true, null);
    }

    private (bool, string?) ValidateDate(string value, string? customMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (true, null);

        if (!DateTime.TryParse(value, out _))
        {
            return (false, customMessage ?? "日期格式不正确");
        }

        return (true, null);
    }

    private (bool, string?) ValidateNumber(string value, string? customMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (true, null);

        if (!decimal.TryParse(value, out _))
        {
            return (false, customMessage ?? "数字格式不正确");
        }

        return (true, null);
    }
}
