using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Framework.Common.Logger;

namespace LlmIde.Wpf.Writer;

/// <summary>
/// Persisted main-window layout (resizable panel sizes).
/// </summary>
public sealed class WindowLayout
{
    /// <summary>Gets or sets the 프로젝트 column width in pixels.</summary>
    public double ProjectWidth { get; set; }

    /// <summary>Gets or sets the 대화 column width in pixels.</summary>
    public double ChatWidth { get; set; }

    /// <summary>Gets or sets the 목차 column width in pixels.</summary>
    public double OutlineWidth { get; set; }

    /// <summary>Gets or sets the 아티팩트 row height in pixels.</summary>
    public double ArtifactHeight { get; set; }

    /// <summary>Gets or sets the 대화 입력창 row height in pixels.</summary>
    public double ComposerHeight { get; set; }
}

/// <summary>
/// Reads and writes the main-window layout under <c>&lt;appRoot&gt;/config</c>.
/// Persistence is best-effort: failures never interrupt the UI.
/// </summary>
public sealed class WindowLayoutStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowLayoutStore"/> class.
    /// </summary>
    /// <param name="ideProgramRoot">The IDE program root directory.</param>
    public WindowLayoutStore(string ideProgramRoot)
    {
        Log.Ins.Debug("시작");
        filePath = Path.Combine(ideProgramRoot, "config", "window-layout.json");
    }

    /// <summary>
    /// Loads the saved layout, or null when none exists or the file is unreadable.
    /// </summary>
    /// <returns>The saved layout, or null.</returns>
    public WindowLayout? Load()
    {
        Log.Ins.Debug("시작");
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<WindowLayout>(File.ReadAllText(filePath));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the layout (best-effort).
    /// </summary>
    /// <param name="layout">The layout to save.</param>
    public void Save(WindowLayout layout)
    {
        Log.Ins.Debug("시작");
        try
        {
            string? directory = Path.GetDirectoryName(filePath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, JsonSerializer.Serialize(layout, Options));
        }
        catch
        {
            // Layout persistence is best-effort; ignore IO/serialization failures.
        }
    }
}
