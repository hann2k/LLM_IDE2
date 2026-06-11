using System.Text.Json;
using LlmIde.Core.Projects;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure.Projects;

/// <summary>
/// Loads the conversation importance scoring rule from the common prompts.json (importance_rule key).
/// </summary>
public sealed class FileImportanceRuleStore : IImportanceRuleStore
{
    /// <summary>
    /// The IDE program root containing policy templates.
    /// </summary>
    private readonly string programRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileImportanceRuleStore"/> class.
    /// </summary>
    /// <param name="programRoot">The IDE program root.</param>
    public FileImportanceRuleStore(string? programRoot = null)
    {
        Log.Ins.Debug("시작");
        this.programRoot = Path.GetFullPath(programRoot ?? AppContext.BaseDirectory);
    }

    /// <summary>
    /// Loads the importance scoring rule.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The importance scoring rule content.</returns>
    public string Load(string projectRoot)
    {
        // The importance rule now lives in the common prompts.json under the "importance_rule" key.
        Log.Ins.Debug("시작");
        string path = Path.Combine(programRoot, LlmIdeLayout.PoliciesDirectoryName, LlmIdeLayout.PromptsFileName);

        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            string json = File.ReadAllText(path);
            Dictionary<string, string>? prompts = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (prompts is not null
                && prompts.TryGetValue(PromptKeys.ImportanceRule, out string? rule)
                && !string.IsNullOrWhiteSpace(rule))
            {
                return rule;
            }
        }
        catch (JsonException)
        {
            // A malformed prompts file must not break chat; importance simply defaults to 0.
        }

        return string.Empty;
    }
}
