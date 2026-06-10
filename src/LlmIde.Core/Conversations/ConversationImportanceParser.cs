using System.Text.RegularExpressions;

namespace LlmIde.Core.Conversations;

/// <summary>
/// Parses conversation importance metadata from provider responses.
/// </summary>
public static partial class ConversationImportanceParser
{
    /// <summary>
    /// Parses importance metadata and removes it from response content.
    /// </summary>
    /// <param name="content">The provider response content.</param>
    /// <returns>The parsing result.</returns>
    public static ConversationImportanceParseResult Parse(string content)
    {
        Match xmlMatch = ImportanceXmlRegex().Match(content);

        if (xmlMatch.Success)
        {
            return CreateResult(content, xmlMatch, xmlMatch.Groups["weight"].Value);
        }

        Match commentMatch = ImportanceCommentRegex().Match(content);

        if (commentMatch.Success)
        {
            return CreateResult(content, commentMatch, commentMatch.Groups["weight"].Value);
        }

        return new ConversationImportanceParseResult
        {
            Content = content,
            ImportanceWeight = 0
        };
    }

    /// <summary>
    /// Creates a parse result from a matched importance marker.
    /// </summary>
    /// <param name="content">The full response content.</param>
    /// <param name="match">The importance marker match.</param>
    /// <param name="weightText">The weight text.</param>
    /// <returns>The parsing result.</returns>
    private static ConversationImportanceParseResult CreateResult(string content, Match match, string weightText)
    {
        int weight = 0;

        if (int.TryParse(weightText, out int parsedWeight))
        {
            weight = Math.Clamp(parsedWeight, 0, 10);
        }

        string contentWithoutMarker = content.Remove(match.Index, match.Length).TrimEnd();
        return new ConversationImportanceParseResult
        {
            Content = contentWithoutMarker,
            ImportanceWeight = weight
        };
    }

    /// <summary>
    /// Gets the XML-like importance marker regex.
    /// </summary>
    /// <returns>The regex.</returns>
    [GeneratedRegex(
        @"\s*(?:`{3}[a-zA-Z]*\s*)?<llmide_importance_weight>\s*(?<weight>\d{1,2})\s*(?:</llmide_importance_weight>)?\s*(?:`{3})?\s*",
        RegexOptions.IgnoreCase)]
    private static partial Regex ImportanceXmlRegex();

    /// <summary>
    /// Gets the HTML comment importance marker regex.
    /// </summary>
    /// <returns>The regex.</returns>
    [GeneratedRegex(
        @"\s*<!--\s*llmide_importance_weight\s*:\s*(?<weight>\d{1,2})\s*-->\s*",
        RegexOptions.IgnoreCase)]
    private static partial Regex ImportanceCommentRegex();
}
