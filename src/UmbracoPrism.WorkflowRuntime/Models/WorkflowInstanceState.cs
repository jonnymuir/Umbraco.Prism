namespace UmbracoPrism.WorkflowRuntime.Models;

/// <summary>
/// Runtime state for a workflow instance held in-memory by the host application.
/// </summary>
public record WorkflowInstanceState
{
    public string InstanceId { get; init; } = "";

    public string WorkflowKey { get; init; } = "";

    public string TenantId { get; init; } = "";

    public string UserId { get; init; } = "";

    /// <summary>
    /// Primary cursor position. In single-lane workflows this is the only position.
    /// In multi-cursor mode this reflects the first active stage cursor for backward-compatibility
    /// with API consumers that read only this field.
    /// </summary>
    public string CurrentState { get; init; } = "";

    public int StateVersion { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public Dictionary<string, object?> FieldValues { get; init; } = new();

    /// <summary>
    /// Active cursors in a multi-lane workflow. Empty for single-lane instances.
    /// Each cursor tracks its own lane and current node position independently.
    /// </summary>
    public IReadOnlyList<WorkflowCursor> Cursors { get; init; } = [];

    /// <summary>
    /// Join-gateway arrival records. Key = gateway key; value = list of cursor IDs that have arrived.
    /// The engine appends to this set as cursors reach the join and removes entries when the join releases.
    /// Not exposed in the public runtime contract.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> JoinArrivals { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}
