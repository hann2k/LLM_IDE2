using System.Collections.Generic;
using System.Windows;
using LlmIde.Core.Agents;

namespace LlmIde.Wpf;

/// <summary>
/// Provides a read-only view of the tools the LLM can use. Tools are implemented in code, so this
/// dialog only lists them; it does not add, edit, or remove tools.
/// </summary>
public partial class ToolManagerDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolManagerDialog"/> class.
    /// </summary>
    /// <param name="tools">The available tool descriptors to display.</param>
    public ToolManagerDialog(IReadOnlyList<AgentToolDescriptor> tools)
    {
        InitializeComponent();
        ToolList.ItemsSource = tools;
    }

    /// <summary>
    /// Closes the dialog.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
