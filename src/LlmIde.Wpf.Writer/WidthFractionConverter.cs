using System;
using System.Globalization;
using System.Windows.Data;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// Multiplies a width by a fraction (converter parameter, default 0.72) so a chat bubble can cap
/// its width to a proportion of the conversation panel — the cap scales as the panel is resized.
/// Returns <see cref="double.PositiveInfinity"/> (no cap) until the panel has a measured width.
/// </summary>
public sealed class WidthFractionConverter : IValueConverter
{
    private const double DefaultFraction = 0.72;

    /// <summary>
    /// Converts a panel width to a fractional maximum width.
    /// </summary>
    /// <param name="value">The source width (panel ActualWidth).</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The fraction, as an invariant-culture string.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The fractional width, or positive infinity when the width is not yet known.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width || width <= 0 || double.IsInfinity(width))
        {
            return double.PositiveInfinity;
        }

        double fraction = DefaultFraction;

        if (parameter is string text
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            && parsed > 0)
        {
            fraction = parsed;
        }

        return width * fraction;
    }

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>Never returns.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
