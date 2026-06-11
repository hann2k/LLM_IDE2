using System;
using System.Collections.Generic;
using LlmIde.Infrastructure.Export;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// Flattens the outline tree (depth-first) into heading/body blocks and writes them to a .docx via
/// <see cref="DocxWriter"/>. Heading level = tree depth; heading text = "Number Title". Markdown in the
/// body is exported as plain text.
/// </summary>
public static class DocxExporter
{
    /// <summary>
    /// Exports the outline + bodies to a .docx file.
    /// </summary>
    /// <param name="outputPath">The destination .docx path.</param>
    /// <param name="roots">The root outline items.</param>
    /// <param name="readBody">Reads an item's body text.</param>
    public static void Export(string outputPath, IEnumerable<OutlineItem> roots, Func<OutlineItem, string> readBody)
    {
        Log.Ins.Debug("시작");
        List<DocxBlock> blocks = new List<DocxBlock>();

        foreach (OutlineItem root in roots)
        {
            Flatten(root, 1, readBody, blocks);
        }

        DocxWriter.Write(outputPath, blocks);
    }

    private static void Flatten(OutlineItem item, int level, Func<OutlineItem, string> readBody, List<DocxBlock> blocks)
    {
        Log.Ins.Debug("시작");
        string heading = string.IsNullOrWhiteSpace(item.Number)
            ? item.Title
            : $"{item.Number} {item.Title}";

        blocks.Add(new DocxBlock(level, heading, readBody(item) ?? string.Empty));

        foreach (OutlineItem child in item.Children)
        {
            Flatten(child, level + 1, readBody, blocks);
        }
    }
}
