using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LlmIde.Wpf;

/// <summary>
/// Converts a conversation importance weight to the delete button visibility:
/// the delete button is hidden once importance reaches 5 or higher.
/// </summary>
public sealed class ImportanceToDeleteVisibilityConverter : IValueConverter
{
    /// <summary>
    /// The importance threshold at and above which the delete button is hidden.
    /// </summary>
    private const int HideThreshold = 5;

    /// <summary>
    /// Converts an importance weight to a <see cref="Visibility"/>.
    /// </summary>
    /// <param name="value">The importance weight.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The converter parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>Collapsed when importance is 5 or higher; otherwise Visible.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int importance = value is int weight ? weight : 0;
        return importance >= HideThreshold ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The converter parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>Never returns.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
