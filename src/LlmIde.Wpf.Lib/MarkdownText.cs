using System.Windows;

namespace LlmIde.Wpf.Lib;

/// <summary>
/// Attached property that renders Markdown text into a read-only <see cref="RichTextBox"/>'s document,
/// so Markdown content can size to its full height inside list rows (no inner scrolling).
/// </summary>
public static class MarkdownText
{
    /// <summary>
    /// Identifies the attached Markdown text property.
    /// </summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(MarkdownText),
        new PropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>
    /// Gets the Markdown text attached to an element.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <returns>The Markdown text.</returns>
    public static string GetText(DependencyObject element)
    {
        return (string)element.GetValue(TextProperty);
    }

    /// <summary>
    /// Sets the Markdown text attached to an element.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="value">The Markdown text.</param>
    public static void SetText(DependencyObject element, string value)
    {
        element.SetValue(TextProperty, value);
    }

    /// <summary>
    /// Re-renders the Markdown into the rich text box document when the text changes.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="e">The change arguments.</param>
    private static void OnTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not System.Windows.Controls.RichTextBox richTextBox)
        {
            return;
        }

        string markdown = e.NewValue as string ?? string.Empty;
        richTextBox.Document = Markdig.Wpf.Markdown.ToFlowDocument(markdown);
    }
}
