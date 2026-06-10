using System.Text.Json;
using LlmIde.Core.Projects;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Loads the combined prompt templates from a project policy file, seeding it from the program
/// template on first use (mirrors the rule stores). Read fresh each call so edits take effect live.
/// </summary>
public sealed class FilePromptStore : IPromptStore
{
    /// <summary>
    /// The IDE program root containing policy templates.
    /// </summary>
    private readonly string programRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePromptStore"/> class.
    /// </summary>
    /// <param name="programRoot">The IDE program root.</param>
    public FilePromptStore(string? programRoot = null)
    {
        this.programRoot = Path.GetFullPath(programRoot ?? AppContext.BaseDirectory);
    }

    /// <summary>
    /// Loads all prompt templates for a project.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The prompt templates keyed by identifier (empty when none configured).</returns>
    public IReadOnlyDictionary<string, string> Load(string projectRoot)
    {
        // Policies are common to all projects: read from the program's /policies folder.
        string path = Path.Combine(programRoot, LlmIdeLayout.PoliciesDirectoryName, LlmIdeLayout.PromptsFileName);

        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            string json = File.ReadAllText(path);
            Dictionary<string, string>? prompts = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return prompts ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // A malformed file must not break chat; fall back to built-in defaults at the call site.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
