using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// A minimal single-line text input dialog (prompt + text box + 확인/취소).
/// </summary>
public sealed class TextInputDialog : Window
{
    private readonly TextBox inputBox;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextInputDialog"/> class.
    /// </summary>
    /// <param name="title">The window title.</param>
    /// <param name="prompt">The prompt shown above the input.</param>
    /// <param name="initialText">The initial text.</param>
    /// <param name="owner">The owner window.</param>
    public TextInputDialog(string title, string prompt, string initialText, Window owner)
    {
        Log.Ins.Debug("시작");
        Title = title;
        Owner = owner;
        Width = 400;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Malgun Gothic");

        StackPanel root = new StackPanel { Margin = new Thickness(16) };

        root.Children.Add(new TextBlock
        {
            Text = prompt,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        });

        inputBox = new TextBox
        {
            Text = initialText,
            FontSize = 14,
            Padding = new Thickness(6, 4, 6, 4)
        };
        root.Children.Add(inputBox);

        StackPanel buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        Button okButton = new Button
        {
            Content = "확인",
            Width = 72,
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0)
        };
        okButton.Click += (_, _) =>
        {
            DialogResult = true;
        };

        Button cancelButton = new Button
        {
            Content = "취소",
            Width = 72,
            IsCancel = true
        };

        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        root.Children.Add(buttons);

        Content = root;

        Loaded += (_, _) =>
        {
            inputBox.Focus();
            inputBox.SelectAll();
        };
    }

    /// <summary>
    /// Gets the entered text (trimmed).
    /// </summary>
    public string ResponseText => inputBox.Text.Trim();

    /// <summary>
    /// Shows the dialog and returns the entered text, or null when cancelled or empty.
    /// </summary>
    /// <param name="title">The window title.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="initialText">The initial text.</param>
    /// <param name="owner">The owner window.</param>
    /// <returns>The entered text, or null.</returns>
    public static string? Prompt(string title, string prompt, string initialText, Window owner)
    {
        Log.Ins.Debug("시작");
        TextInputDialog dialog = new TextInputDialog(title, prompt, initialText, owner);

        if (dialog.ShowDialog() == true && dialog.ResponseText.Length > 0)
        {
            return dialog.ResponseText;
        }

        return null;
    }
}
