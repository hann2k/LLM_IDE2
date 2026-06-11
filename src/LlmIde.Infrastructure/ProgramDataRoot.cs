using LlmIde.Core.Projects;
using Framework.Common.Logger;

namespace LlmIde.Infrastructure;

/// <summary>
/// Resolves the per-user program data root (%LOCALAPPDATA%\LlmIde2) shared by all apps and the CLI.
/// The application binaries (and the shipped default policy templates next to the executable) are
/// read-only; user data — project registry, settings, policies, project folders, logs — lives under
/// the data root so it survives installs to read-only locations (e.g. Program Files).
/// </summary>
public static class ProgramDataRoot
{
    /// <summary>
    /// The data root folder name under %LOCALAPPDATA%.
    /// </summary>
    private const string FolderName = "LlmIde2";

    /// <summary>
    /// Ensures the data root exists, seeds the default policies on first run, and returns its path.
    /// </summary>
    /// <returns>The absolute data root path (%LOCALAPPDATA%\LlmIde2).</returns>
    public static string EnsureAndGet()
    {
        Log.Ins.Debug("시작");
        string dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName);

        Directory.CreateDirectory(dataRoot);
        SeedPolicies(dataRoot);
        return dataRoot;
    }

    /// <summary>
    /// Copies any shipped default policy file (next to the executable) into the data root when it is
    /// missing there. Existing files are never overwritten, so user edits and updates are preserved.
    /// </summary>
    /// <param name="dataRoot">The data root path.</param>
    private static void SeedPolicies(string dataRoot)
    {
        Log.Ins.Debug("시작");
        string sourcePolicies = Path.Combine(AppContext.BaseDirectory, LlmIdeLayout.PoliciesDirectoryName);

        if (!Directory.Exists(sourcePolicies))
        {
            return;
        }

        string targetPolicies = Path.Combine(dataRoot, LlmIdeLayout.PoliciesDirectoryName);
        Directory.CreateDirectory(targetPolicies);

        foreach (string source in Directory.GetFiles(sourcePolicies))
        {
            string destination = Path.Combine(targetPolicies, Path.GetFileName(source));

            if (!File.Exists(destination))
            {
                try
                {
                    File.Copy(source, destination);
                }
                catch
                {
                    // Seeding is best-effort; a missing policy falls back to built-in defaults.
                }
            }
        }
    }
}
