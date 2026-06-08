using System.Text.RegularExpressions;

namespace LlmIde.Core.Artifacts;

/// <summary>
/// Extracts explicit artifact tags from AI responses.
/// </summary>
public static class ArtifactTagParser
{
    private static readonly Regex ArtifactRegex = new Regex(
        @"<artifact(?<attributes>[^>]*)>(?<content>.*?)</artifact>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AttributeRegex = new Regex(
        @"(?<name>[a-zA-Z_][a-zA-Z0-9_\-]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Extracts artifact candidates from response text.
    /// </summary>
    /// <param name="text">The response text.</param>
    /// <returns>The extracted artifact candidates.</returns>
    public static IReadOnlyList<ArtifactCandidate> Extract(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        List<ArtifactCandidate> candidates = [];

        foreach (Match match in ArtifactRegex.Matches(text))
        {
            Dictionary<string, string> attributes = ParseAttributes(match.Groups["attributes"].Value);
            string content = match.Groups["content"].Value.Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            candidates.Add(new ArtifactCandidate
            {
                Title = GetAttribute(attributes, "title", "제목 없는 산출물"),
                Type = GetAttribute(attributes, "type", "note"),
                TargetPath = GetAttribute(attributes, "path", string.Empty),
                Content = content
            });
        }

        return candidates;
    }

    /// <summary>
    /// Removes artifact tag blocks from response text for display.
    /// </summary>
    /// <param name="text">The response text.</param>
    /// <returns>The text without artifact blocks.</returns>
    public static string RemoveArtifacts(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return ArtifactRegex.Replace(text, string.Empty).Trim();
    }

    /// <summary>
    /// Replaces artifact tag blocks with the extracted artifact title for display.
    /// </summary>
    /// <param name="text">The response text.</param>
    /// <returns>The text with artifact blocks replaced by their titles.</returns>
    public static string ReplaceArtifactsWithTitles(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return ArtifactRegex.Replace(text, ReplaceWithTitle).Trim();
    }

    /// <summary>
    /// Builds the title replacement for one artifact match.
    /// </summary>
    /// <param name="match">The artifact match.</param>
    /// <returns>The artifact title marker.</returns>
    private static string ReplaceWithTitle(Match match)
    {
        Dictionary<string, string> attributes = ParseAttributes(match.Groups["attributes"].Value);
        string title = GetAttribute(attributes, "title", "제목 없는 산출물");
        return $"[아티팩트: {title}]";
    }

    private static Dictionary<string, string> ParseAttributes(string attributes)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AttributeRegex.Matches(attributes))
        {
            result[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return result;
    }

    private static string GetAttribute(
        IReadOnlyDictionary<string, string> attributes,
        string name,
        string fallback)
    {
        return attributes.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }
}
