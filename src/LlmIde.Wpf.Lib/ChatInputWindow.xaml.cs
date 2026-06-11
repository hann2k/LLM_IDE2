using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Lib;

/// <summary>
/// A standalone, movable and resizable chat input popup window. Raises events back to the owner
/// for sending messages, registering manual conversations, and changing the provider.
/// </summary>
public partial class ChatInputWindow : Window
{
    /// <summary>
    /// Suppresses provider change events while the combo box is populated programmatically.
    /// </summary>
    private bool suppressProviderChange;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInputWindow"/> class.
    /// </summary>
    public ChatInputWindow()
    {
        Log.Ins.Debug("시작");
        InitializeComponent();
    }

    /// <summary>
    /// Raised when the user sends a message. The argument is the message text.
    /// </summary>
    public event Action<string>? SendRequested;

    /// <summary>
    /// Invoked to register a manual conversation. Returns true when registration succeeded.
    /// </summary>
    public Func<string, string, bool>? ManualRegisterRequested { get; set; }

    /// <summary>
    /// Raised when the user selects a different provider. The argument is the provider name.
    /// </summary>
    public event Action<string>? ProviderChanged;

    /// <summary>
    /// Gets a value indicating whether manual conversation mode is enabled.
    /// </summary>
    private bool IsManualMode => ManualModeCheckBox.IsChecked == true;

    /// <summary>
    /// Populates the provider combo box with the registered providers.
    /// </summary>
    /// <param name="providerNames">The provider names.</param>
    /// <param name="selectedProvider">The currently selected provider.</param>
    public void SetProviders(IReadOnlyList<string> providerNames, string selectedProvider)
    {
        Log.Ins.Debug("시작");
        suppressProviderChange = true;
        ProviderComboBox.ItemsSource = providerNames;
        ProviderComboBox.SelectedItem = selectedProvider;
        suppressProviderChange = false;
    }

    /// <summary>
    /// Focuses the user input and places the caret at the end.
    /// </summary>
    public void FocusInput()
    {
        Log.Ins.Debug("시작");
        UserInput.Focus();
        UserInput.CaretIndex = UserInput.Text.Length;
    }

    /// <summary>
    /// Restores the input text and shows the window so a failed send can be retried.
    /// </summary>
    /// <param name="text">The message text to restore.</param>
    public void RestoreForRetry(string text)
    {
        Log.Ins.Debug("시작");
        UserInput.Text = text;
        Show();
        Activate();
        FocusInput();
    }

    /// <summary>
    /// Handles window-level keys (Escape hides the window, keeping the text).
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
        }
    }

    /// <summary>
    /// Handles Enter in the user input: sends unless in manual mode (then Enter is a newline).
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void UserInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (IsManualMode)
        {
            return;
        }

        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            Send();
        }
    }

    /// <summary>
    /// Sends the current user input, or closes the window when it is empty.
    /// </summary>
    private void Send()
    {
        Log.Ins.Debug("시작");
        string text = UserInput.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            UserInput.Clear();
            Hide();
            System.Windows.MessageBox.Show(this, "전송할 내용이 없어 전송되지 않고 닫혔습니다.", "전송", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SendRequested?.Invoke(text);
        UserInput.Clear();
        Hide();
    }

    /// <summary>
    /// Toggles manual conversation mode: shows the assistant input and register button.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ManualModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        bool manual = IsManualMode;
        AssistantInputArea.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        RegisterButton.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;

        // In manual mode, split the user and assistant inputs evenly (50/50).
        AssistantRow.Height = manual ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        HintText.Text = manual
            ? "수동대화: Enter=줄바꿈, [등록]으로 추가, ESC: 닫기"
            : "Enter: 전송   Shift+Enter: 줄바꿈   ESC: 닫기";
    }

    /// <summary>
    /// Registers a manual conversation from the user and assistant inputs.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (string.IsNullOrWhiteSpace(UserInput.Text) || string.IsNullOrWhiteSpace(AssistantInput.Text))
        {
            System.Windows.MessageBox.Show(this, "user와 assistant 내용을 모두 입력하세요.", "수동대화 추가", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool registered = ManualRegisterRequested?.Invoke(UserInput.Text, AssistantInput.Text) ?? false;

        if (registered)
        {
            UserInput.Clear();
            AssistantInput.Clear();
            UserInput.Focus();
        }
    }

    /// <summary>
    /// Raises the provider change event when the user picks a provider.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        Log.Ins.Debug("시작");
        if (suppressProviderChange)
        {
            return;
        }

        if (ProviderComboBox.SelectedItem is string providerName && !string.IsNullOrWhiteSpace(providerName))
        {
            ProviderChanged?.Invoke(providerName);
        }
    }
}
