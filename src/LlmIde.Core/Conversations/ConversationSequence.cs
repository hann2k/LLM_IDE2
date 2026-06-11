using Framework.Common.Logger;
namespace LlmIde.Core.Conversations;

/// <summary>
/// Provides helpers for numeric conversation identifiers.
/// </summary>
public static class ConversationSequence
{
    /// <summary>
    /// Converts a sequence number to the stored identifier value.
    /// </summary>
    /// <param name="sequence">The sequence number.</param>
    /// <returns>The stored identifier value.</returns>
    public static string ToId(long sequence)
    {
        Log.Ins.Debug("시작");
        return sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tries to parse a stored identifier as a sequence number. Chat/message sequences are
    /// positive, compression sequences are negative (DEC-085); zero is not a valid sequence.
    /// Legacy identifiers (GUIDs, 'c' suffix) fail to parse and are excluded from numbering.
    /// </summary>
    /// <param name="identifier">The stored identifier.</param>
    /// <param name="sequence">The parsed sequence number.</param>
    /// <returns>True when the identifier is a non-zero numeric sequence.</returns>
    public static bool TryParse(string identifier, out long sequence)
    {
        Log.Ins.Debug("시작");
        sequence = 0;

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        if (!long.TryParse(
            identifier,
            System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture,
            out long parsed))
        {
            return false;
        }

        if (parsed == 0)
        {
            return false;
        }

        sequence = parsed;
        return true;
    }
}
