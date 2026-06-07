namespace LlmIde.Core.Projects;

/// <summary>
/// Manages project state.
/// </summary>
public sealed class ProjectStateService
{
    /// <summary>
    /// The project state store.
    /// </summary>
    private readonly IProjectStateStore projectStateStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectStateService"/> class.
    /// </summary>
    /// <param name="projectStateStore">The project state store.</param>
    public ProjectStateService(IProjectStateStore projectStateStore)
    {
        this.projectStateStore = projectStateStore;
    }

    /// <summary>
    /// Gets the current project state.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The project state.</returns>
    public ProjectState Get(string projectRoot)
    {
        return projectStateStore.Load(projectRoot);
    }

    /// <summary>
    /// Updates scalar project state fields.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="stage">The replacement stage.</param>
    /// <param name="currentTask">The replacement current task.</param>
    /// <param name="lastDecision">The replacement last decision.</param>
    /// <returns>The updated project state.</returns>
    public ProjectState Set(
        string projectRoot,
        string? stage,
        string? currentTask,
        string? lastDecision)
    {
        ProjectState state = projectStateStore.Load(projectRoot);

        if (stage is not null)
        {
            state.Stage = stage;
        }

        if (currentTask is not null)
        {
            state.CurrentTask = currentTask;
        }

        if (lastDecision is not null)
        {
            state.LastDecision = lastDecision;
        }

        projectStateStore.Save(projectRoot, state);
        return state;
    }

    /// <summary>
    /// Adds an item to a project state list.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="listName">The list name.</param>
    /// <param name="item">The item to add.</param>
    /// <returns>The updated project state.</returns>
    public ProjectState AddItem(string projectRoot, string listName, string item)
    {
        if (string.IsNullOrWhiteSpace(item))
        {
            throw new InvalidOperationException("프로젝트 상태 항목은 필수입니다.");
        }

        ProjectState state = projectStateStore.Load(projectRoot);
        List<string> list = GetList(state, listName);

        if (!list.Any(existingItem => string.Equals(existingItem, item, StringComparison.Ordinal)))
        {
            list.Add(item);
        }

        projectStateStore.Save(projectRoot, state);
        return state;
    }

    /// <summary>
    /// Removes an item from a project state list.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="listName">The list name.</param>
    /// <param name="item">The item to remove.</param>
    /// <returns>True when the item was removed.</returns>
    public bool RemoveItem(string projectRoot, string listName, string item)
    {
        ProjectState state = projectStateStore.Load(projectRoot);
        List<string> list = GetList(state, listName);
        int removedCount = list.RemoveAll(existingItem =>
            string.Equals(existingItem, item, StringComparison.Ordinal));

        if (removedCount > 0)
        {
            projectStateStore.Save(projectRoot, state);
        }

        return removedCount > 0;
    }

    /// <summary>
    /// Gets a mutable state list by CLI list name.
    /// </summary>
    /// <param name="state">The project state.</param>
    /// <param name="listName">The list name.</param>
    /// <returns>The mutable list.</returns>
    private static List<string> GetList(ProjectState state, string listName)
    {
        return listName switch
        {
            "completed" => state.CompletedItems,
            "in-progress" => state.InProgressItems,
            "next-action" => state.NextActions,
            "blocker" => state.Blockers,
            _ => throw new InvalidOperationException($"알 수 없는 프로젝트 상태 목록입니다: {listName}")
        };
    }
}
