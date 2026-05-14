using FrameAgentWordFill.Models.AIExtraction;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;

namespace FrameAgentWordFill.Tools.FileParser;

/// <summary>
/// PDF 文本解析器。
/// </summary>
public sealed class PdfParser
{
    private readonly ILogger<PdfParser> _logger;

    public PdfParser(ILogger<PdfParser> logger)
    {
        _logger = logger;
    }

    public async Task<ParsedDocumentContent> ParseAsync(string filePath)
    {
        var result = new ParsedDocumentContent { FileType = "PDF" };

        try
        {
            await Task.Run(() =>
            {
                using var reader = new PdfReader(filePath);
                using var pdf = new PdfDocument(reader);
                var textBuilder = new StringBuilder();

                for (var pageNum = 1; pageNum <= pdf.GetNumberOfPages(); pageNum++)
                {
                    var page = pdf.GetPage(pageNum);
                    var strategy = new LocationTextExtractionStrategy();
                    var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        textBuilder.AppendLine(pageText);
                        textBuilder.AppendLine();
                    }
                }

                result.PlainText = textBuilder.ToString().Trim();

                if (string.IsNullOrWhiteSpace(result.PlainText))
                {
                    result.ParseQuality = 40;
                    result.Warnings.Add("PDF 文本提取结果为空，可能为扫描件或图片型 PDF");
                }
                else
                {
                    result.ParseQuality = 85;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF 解析失败");
            result.ParseQuality = 20;
            result.Warnings.Add($"PDF 解析失败：{ex.Message}");
        }

        return result;
    }
}
