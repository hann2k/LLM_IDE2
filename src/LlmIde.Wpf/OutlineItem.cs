using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LlmIde.Wpf;

/// <summary>
/// Represents one outline (목차) node in the writing workspace tree.
/// Body content is stored in a project-root file (never under .llmide); the item id keeps the
/// file traceable back to its outline node.
/// </summary>
public sealed class OutlineItem : INotifyPropertyChanged
{
    private string number = string.Empty;
    private string title = string.Empty;
    private bool isSelected;
    private bool isExpanded = true;

    /// <summary>
    /// Occurs when a bindable property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the stable item identifier (embedded in the body file name).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chapter/section number (auto-assigned on reorder).
    /// </summary>
    public string Number
    {
        get => number;
        set
        {
            if (number == value)
            {
                return;
            }

            number = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the outline title.
    /// </summary>
    public string Title
    {
        get => title;
        set
        {
            if (title == value)
            {
                return;
            }

            title = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the project-root-relative body file path (IDE-assigned, id-traceable).
    /// </summary>
    public string BodyFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body summary shown on hover.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Gets the child outline items.
    /// </summary>
    public ObservableCollection<OutlineItem> Children { get; } = [];

    /// <summary>
    /// Gets or sets the parent item (null for a root item). Not persisted.
    /// </summary>
    [JsonIgnore]
    public OutlineItem? Parent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this item is selected in the tree. Not persisted.
    /// </summary>
    [JsonIgnore]
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this item is expanded in the tree. Not persisted.
    /// </summary>
    [JsonIgnore]
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
            {
                return;
            }

            isExpanded = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Raises the property changed event.
    /// </summary>
    /// <param name="propertyName">The changed property name.</param>
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
