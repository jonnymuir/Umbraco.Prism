using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Editor-native representation of a workflow — the human and agent authoring source of truth.
/// Never loaded directly by the Prism runtime; compiled to <see cref="UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile"/>
/// by <see cref="IWorkflowProjector"/>.
/// </summary>
public record AuthoredWorkflow
{
    /// <summary>
    /// Stable surrogate identifier. Unlike <see cref="DefinitionKey"/>, this is never repurposed
    /// even if the key is renamed (migration scenario).
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Slug that maps to <c>WorkflowDefinitionFile.DefinitionKey</c> on projection.
    /// Once set, must not change without a migration step.
    /// </summary>
    [JsonPropertyName("definitionKey")]
    public required string DefinitionKey { get; init; }

    /// <summary>Human-readable display name. Maps to <c>WorkflowDefinitionFile.DisplayName</c>.</summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    /// <summary>Monotonically increasing version. Incremented by <c>ApplyPatch</c> on each committed edit.</summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    /// <summary>Optional free-text description of the workflow's purpose.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Authored schema version (separate from business version). Used for migration guards.</summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>
    /// The <see cref="AuthoredStage.StageKey"/> of the stage that becomes <c>WorkflowDefinitionFile.InitialState</c>.
    /// Must reference a stage in <see cref="Stages"/>.
    /// </summary>
    [JsonPropertyName("initialStageKey")]
    public required string InitialStageKey { get; init; }

    /// <summary>Instance creation policy forwarded verbatim to the runtime.</summary>
    [JsonPropertyName("instancePolicy")]
    public string InstancePolicy { get; init; } = "single";

    /// <summary>All stages in this workflow. Order is informational; graph edges define execution order.</summary>
    [JsonPropertyName("stages")]
    public IReadOnlyList<AuthoredStage> Stages { get; init; } = [];

    /// <summary>Named lanes used to share assignment data across stages and gateways.</summary>
    [JsonPropertyName("lanes")]
    public IReadOnlyList<AuthoredLane> Lanes { get; init; } = [];

    /// <summary>Gateway definitions that own all authored routing between stages.</summary>
    [JsonPropertyName("gateways")]
    public IReadOnlyList<AuthoredGateway> Gateways { get; init; } = [];

    /// <summary>Named handoff boundaries between stages, used as agent insertion points.</summary>
    [JsonPropertyName("handoffs")]
    public IReadOnlyList<AuthoredHandoff> Handoffs { get; init; } = [];

    /// <summary>Reusable parameter schema definitions referenced by authored actions.</summary>
    [JsonPropertyName("parameterSchemas")]
    public IReadOnlyList<AuthoredParameterSchema> ParameterSchemas { get; init; } = [];

    /// <summary>Arbitrary string key–value metadata (e.g. owner, service-area tags).</summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>Editor comment for the current revision. Stripped during projection.</summary>
    [JsonPropertyName("authorNote")]
    public string? AuthorNote { get; init; }
}
