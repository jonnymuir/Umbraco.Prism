using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.Shared.Models.Workflow;

/// <summary>
/// JSON-deserialized shape of a v2.0 workflow definition seed file.
/// Uses polymorphic component hierarchy with type discriminator for all components.
/// </summary>
public record WorkflowDefinitionFileV2
{
    /// <summary>Schema version identifier (must be "2.0" for v2 definitions).</summary>
    public string SchemaVersion { get; init; } = "2.0";

    /// <summary>The unique identifier for this workflow definition (e.g. "retirement-quote").</summary>
    public string DefinitionKey { get; init; } = "";

    /// <summary>User-facing display name for the workflow.</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Version number of the definition (for tracking schema evolution).</summary>
    public int Version { get; init; }

    /// <summary>The state key that instances start in when first created.</summary>
    public string InitialState { get; init; } = "";

    /// <summary>Instance creation policy: "single" (reuse existing), "multiple" (always create new), "prompt" (ask user).</summary>
    public string InstancePolicy { get; init; } = "single";

    /// <summary>All states defined in this workflow.</summary>
    public IReadOnlyList<StepDefinitionV2> States { get; init; } = Array.Empty<StepDefinitionV2>();

    /// <summary>All state transitions (edges) defined in this workflow.</summary>
    public IReadOnlyList<WorkflowTransitionFile> Transitions { get; init; } = Array.Empty<WorkflowTransitionFile>();
}

/// <summary>
/// JSON-deserialized shape of a workflow state within a v2.0 definition.
/// Describes what to collect/display when the instance reaches this state using polymorphic components.
/// </summary>
public record StepDefinitionV2
{
    /// <summary>The unique identifier for this state within the workflow (e.g. "collect-details").</summary>
    public string StateKey { get; init; } = "";

    /// <summary>User-facing display name for this state.</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Polymorphic components to render within this step.</summary>
    public IReadOnlyList<PrismComponent> Components { get; init; } = Array.Empty<PrismComponent>();
}
