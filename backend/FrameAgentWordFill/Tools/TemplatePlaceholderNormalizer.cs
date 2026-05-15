using System.Text.RegularExpressions;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 占位符规范化工具：统一不同语法包装并验证 token。
/// </summary>
public sealed class TemplatePlaceholderNormalizer
{
    private static readonly Regex ValidTokenRegex = new(
        @"^[a-zA-Z0-9_\u4e00-\u9fa5]+(?:\.[a-zA-Z0-9_\u4e00-\u9fa5]+)*$",
        RegexOptions.Compiled
    );

    public PlaceholderNormalizationResult Normalize(string rawPlaceholder)
    {
        if (string.IsNullOrWhiteSpace(rawPlaceholder))
        {
            return PlaceholderNormalizationResult.Fail("占位符为空，已忽略");
        }

        var raw = rawPlaceholder.Trim();
        var (innerToken, syntaxType) = StripWrapper(raw);
        if (innerToken == null)
        {
            return PlaceholderNormalizationResult.Fail($"无法识别占位符语法: '{rawPlaceholder}'");
        }

        var normalizedToken = NormalizeToken(innerToken);
        if (!ValidTokenRegex.IsMatch(normalizedToken))
        {
            return PlaceholderNormalizationResult.Fail($"占位符 '{rawPlaceholder}' 包含非法字符，已忽略");
        }

        var canonical = $"{{{normalizedToken}}}";
        var changed = !string.Equals(raw, canonical, StringComparison.Ordinal);
        var warning = changed
            ? $"占位符 '{rawPlaceholder}' 已规范化为 '{canonical}'"
            : null;

        return PlaceholderNormalizationResult.Ok(normalizedToken, syntaxType, warning);
    }

    private static string NormalizeToken(string token)
    {
        var normalized = token.Trim();
        normalized = Regex.Replace(normalized, @"\s*\.\s*", ".");
        normalized = Regex.Replace(normalized, @"\s+", "");
        return normalized;
    }

    private static (string? Token, string SyntaxType) StripWrapper(string raw)
    {
        if (raw.StartsWith("{{", StringComparison.Ordinal) && raw.EndsWith("}}", StringComparison.Ordinal) && raw.Length > 4)
        {
            return (raw[2..^2], "doubleCurly");
        }

        if (raw.StartsWith("{", StringComparison.Ordinal) && raw.EndsWith("}", StringComparison.Ordinal) && raw.Length > 2)
        {
            return (raw[1..^1], "curly");
        }

        if (raw.StartsWith("[", StringComparison.Ordinal) && raw.EndsWith("]", StringComparison.Ordinal) && raw.Length > 2)
        {
            return (raw[1..^1], "square");
        }

        if (raw.StartsWith("(", StringComparison.Ordinal) && raw.EndsWith(")", StringComparison.Ordinal) && raw.Length > 2)
        {
            return (raw[1..^1], "round");
        }

        if (raw.StartsWith("【", StringComparison.Ordinal) && raw.EndsWith("】", StringComparison.Ordinal) && raw.Length > 2)
        {
            return (raw[1..^1], "fullWidthCurly");
        }

        if (raw.StartsWith("［", StringComparison.Ordinal) && raw.EndsWith("］", StringComparison.Ordinal) && raw.Length > 2)
        {
            return (raw[1..^1], "fullWidthSquare");
        }

        return (null, "unknown");
    }
}

public sealed class PlaceholderNormalizationResult
{
    public bool Success { get; private set; }

    public string NormalizedToken { get; private set; } = string.Empty;

    public string SyntaxType { get; private set; } = "unknown";

    public string? Warning { get; private set; }

    public string? Error { get; private set; }

    public static PlaceholderNormalizationResult Ok(string normalizedToken, string syntaxType, string? warning)
    {
        return new PlaceholderNormalizationResult
        {
            Success = true,
            NormalizedToken = normalizedToken,
            SyntaxType = syntaxType,
            Warning = warning
        };
    }

    public static PlaceholderNormalizationResult Fail(string error)
    {
        return new PlaceholderNormalizationResult
        {
            Success = false,
            Error = error
        };
    }
}
