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
                Title = GetAttribute(attributes, "title", "Untitled Artifact"),
                Type = GetAttribute(attributes, "type", "note"),
                TargetPath = GetAttribute(attributes, "path", string.Empty),
                Content = content
            });
        }

        return candidates;
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
