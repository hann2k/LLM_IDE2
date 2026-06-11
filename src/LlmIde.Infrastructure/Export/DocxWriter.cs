using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Export;

/// <summary>
/// One document block: a heading at the given 1-based level, followed by its body text.
/// </summary>
/// <param name="Level">The heading level (1-based; clamped to the supported range).</param>
/// <param name="Heading">The heading text.</param>
/// <param name="Body">The body text (plain text; newlines become separate paragraphs).</param>
public sealed record DocxBlock(int Level, string Heading, string Body);

/// <summary>
/// Writes a sequence of heading/body blocks to a .docx file using the Open XML SDK.
/// Headings use the built-in Heading styles so Word treats them as navigable headings.
/// </summary>
public static class DocxWriter
{
    private const int MaxHeadingLevel = 6;

    /// <summary>
    /// Writes the blocks to a .docx file (overwriting any existing file).
    /// </summary>
    /// <param name="outputPath">The destination .docx path.</param>
    /// <param name="blocks">The ordered heading/body blocks.</param>
    public static void Write(string outputPath, IEnumerable<DocxBlock> blocks)
    {
        Log.Ins.Debug("시작");
        using WordprocessingDocument doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        MainDocumentPart main = doc.AddMainDocumentPart();
        main.Document = new Document();
        AddStyles(main);
        Body body = main.Document.AppendChild(new Body());

        foreach (DocxBlock block in blocks)
        {
            int level = Math.Clamp(block.Level, 1, MaxHeadingLevel);
            body.AppendChild(Heading(block.Heading ?? string.Empty, level));

            string text = block.Body ?? string.Empty;

            if (text.Length > 0)
            {
                foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                {
                    body.AppendChild(NormalParagraph(line));
                }
            }
        }
    }

    private static Paragraph Heading(string text, int level)
    {
        Log.Ins.Debug("시작");
        Paragraph paragraph = new Paragraph();
        paragraph.AppendChild(new ParagraphProperties(new ParagraphStyleId { Val = "Heading" + level }));
        Run run = paragraph.AppendChild(new Run());
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return paragraph;
    }

    private static Paragraph NormalParagraph(string text)
    {
        Log.Ins.Debug("시작");
        Paragraph paragraph = new Paragraph();
        Run run = paragraph.AppendChild(new Run());
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return paragraph;
    }

    private static void AddStyles(MainDocumentPart main)
    {
        Log.Ins.Debug("시작");
        StyleDefinitionsPart stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        Styles styles = new Styles();

        // Default Normal paragraph style.
        Style normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.AppendChild(new StyleName { Val = "Normal" });
        normal.AppendChild(new PrimaryStyle());
        styles.AppendChild(normal);

        // Built-in Heading1..HeadingN so Word treats them as real (navigable, TOC-able) headings.
        for (int level = 1; level <= MaxHeadingLevel; level++)
        {
            int halfPoints = Math.Max(22, 36 - (level - 1) * 4); // ~18pt down to ~11pt

            Style style = new Style
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading" + level,
                CustomStyle = false
            };
            style.AppendChild(new StyleName { Val = "heading " + level });
            style.AppendChild(new BasedOn { Val = "Normal" });
            style.AppendChild(new NextParagraphStyle { Val = "Normal" });
            style.AppendChild(new PrimaryStyle());
            style.AppendChild(new StyleParagraphProperties(new OutlineLevel { Val = level - 1 }));
            style.AppendChild(new StyleRunProperties(
                new Bold(),
                new FontSize { Val = halfPoints.ToString() }));
            styles.AppendChild(style);
        }

        stylesPart.Styles = styles;
    }
}
