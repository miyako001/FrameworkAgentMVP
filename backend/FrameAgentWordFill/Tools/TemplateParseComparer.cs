using FrameAgentWordFill.Models.Parsing;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// 模板本地解析与 AI 解析差异比较。
/// </summary>
public sealed class TemplateParseComparer
{
    public TemplateParseComparisonResult Compare(TemplateParseResult localResult, TemplateAiParseResult aiResult)
    {
        var comparison = new TemplateParseComparisonResult();

        var localFields = localResult.Fields
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var aiFields = aiResult.Fields
            .Where(f => !f.Contains('.', StringComparison.Ordinal))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var localFieldOnly = localFields.Except(aiFields, StringComparer.OrdinalIgnoreCase).ToList();
        var aiFieldOnly = aiFields.Except(localFields, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var field in localFieldOnly)
        {
            comparison.Warnings.Add($"[AI比对] 本地识别到字段 '{field}'，AI 未识别到");
        }

        foreach (var field in aiFieldOnly)
        {
            comparison.Warnings.Add($"[AI比对] AI 识别到字段 '{field}'，本地未识别到");
        }

        comparison.FieldDiffCount = localFieldOnly.Count + aiFieldOnly.Count;

        var localTableKeys = BuildTableKeysFromLocal(localResult);
        var aiTableKeys = BuildTableKeysFromAi(aiResult);

        var localTableOnly = localTableKeys.Except(aiTableKeys, StringComparer.OrdinalIgnoreCase).ToList();
        var aiTableOnly = aiTableKeys.Except(localTableKeys, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var key in localTableOnly)
        {
            comparison.Warnings.Add($"[AI比对] 本地识别到表格字段 '{key}'，AI 未识别到");
        }

        foreach (var key in aiTableOnly)
        {
            comparison.Warnings.Add($"[AI比对] AI 识别到表格字段 '{key}'，本地未识别到");
        }

        comparison.TableDiffCount = localTableOnly.Count + aiTableOnly.Count;

        foreach (var warning in BuildDotHierarchyWarnings(localTableKeys, "本地"))
        {
            comparison.Warnings.Add(warning);
        }

        foreach (var warning in BuildDotHierarchyWarnings(aiTableKeys, "AI"))
        {
            comparison.Warnings.Add(warning);
        }

        comparison.ComparisonLevel = comparison.FieldDiffCount == 0 && comparison.TableDiffCount == 0
            ? "consistent"
            : "warning";

        return comparison;
    }

    private static HashSet<string> BuildTableKeysFromLocal(TemplateParseResult localResult)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in localResult.Tables)
        {
            foreach (var column in table.Columns)
            {
                keys.Add($"{table.Name}.{column.Name}");
            }
        }

        return keys;
    }

    private static HashSet<string> BuildTableKeysFromAi(TemplateAiParseResult aiResult)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in aiResult.Tables)
        {
            foreach (var column in table.Columns)
            {
                keys.Add($"{table.Name}.{column}");
            }
        }

        return keys;
    }

    private static List<string> BuildDotHierarchyWarnings(HashSet<string> tableKeys, string source)
    {
        var warnings = new List<string>();

        var grouped = tableKeys
            .Select(k => k.Split('.', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0], StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var columns = group.Select(parts => parts[1]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = 0; i < columns.Count; i++)
            {
                for (int j = i + 1; j < columns.Count; j++)
                {
                    if (IsHierarchyConflict(columns[i], columns[j]))
                    {
                        warnings.Add($"[AI比对] {source}解析中存在点号层级冲突：'{group.Key}.{columns[i]}' 与 '{group.Key}.{columns[j]}'");
                    }
                }
            }
        }

        return warnings;
    }

    private static bool IsHierarchyConflict(string left, string right)
    {
        return left.StartsWith($"{right}.", StringComparison.OrdinalIgnoreCase)
            || right.StartsWith($"{left}.", StringComparison.OrdinalIgnoreCase);
    }
}
