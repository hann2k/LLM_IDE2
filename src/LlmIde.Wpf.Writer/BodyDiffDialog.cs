using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// Shows the current body (이전) and the LLM-proposed revision (수정) side by side, and lets the user
/// apply the change (변경) or discard it (취소).
/// </summary>
public sealed class BodyDiffDialog : Window
{
    private BodyDiffDialog(string original, string revised, Window owner)
    {
        Title = "본문 수정 확인";
        Owner = owner;
        Width = 1000;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Malgun Gothic");

        Grid root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(columns, 0);

        columns.Children.Add(BuildPane("이전", original, "#666666", 0));
        columns.Children.Add(BuildPane("수정", revised, "#2E7D32", 1));
        root.Children.Add(columns);

        StackPanel buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        Button apply = new Button { Content = "변경", Width = 90, Height = 30, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        apply.Click += (_, _) => DialogResult = true;

        Button cancel = new Button { Content = "취소", Width = 90, Height = 30, IsCancel = true };

        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);

        Content = root;
    }

    /// <summary>
    /// Shows the dialog and returns true when the user chose to apply the revision.
    /// </summary>
    /// <param name="original">The current body.</param>
    /// <param name="revised">The proposed body.</param>
    /// <param name="owner">The owner window.</param>
    /// <returns>True when the user applied the change.</returns>
    public static bool Confirm(string original, string revised, Window owner)
    {
        return new BodyDiffDialog(original, revised, owner).ShowDialog() == true;
    }

    private static Border BuildPane(string header, string text, string headerColor, int column)
    {
        DockPanel panel = new DockPanel();

        panel.Children.Add(new TextBlock
        {
            Text = header,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Margin = new Thickness(8, 6, 8, 6),
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(headerColor)!
        });
        DockPanel.SetDock(panel.Children[0], Dock.Top);

        panel.Children.Add(new TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            FontSize = 13
        });

        Border border = new Border
        {
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4D, 0x8E, 0xDB)),
            BorderThickness = new Thickness(column == 0 ? 1 : 0, 1, 1, 1),
            Margin = new Thickness(0, 0, column == 0 ? 6 : 0, 0),
            Child = panel
        };

        // Without this the two panes both land in column 0 and overlap into one.
        Grid.SetColumn(border, column);
        return border;
    }
}
