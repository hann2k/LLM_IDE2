using System.Text;

namespace LlmIde.Core.Artifacts;

/// <summary>
/// Maps a rendered Markdown selection (plain text taken from a rendered view, where structural
/// markers and line breaks are lost) back to the matching span in the original Markdown source.
/// </summary>
public static class MarkdownSelectionMatcher
{
    /// <summary>
    /// Finds the source span that corresponds to a rendered selection.
    /// </summary>
    /// <param name="source">The original Markdown source.</param>
    /// <param name="selection">The rendered selection text.</param>
    /// <returns>The matching source span, or null when not found.</returns>
    public static (int Start, int Length)? FindSourceSpan(string source, string selection)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrWhiteSpace(selection))
        {
            return null;
        }

        (string normalizedSource, List<int> sourceMap) = Normalize(source);
        (string normalizedSelection, _) = Normalize(selection);
        normalizedSelection = normalizedSelection.Trim();

        if (normalizedSelection.Length == 0)
        {
            return null;
        }

        int index = normalizedSource.IndexOf(normalizedSelection, StringComparison.Ordinal);

        if (index < 0)
        {
            return null;
        }

        int start = sourceMap[index];
        int sourceEndExclusive = sourceMap[index + normalizedSelection.Length - 1] + 1;
        return (start, sourceEndExclusive - start);
    }

    /// <summary>
    /// Normalizes Markdown text for matching: drops line-start block markers (heading, list, blockquote,
    /// ordered list) and inline emphasis markers, and collapses whitespace runs to a single space.
    /// Returns the normalized text and a map from each normalized character to its source index.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text and the index map.</returns>
    private static (string Normalized, List<int> Map) Normalize(string text)
    {
        StringBuilder builder = new StringBuilder();
        List<int> map = [];
        bool lastWasSpace = false;
        bool atLineStart = true;
        int i = 0;

        while (i < text.Length)
        {
            if (atLineStart)
            {
                i = SkipLineStartMarkers(text, i);
                atLineStart = false;
                continue;
            }

            char c = text[i];

            if (c == '\n' || c == '\r' || c == ' ' || c == '\t')
            {
                if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    map.Add(i);
                    lastWasSpace = true;
                }

                if (c == '\n')
                {
                    atLineStart = true;
                }

                i++;
                continue;
            }

            if (c == '*' || c == '_' || c == '`' || c == '~')
            {
                // Inline emphasis / code markers are dropped (they are absent from rendered text).
                i++;
                continue;
            }

            builder.Append(c);
            map.Add(i);
            lastWasSpace = false;
            i++;
        }

        if (builder.Length > 0 && builder[^1] == ' ')
        {
            builder.Length--;
            map.RemoveAt(map.Count - 1);
        }

        return (builder.ToString(), map);
    }

    /// <summary>
    /// Skips leading whitespace and block-level Markdown markers at the start of a line.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The line start index.</param>
    /// <returns>The index after the skipped markers.</returns>
    private static int SkipLineStartMarkers(string text, int start)
    {
        int i = SkipSpacesAndTabs(text, start);

        // Heading: one or more '#' followed by a space.
        if (i < text.Length && text[i] == '#')
        {
            int j = i;

            while (j < text.Length && text[j] == '#')
            {
                j++;
            }

            if (j < text.Length && (text[j] == ' ' || text[j] == '\t'))
            {
                i = SkipSpacesAndTabs(text, j);
            }
        }

        // Blockquotes: one or more '>' each with an optional following space.
        while (i < text.Length && text[i] == '>')
        {
            i++;

            if (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
            {
                i++;
            }
        }

        // Unordered list marker: '-', '*' or '+' followed by a space.
        if (i + 1 < text.Length
            && (text[i] == '-' || text[i] == '*' || text[i] == '+')
            && (text[i + 1] == ' ' || text[i + 1] == '\t'))
        {
            return SkipSpacesAndTabs(text, i + 1);
        }

        // Ordered list marker: digits followed by '.' and a space.
        int digitEnd = i;

        while (digitEnd < text.Length && char.IsDigit(text[digitEnd]))
        {
            digitEnd++;
        }

        if (digitEnd > i
            && digitEnd + 1 < text.Length
            && text[digitEnd] == '.'
            && (text[digitEnd + 1] == ' ' || text[digitEnd + 1] == '\t'))
        {
            return SkipSpacesAndTabs(text, digitEnd + 1);
        }

        return i;
    }

    /// <summary>
    /// Advances past spaces and tabs.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The start index.</param>
    /// <returns>The index of the first non-space, non-tab character.</returns>
    private static int SkipSpacesAndTabs(string text, int start)
    {
        int i = start;

        while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
        {
            i++;
        }

        return i;
    }
}
