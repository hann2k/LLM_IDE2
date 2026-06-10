using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LlmIde.Wpf;

/// <summary>
/// Serializable outline node (persisted to the project root's outline.json).
/// </summary>
internal sealed class OutlineNode
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string BodyFile { get; set; } = string.Empty;

    public List<OutlineNode> Children { get; set; } = [];
}

/// <summary>
/// Persists the document outline (outline.json) and per-item body files directly under the project
/// root (never under .llmide), so the read-only agent tools can reach them. File names embed the
/// outline item id, keeping each body file traceable to its node.
/// </summary>
public sealed class OutlineStore
{
    private const string OutlineFileName = "outline.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Loads the outline roots from the project root, or an empty list when none exists.
    /// </summary>
    /// <param name="projectRoot">The project root directory.</param>
    /// <returns>The root outline items.</returns>
    public IReadOnlyList<OutlineItem> Load(string projectRoot)
    {
        string path = Path.Combine(projectRoot, OutlineFileName);

        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            List<OutlineNode>? nodes = JsonSerializer.Deserialize<List<OutlineNode>>(File.ReadAllText(path), Options);
            return nodes is null ? [] : nodes.Select(node => ToItem(node, null)).ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Saves the outline tree to the project root (best-effort).
    /// </summary>
    /// <param name="projectRoot">The project root directory.</param>
    /// <param name="roots">The root outline items.</param>
    public void Save(string projectRoot, IEnumerable<OutlineItem> roots)
    {
        try
        {
            List<OutlineNode> nodes = roots.Select(ToNode).ToList();
            File.WriteAllText(Path.Combine(projectRoot, OutlineFileName), JsonSerializer.Serialize(nodes, Options));
        }
        catch
        {
            // Persistence is best-effort.
        }
    }

    /// <summary>
    /// Ensures the item has a body file name and that the (empty) file exists.
    /// </summary>
    /// <param name="projectRoot">The project root directory.</param>
    /// <param name="item">The outline item.</param>
    public void EnsureBodyFile(string projectRoot, OutlineItem item)
    {
        if (string.IsNullOrEmpty(item.BodyFile))
        {
            item.BodyFile = item.Id + ".md";
        }

        try
        {
            string path = Path.Combine(projectRoot, item.BodyFile);

            if (!File.Exists(path))
            {
                File.WriteAllText(path, string.Empty);
            }
        }
        catch
        {
            // Best-effort.
        }
    }

    /// <summary>
    /// Reads the item's body text (empty when missing).
    /// </summary>
    /// <param name="projectRoot">The project root directory.</param>
    /// <param name="item">The outline item.</param>
    /// <returns>The body text.</returns>
    public string ReadBody(string projectRoot, OutlineItem item)
    {
        if (string.IsNullOrEmpty(item.BodyFile))
        {
            return string.Empty;
        }

        try
        {
            string path = Path.Combine(projectRoot, item.BodyFile);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Writes the item's body text (best-effort).
    /// </summary>
    /// <param name="projectRoot">The project root directory.</param>
    /// <param name="item">The outline item.</param>
    /// <param name="text">The body text.</param>
    public void WriteBody(string projectRoot, OutlineItem item, string text)
    {
        if (string.IsNullOrEmpty(item.BodyFile))
        {
            item.BodyFile = item.Id + ".md";
        }

        try
        {
            File.WriteAllText(Path.Combine(projectRoot, item.BodyFile), text);
        }
        catch
        {
            // Best-effort.
        }
    }

    /// <summary>
    /// Deletes the body files of an item and all its descendants (best-effort).
    /// </summary>
    /// <param name="projectRoot">The project root directory.</param>
    /// <param name="item">The outline item.</param>
    public void DeleteBodyFiles(string projectRoot, OutlineItem item)
    {
        try
        {
            if (!string.IsNullOrEmpty(item.BodyFile))
            {
                string path = Path.Combine(projectRoot, item.BodyFile);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch
        {
            // Best-effort.
        }

        foreach (OutlineItem child in item.Children)
        {
            DeleteBodyFiles(projectRoot, child);
        }
    }

    private static OutlineNode ToNode(OutlineItem item)
    {
        return new OutlineNode
        {
            Id = item.Id,
            Title = item.Title,
            BodyFile = item.BodyFile,
            Children = item.Children.Select(ToNode).ToList()
        };
    }

    private static OutlineItem ToItem(OutlineNode node, OutlineItem? parent)
    {
        OutlineItem item = new OutlineItem
        {
            Id = node.Id,
            Title = node.Title,
            BodyFile = node.BodyFile,
            Parent = parent
        };

        foreach (OutlineNode child in node.Children)
        {
            item.Children.Add(ToItem(child, item));
        }

        return item;
    }
}
