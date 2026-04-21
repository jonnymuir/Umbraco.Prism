using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Runtime state for a workflow instance held in-memory by the Business App.
/// A new instance is created the first time a user accesses a workflow,
/// and it persists (in-memory) until the application restarts.
/// </summary>
public record WorkflowInstanceState
{
    /// <summary>Unique identifier for this instance (a GUID).</summary>
    public string InstanceId { get; init; } = "";
    /// <summary>The workflow key this instance belongs to (from the definition).</summary>
    public string WorkflowKey { get; init; } = "";
    /// <summary>The tenant ID this instance belongs to.</summary>
    public string TenantId { get; init; } = "";
    /// <summary>The user ID who owns this instance.</summary>
    public string UserId { get; init; } = "";
    /// <summary>The current state key of this instance.</summary>
    public string CurrentState { get; init; } = "";
    /// <summary>Optimistic concurrency version; incremented on each state change.</summary>
    public int StateVersion { get; init; }
    /// <summary>When this instance was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>When this instance was last modified.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
    /// <summary>Field values collected from the user during this workflow run.</summary>
    public Dictionary<string, object?> FieldValues { get; init; } = new();
}
