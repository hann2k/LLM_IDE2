using LlmIde.Core.Projects;
using LlmIde.Infrastructure.Projects;

namespace LlmIde.Tests;

/// <summary>
/// Provides a lightweight test runner for Phase 1 behavior.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs all tests.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public static int Main()
    {
        List<Action> tests =
        [
            ProjectInitializationCreatesMinimumMetadata,
            ProjectInitializationIsIdempotent,
            RecentProjectsPersistAndMoveLatestToTop,
            RemoveMissingProjectsPrunesDeletedDirectories
        ];

        foreach (Action test in tests)
        {
            test();
            Console.WriteLine($"passed: {test.Method.Name}");
        }

        Console.WriteLine("All tests passed.");
        return 0;
    }

    /// <summary>
    /// Verifies that project initialization creates the Phase 1 metadata layout.
    /// </summary>
    private static void ProjectInitializationCreatesMinimumMetadata()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.ApplicationDataRoot);
        string projectRoot = Path.Combine(workspace.Root, "project-a");

        ProjectInitializationResult result = initializer.Initialize(projectRoot);

        AssertTrue(result.Created, "Project should be newly created.");
        AssertFileExists(projectRoot, ".llmide/project.json");
        AssertFileExists(projectRoot, ".llmide/project-state.json");
        AssertFileExists(projectRoot, ".llmide/criteria.json");
        AssertFileExists(projectRoot, ".llmide/policies/system-rule.md");
        AssertFileExists(projectRoot, ".llmide/policies/context-policy.json");
        AssertFileExists(projectRoot, ".llmide/policies/provider-policy.json");
        AssertFileExists(projectRoot, ".llmide/conversations/messages.jsonl");
        AssertFileExists(projectRoot, ".llmide/conversations/requests.jsonl");
        AssertDirectoryExists(projectRoot, ".llmide/conversations/context-packages");
        AssertFileExists(projectRoot, ".llmide/settings/providers.json");
        AssertFileExists(projectRoot, ".llmide/artifacts/artifacts.index.json");
    }

    /// <summary>
    /// Verifies that initialization can be run repeatedly without changing the project id.
    /// </summary>
    private static void ProjectInitializationIsIdempotent()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.ApplicationDataRoot);
        string projectRoot = Path.Combine(workspace.Root, "project-a");

        ProjectInitializationResult firstResult = initializer.Initialize(projectRoot);
        ProjectInitializationResult secondResult = initializer.Initialize(projectRoot);

        AssertFalse(secondResult.Created, "Second initialization should open existing metadata.");
        AssertEqual(firstResult.ProjectInfo.ProjectId, secondResult.ProjectInfo.ProjectId, "Project id should remain stable.");
    }

    /// <summary>
    /// Verifies that recent projects are persisted and reordered when reopened.
    /// </summary>
    private static void RecentProjectsPersistAndMoveLatestToTop()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.ApplicationDataRoot);
        RecentProjectService recentProjects = CreateRecentProjectService(workspace.ApplicationDataRoot);
        string projectA = Path.Combine(workspace.Root, "project-a");
        string projectB = Path.Combine(workspace.Root, "project-b");

        initializer.Initialize(projectA);
        initializer.Initialize(projectB);
        recentProjects.RecordOpened(projectA, "project-a");

        IReadOnlyList<RecentProjectEntry> projects = CreateRecentProjectService(workspace.ApplicationDataRoot).List();

        AssertEqual(2, projects.Count, "Recent project count should be persisted.");
        AssertEqual(Path.GetFullPath(projectA), projects[0].Path, "Reopened project should be first.");
        AssertEqual(Path.GetFullPath(projectB), projects[1].Path, "Older project should move down.");
    }

    /// <summary>
    /// Verifies that missing project directories can be removed from the recent list.
    /// </summary>
    private static void RemoveMissingProjectsPrunesDeletedDirectories()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        ProjectInitializer initializer = CreateProjectInitializer(workspace.ApplicationDataRoot);
        RecentProjectService recentProjects = CreateRecentProjectService(workspace.ApplicationDataRoot);
        string projectRoot = Path.Combine(workspace.Root, "project-a");

        initializer.Initialize(projectRoot);
        Directory.Delete(projectRoot, true);

        int removedCount = recentProjects.RemoveMissing();
        IReadOnlyList<RecentProjectEntry> projects = recentProjects.List();

        AssertEqual(1, removedCount, "One missing project should be removed.");
        AssertEqual(0, projects.Count, "Recent project list should be empty.");
    }

    /// <summary>
    /// Creates a project initializer for tests.
    /// </summary>
    /// <param name="applicationDataRoot">The test application data root.</param>
    /// <returns>The project initializer.</returns>
    private static ProjectInitializer CreateProjectInitializer(string applicationDataRoot)
    {
        RecentProjectService recentProjects = CreateRecentProjectService(applicationDataRoot);
        return new ProjectInitializer(new JsonFileProjectStore(), recentProjects);
    }

    /// <summary>
    /// Creates a recent project service for tests.
    /// </summary>
    /// <param name="applicationDataRoot">The test application data root.</param>
    /// <returns>The recent project service.</returns>
    private static RecentProjectService CreateRecentProjectService(string applicationDataRoot)
    {
        return new RecentProjectService(new JsonRecentProjectStore(applicationDataRoot));
    }

    /// <summary>
    /// Asserts that a file exists below a project root.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="relativePath">The relative file path.</param>
    private static void AssertFileExists(string projectRoot, string relativePath)
    {
        string path = Path.Combine(projectRoot, relativePath);
        AssertTrue(File.Exists(path), $"Expected file to exist: {relativePath}");
    }

    /// <summary>
    /// Asserts that a directory exists below a project root.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="relativePath">The relative directory path.</param>
    private static void AssertDirectoryExists(string projectRoot, string relativePath)
    {
        string path = Path.Combine(projectRoot, relativePath);
        AssertTrue(Directory.Exists(path), $"Expected directory to exist: {relativePath}");
    }

    /// <summary>
    /// Asserts that a condition is true.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="message">The failure message.</param>
    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Asserts that a condition is false.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="message">The failure message.</param>
    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Asserts that two values are equal.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="message">The failure message.</param>
    private static void AssertEqual<TValue>(TValue expected, TValue actual, string message)
    {
        if (!EqualityComparer<TValue>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }
}

/// <summary>
/// Provides an isolated temporary workspace for tests.
/// </summary>
public sealed class TestWorkspace : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestWorkspace"/> class.
    /// </summary>
    /// <param name="root">The workspace root path.</param>
    private TestWorkspace(string root)
    {
        Root = root;
        ApplicationDataRoot = Path.Combine(root, "app-data");
    }

    /// <summary>
    /// Gets the workspace root path.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Gets the application data root path.
    /// </summary>
    public string ApplicationDataRoot { get; }

    /// <summary>
    /// Creates a new test workspace.
    /// </summary>
    /// <returns>The created test workspace.</returns>
    public static TestWorkspace Create()
    {
        string root = Path.Combine(Path.GetTempPath(), "LlmIde.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TestWorkspace(root);
    }

    /// <summary>
    /// Deletes the test workspace.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
