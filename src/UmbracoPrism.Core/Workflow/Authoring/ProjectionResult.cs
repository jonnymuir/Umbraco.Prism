using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>
/// The output of a successful <see cref="IWorkflowProjector.Project"/> call.
/// </summary>
public record ProjectionResult
{
    /// <summary>The compiled runtime definition file, ready for use by the Prism runtime.</summary>
    public required WorkflowDefinitionFile File { get; init; }

    /// <summary>
    /// Lowercase hex-encoded SHA-256 of the canonical JSON serialization of <see cref="File"/>.
    /// Identical inputs produce identical checksums across invocations and platforms.
    /// </summary>
    public required string Checksum { get; init; }

    /// <summary>Diagnostics produced during projection. Check <see cref="HasErrors"/> before consuming <see cref="File"/>.</summary>
    public IReadOnlyList<ProjectionDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>Returns true if any diagnostic has <see cref="DiagnosticSeverity.Error"/> severity.</summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}
