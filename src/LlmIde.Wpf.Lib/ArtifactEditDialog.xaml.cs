using System.Windows;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Lib;

/// <summary>
/// Edits an artifact extracted from a conversation response: title, type (combo box), and content,
/// while keeping the source conversation identifier.
/// </summary>
public partial class ArtifactEditDialog : Window
{
    /// <summary>
    /// The artifact types allowed by the artifact rule policy.
    /// </summary>
    private static readonly string[] ArtifactTypes =
    [
        "Code", "Table", "Markdown", "Json", "Prompt", "Plan", "Review", "Patch", "Command", "Log", "Report", "Config"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactEditDialog"/> class.
    /// </summary>
    /// <param name="sourceRequestId">The source conversation identifier.</param>
    /// <param name="content">The initial artifact content (the dragged selection).</param>
    public ArtifactEditDialog(string sourceRequestId, string content)
    {
        Log.Ins.Debug("시작");
        InitializeComponent();

        TypeComboBox.ItemsSource = ArtifactTypes;
        TypeComboBox.SelectedItem = "Markdown";
        SourceIdText.Text = $"출처 대화 ID: {sourceRequestId}";
        ContentTextBox.Text = content;
        TitleTextBox.Focus();
    }

    /// <summary>
    /// Gets the entered artifact title.
    /// </summary>
    public string ArtifactTitle
    {
        get
        {
            return TitleTextBox.Text.Trim();
        }
    }

    /// <summary>
    /// Gets the selected artifact type.
    /// </summary>
    public string ArtifactType
    {
        get
        {
            return TypeComboBox.SelectedItem as string ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the artifact content.
    /// </summary>
    public string ArtifactContent
    {
        get
        {
            return ContentTextBox.Text;
        }
    }

    /// <summary>
    /// Validates input and accepts the dialog.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "제목을 입력하세요.", "아티팩트 추출", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ArtifactType))
        {
            System.Windows.MessageBox.Show(this, "유형을 선택하세요.", "아티팩트 추출", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ContentTextBox.Text))
        {
            System.Windows.MessageBox.Show(this, "내용이 비어 있습니다.", "아티팩트 추출", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        Log.Ins.Debug("시작");
        DialogResult = false;
    }
}
