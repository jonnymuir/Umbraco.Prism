namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Result of applying a <see cref="ProposalEnvelope"/> to an <see cref="AuthoredWorkflow"/>.
/// When <see cref="HasErrors"/> is true, <see cref="Updated"/> is the original (unchanged) workflow.
/// </summary>
public record PatchResult
{
    public required AuthoredWorkflow Updated { get; init; }
    public IReadOnlyList<ProjectionDiagnostic> Diagnostics { get; init; } = [];
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}
