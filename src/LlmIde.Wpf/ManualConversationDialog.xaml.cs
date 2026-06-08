using System.Windows;

namespace LlmIde.Wpf;

/// <summary>
/// Collects a user and assistant message to add a completed conversation manually (no LLM call).
/// </summary>
public partial class ManualConversationDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ManualConversationDialog"/> class.
    /// </summary>
    public ManualConversationDialog()
    {
        InitializeComponent();
        UserTextBox.Focus();
    }

    /// <summary>
    /// Gets the entered user message text.
    /// </summary>
    public string UserText
    {
        get
        {
            return UserTextBox.Text;
        }
    }

    /// <summary>
    /// Gets the entered assistant message text.
    /// </summary>
    public string AssistantText
    {
        get
        {
            return AssistantTextBox.Text;
        }
    }

    /// <summary>
    /// Validates input and accepts the dialog.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UserTextBox.Text) || string.IsNullOrWhiteSpace(AssistantTextBox.Text))
        {
            System.Windows.MessageBox.Show(
                this,
                "user와 assistant 내용을 모두 입력하세요.",
                "대화 수동 추가",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    /// <summary>
    /// Cancels the dialog.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
