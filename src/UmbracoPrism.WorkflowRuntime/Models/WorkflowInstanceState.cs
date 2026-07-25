using UmbracoPrism.Shared.Services.Calculations;

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
    /// True when <see cref="UserId"/> identifies a signed-in user rather than an anonymous
    /// visitor's correlation cookie. A store may use this to apply a longer-lived (or
    /// unbounded) retention policy than it would for an anonymous session — see
    /// <c>UmbracoCmsWorkflowInstanceStore</c>, whose 30-minute sliding expiry is skipped
    /// entirely for authenticated instances.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Primary cursor position. In single-queue workflows this is the only position.
    /// In multi-cursor mode this reflects the first active stage cursor for backward-compatibility
    /// with API consumers that read only this field.
    /// </summary>
    public string CurrentState { get; init; } = "";

    public int StateVersion { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public Dictionary<string, object?> FieldValues { get; init; } = new();

    /// <summary>
    /// Active cursors in a multi-queue workflow. Empty for single-queue instances.
    /// Each cursor tracks its own queue and current node position independently.
    /// </summary>
    public IReadOnlyList<WorkflowCursor> Cursors { get; init; } = [];

    /// <summary>
    /// Join-gateway arrival records. Key = gateway key; value = list of cursor IDs that have arrived.
    /// The engine appends to this set as cursors reach the join and removes entries when the join releases.
    /// Not exposed in the public runtime contract.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> JoinArrivals { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// The most recently computed calculation result for this instance's current state, if
    /// its definition has a calculations block and it last evaluated cleanly. Not part of the
    /// public runtime contract — internal bookkeeping so a composed caller (e.g. the
    /// simulation runner) can read raw calculated values without duplicating evaluation.
    /// </summary>
    public CalculationResult? LastCalculationResult { get; init; }
}
