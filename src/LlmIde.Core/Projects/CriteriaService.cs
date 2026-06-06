namespace LlmIde.Core.Projects;

/// <summary>
/// Manages project criteria.
/// </summary>
public sealed class CriteriaService
{
    /// <summary>
    /// The criteria store.
    /// </summary>
    private readonly ICriteriaStore criteriaStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CriteriaService"/> class.
    /// </summary>
    /// <param name="criteriaStore">The criteria store.</param>
    public CriteriaService(ICriteriaStore criteriaStore)
    {
        this.criteriaStore = criteriaStore;
    }

    /// <summary>
    /// Adds a criterion.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="title">The criterion title.</param>
    /// <param name="description">The criterion description.</param>
    /// <param name="priority">The criterion priority.</param>
    /// <returns>The created criterion.</returns>
    public Criterion Add(string projectRoot, string title, string description, string priority)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Criterion title is required.");
        }

        List<Criterion> criteria = criteriaStore.Load(projectRoot).ToList();
        Criterion criterion = new Criterion
        {
            CriterionId = $"crt_{Guid.NewGuid():N}",
            Title = title,
            Description = description,
            Priority = string.IsNullOrWhiteSpace(priority) ? "normal" : priority,
            Status = "active"
        };
        criteria.Add(criterion);
        criteriaStore.Save(projectRoot, criteria);
        return criterion;
    }

    /// <summary>
    /// Lists criteria.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The project criteria.</returns>
    public IReadOnlyList<Criterion> List(string projectRoot)
    {
        return criteriaStore.Load(projectRoot);
    }

    /// <summary>
    /// Updates a criterion.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="criterionId">The criterion identifier.</param>
    /// <param name="title">The replacement title.</param>
    /// <param name="description">The replacement description.</param>
    /// <param name="priority">The replacement priority.</param>
    public void Update(
        string projectRoot,
        string criterionId,
        string? title,
        string? description,
        string? priority)
    {
        List<Criterion> criteria = criteriaStore.Load(projectRoot).ToList();
        Criterion criterion = GetRequired(criteria, criterionId);

        if (title is not null)
        {
            criterion.Title = title;
        }

        if (description is not null)
        {
            criterion.Description = description;
        }

        if (priority is not null)
        {
            criterion.Priority = priority;
        }

        criteriaStore.Save(projectRoot, criteria);
    }

    /// <summary>
    /// Removes a criterion.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="criterionId">The criterion identifier.</param>
    /// <returns>True when the criterion was removed.</returns>
    public bool Remove(string projectRoot, string criterionId)
    {
        List<Criterion> criteria = criteriaStore.Load(projectRoot).ToList();
        int removedCount = criteria.RemoveAll(criterion =>
            string.Equals(criterion.CriterionId, criterionId, StringComparison.Ordinal));

        if (removedCount > 0)
        {
            criteriaStore.Save(projectRoot, criteria);
        }

        return removedCount > 0;
    }

    /// <summary>
    /// Activates a criterion.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="criterionId">The criterion identifier.</param>
    public void Activate(string projectRoot, string criterionId)
    {
        SetStatus(projectRoot, criterionId, "active");
    }

    /// <summary>
    /// Deactivates a criterion.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="criterionId">The criterion identifier.</param>
    public void Deactivate(string projectRoot, string criterionId)
    {
        SetStatus(projectRoot, criterionId, "inactive");
    }

    /// <summary>
    /// Lists active criteria.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The active criteria.</returns>
    public IReadOnlyList<Criterion> ListActive(string projectRoot)
    {
        return criteriaStore.Load(projectRoot)
            .Where(criterion => string.Equals(criterion.Status, "active", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Sets a criterion status.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <param name="criterionId">The criterion identifier.</param>
    /// <param name="status">The status.</param>
    private void SetStatus(string projectRoot, string criterionId, string status)
    {
        List<Criterion> criteria = criteriaStore.Load(projectRoot).ToList();
        Criterion criterion = GetRequired(criteria, criterionId);
        criterion.Status = status;
        criteriaStore.Save(projectRoot, criteria);
    }

    /// <summary>
    /// Gets a criterion or fails.
    /// </summary>
    /// <param name="criteria">The criteria.</param>
    /// <param name="criterionId">The criterion identifier.</param>
    /// <returns>The criterion.</returns>
    private static Criterion GetRequired(List<Criterion> criteria, string criterionId)
    {
        Criterion? criterion = criteria.FirstOrDefault(candidate =>
            string.Equals(candidate.CriterionId, criterionId, StringComparison.Ordinal));

        if (criterion is null)
        {
            throw new InvalidOperationException($"Criterion does not exist: {criterionId}");
        }

        return criterion;
    }
}
