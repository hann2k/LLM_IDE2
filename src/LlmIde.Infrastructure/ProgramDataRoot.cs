namespace LlmIde.Infrastructure;

/// <summary>
/// Resolves the per-user program data root (%LOCALAPPDATA%\LlmIde2) shared by all apps and the CLI.
/// User data — project registry, settings, policies, project folders, logs — lives under the data
/// root so it survives installs to read-only locations (e.g. Program Files). The common policy
/// files under the data root are copied there unconditionally by the build
/// (Directory.Build.props CopyPoliciesToProgramDataRoot target, DEC-083); there is no runtime seeding.
/// </summary>
public static class ProgramDataRoot
{
    /// <summary>
    /// The data root folder name under %LOCALAPPDATA%.
    /// </summary>
    private const string FolderName = "LlmIde2";

    /// <summary>
    /// Ensures the data root exists and returns its path.
    /// </summary>
    /// <returns>The absolute data root path (%LOCALAPPDATA%\LlmIde2).</returns>
    public static string EnsureAndGet()
    {
        // 여기는 예외로 로깅하면 안된다. (로그 디렉터리 설정 전에 호출됨)
        // Log.Ins.Debug("시작");
        string dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName);

        Directory.CreateDirectory(dataRoot);
        return dataRoot;
    }

    /// <summary>
    /// Ensures the log directory under the data root exists and returns its path.
    /// </summary>
    /// <returns>The absolute log directory path (%LOCALAPPDATA%\LlmIde2\Log).</returns>
    public static string GetLogDir()
    {
        // 여기는 예외로 로깅하면 안된다. (로그 디렉터리 설정 전에 호출됨)
        string logRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName, "Log");

        Directory.CreateDirectory(logRoot);
        return logRoot;
    }
}
