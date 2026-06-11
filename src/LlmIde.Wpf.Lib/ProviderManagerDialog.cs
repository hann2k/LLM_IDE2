using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LlmIde.Core.Providers;
using Binding = System.Windows.Data.Binding;
using Button = System.Windows.Controls.Button;
using DataGrid = System.Windows.Controls.DataGrid;
using ComboBox = System.Windows.Controls.ComboBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Lib;

/// <summary>
/// Manages the common LLM providers (shared by all projects): add / edit / delete provider entries
/// (name, API key, model, endpoint) and choose the default provider.
/// </summary>
public sealed class ProviderManagerDialog : Window
{
    private readonly IProviderSettingsStore store;
    private readonly ObservableCollection<ProviderSettings> providers;
    private readonly DataGrid grid;
    private readonly ComboBox defaultBox;

    private ProviderManagerDialog(IProviderSettingsStore store, Window owner)
    {
        Log.Ins.Debug("시작");
        this.store = store;
        ProviderSettingsDocument document = store.Load(string.Empty);
        providers = new ObservableCollection<ProviderSettings>(document.Providers);

        Title = "LLM 관리";
        Owner = owner;
        Width = 760;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Malgun Gothic");

        Grid root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        grid = new DataGrid
        {
            ItemsSource = providers,
            AutoGenerateColumns = false,
            CanUserAddRows = true,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.All
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "이름", Binding = new Binding("Name"), Width = 120 });
        grid.Columns.Add(new DataGridTextColumn { Header = "API 키", Binding = new Binding("ApiKey"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "모델", Binding = new Binding("Model"), Width = 150 });
        grid.Columns.Add(new DataGridTextColumn { Header = "엔드포인트", Binding = new Binding("Endpoint"), Width = 200 });
        Grid.SetRow(grid, 0);
        root.Children.Add(grid);

        StackPanel defaultRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        defaultRow.Children.Add(new TextBlock { Text = "기본 프로바이더:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        defaultBox = new ComboBox { Width = 160, ItemsSource = providers, DisplayMemberPath = "Name" };
        defaultBox.SelectedItem = providers.FirstOrDefault(provider =>
            string.Equals(provider.Name, document.DefaultProvider, System.StringComparison.OrdinalIgnoreCase));
        defaultRow.Children.Add(defaultBox);
        Button deleteButton = new Button { Content = "선택 삭제", Width = 90, Height = 28, Margin = new Thickness(16, 0, 0, 0) };
        deleteButton.Click += (_, _) =>
        {
            if (grid.SelectedItem is ProviderSettings selected)
            {
                providers.Remove(selected);
            }
        };
        defaultRow.Children.Add(deleteButton);
        Grid.SetRow(defaultRow, 1);
        root.Children.Add(defaultRow);

        StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        Button save = new Button { Content = "저장", Width = 80, Height = 30, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        save.Click += (_, _) => Persist();
        Button cancel = new Button { Content = "취소", Width = 80, Height = 30, IsCancel = true };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        Content = root;
    }

    /// <summary>
    /// Opens the provider manager dialog.
    /// </summary>
    /// <param name="store">The common provider settings store.</param>
    /// <param name="owner">The owner window.</param>
    public static void Show(IProviderSettingsStore store, Window owner)
    {
        Log.Ins.Debug("시작");
        new ProviderManagerDialog(store, owner).ShowDialog();
    }

    private void Persist()
    {
        // Commit any in-progress grid edit before saving.
        Log.Ins.Debug("시작");
        grid.CommitEdit(DataGridEditingUnit.Row, true);

        List<ProviderSettings> valid = providers
            .Where(provider => !string.IsNullOrWhiteSpace(provider.Name))
            .ToList();

        ProviderSettingsDocument document = new ProviderSettingsDocument
        {
            Providers = valid,
            DefaultProvider = (defaultBox.SelectedItem as ProviderSettings)?.Name
                ?? valid.FirstOrDefault()?.Name
                ?? "deepseek"
        };

        store.Save(string.Empty, document);
        DialogResult = true;
    }
}
