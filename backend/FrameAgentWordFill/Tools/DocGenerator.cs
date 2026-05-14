using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FrameAgentWordFill.Tools;

/// <summary>
/// Word 文档生成工具（普通字段替换 + 表格填充）
/// </summary>
public sealed class DocGenerator
{
    private readonly ILogger<DocGenerator> _logger;

    public DocGenerator(ILogger<DocGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成填充后的文档
    /// </summary>
    /// <param name="templatePath">模板文件路径</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="fieldData">字段数据（键值对）</param>
    /// <param name="tableData">表格数据（表格名 → 行数据列表）</param>
    /// <returns>生成是否成功</returns>
    public async Task<(bool Success, string? ErrorMessage)> GenerateDocumentAsync(
        string templatePath,
        string outputPath,
        Dictionary<string, string> fieldData,
        Dictionary<string, List<Dictionary<string, string>>> tableData)
    {
        try
        {
            // 1. 复制模板文件到输出路径
            await Task.Run(() => File.Copy(templatePath, outputPath, true));

            // 2. 打开文档进行编辑
            using var document = WordprocessingDocument.Open(outputPath, true);
            if (document.MainDocumentPart == null)
            {
                return (false, "无法打开文档：MainDocumentPart 为空");
            }

            var body = document.MainDocumentPart.Document.Body;
            if (body == null)
            {
                return (false, "文档内容为空");
            }

            // 3. 替换普通字段
            ReplaceFieldsInDocument(body, fieldData);

            // 4. 填充表格
            FillTablesInDocument(body, tableData);

            // 5. 保存文档
            document.MainDocumentPart.Document.Save();

            _logger.LogInformation("文档生成成功: {OutputPath}", outputPath);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成文档失败: {TemplatePath} -> {OutputPath}", templatePath, outputPath);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 替换文档中的普通字段占位符
    /// </summary>
    private void ReplaceFieldsInDocument(Body body, Dictionary<string, string> fieldData)
    {
        // 获取所有文本元素（Text nodes）
        var textElements = body.Descendants<Text>().ToList();

        foreach (var textElement in textElements)
        {
            if (string.IsNullOrWhiteSpace(textElement.Text))
                continue;

            var originalText = textElement.Text;
            var replacedText = originalText;

            // 替换所有匹配的占位符
            foreach (var kvp in fieldData)
            {
                var placeholder = $"{{{kvp.Key}}}";
                if (replacedText.Contains(placeholder))
                {
                    replacedText = replacedText.Replace(placeholder, kvp.Value);
                    _logger.LogDebug("替换字段: {Placeholder} -> {Value}", placeholder, kvp.Value);
                }
            }

            // 如果有替换，更新文本
            if (replacedText != originalText)
            {
                textElement.Text = replacedText;
            }
        }
    }

    /// <summary>
    /// 填充文档中的表格
    /// </summary>
    private void FillTablesInDocument(Body body, Dictionary<string, List<Dictionary<string, string>>> tableData)
    {
        var tables = body.Elements<Table>().ToList();

        foreach (var table in tables)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 2) // 至少需要表头 + 1个数据行模板
                continue;

            // 第一行是表头，第二行是数据行模板
            var headerRow = rows[0];
            var templateRow = rows[1];

            // 识别表格名称（从表头第一个单元格的占位符提取）
            var tableName = ExtractTableNameFromHeader(headerRow);
            if (string.IsNullOrEmpty(tableName) || !tableData.ContainsKey(tableName))
                continue;

            _logger.LogInformation("填充表格: {TableName}, 数据行数: {RowCount}", 
                tableName, tableData[tableName].Count);

            // 获取列映射（占位符 → 列索引）
            var columnMapping = GetColumnMapping(headerRow, tableName);

            // 删除原有的数据行（保留表头）
            for (int i = rows.Count - 1; i >= 1; i--)
            {
                rows[i].Remove();
            }

            // 根据数据创建新行
            var dataRows = tableData[tableName];
            foreach (var rowData in dataRows)
            {
                // 克隆模板行
                var newRow = (TableRow)templateRow.CloneNode(true);
                
                // 填充单元格数据
                FillTableRow(newRow, rowData, columnMapping);
                
                // 添加到表格
                table.AppendChild(newRow);
            }
        }
    }

    /// <summary>
    /// 从表头提取表格名称
    /// </summary>
    private string? ExtractTableNameFromHeader(TableRow headerRow)
    {
        var cells = headerRow.Elements<TableCell>().ToList();
        if (cells.Count == 0)
            return null;

        var firstCellText = GetCellText(cells[0]);
        
        // 匹配 {表格名.字段名} 格式
        var match = System.Text.RegularExpressions.Regex.Match(
            firstCellText, 
            @"\{([a-zA-Z0-9_\u4e00-\u9fa5]+)\.([a-zA-Z0-9_\u4e00-\u9fa5]+)\}"
        );

        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 获取列映射（字段名 → 列索引）
    /// </summary>
    private Dictionary<string, int> GetColumnMapping(TableRow headerRow, string tableName)
    {
        var mapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cells = headerRow.Elements<TableCell>().ToList();

        for (int i = 0; i < cells.Count; i++)
        {
            var cellText = GetCellText(cells[i]);
            
            // 匹配 {表格名.字段名} 格式
            var match = System.Text.RegularExpressions.Regex.Match(
                cellText, 
                @"\{" + tableName + @"\.([a-zA-Z0-9_\u4e00-\u9fa5]+)\}"
            );

            if (match.Success)
            {
                var columnName = match.Groups[1].Value;
                mapping[columnName] = i;
                _logger.LogDebug("列映射: {ColumnName} -> 列索引 {Index}", columnName, i);
            }
        }

        return mapping;
    }

    /// <summary>
    /// 填充表格行数据
    /// </summary>
    private void FillTableRow(
        TableRow row, 
        Dictionary<string, string> rowData, 
        Dictionary<string, int> columnMapping)
    {
        var cells = row.Elements<TableCell>().ToList();

        foreach (var kvp in rowData)
        {
            var columnName = kvp.Key;
            var value = kvp.Value;

            if (columnMapping.TryGetValue(columnName, out var columnIndex))
            {
                if (columnIndex < cells.Count)
                {
                    SetCellText(cells[columnIndex], value);
                }
            }
        }
    }

    /// <summary>
    /// 获取单元格文本
    /// </summary>
    private static string GetCellText(TableCell cell)
    {
        return cell.InnerText;
    }

    /// <summary>
    /// 设置单元格文本
    /// </summary>
    private static void SetCellText(TableCell cell, string text)
    {
        // 清空原有内容
        cell.RemoveAllChildren<Paragraph>();

        // 创建新段落和文本
        var paragraph = new Paragraph();
        var run = new Run();
        var textElement = new Text(text);
        
        run.AppendChild(textElement);
        paragraph.AppendChild(run);
        cell.AppendChild(paragraph);
    }
}
