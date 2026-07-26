namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Result of applying a <see cref="ProposalEnvelope"/> to an <see cref="AuthoredServiceBlueprint"/>.
/// When <see cref="HasErrors"/> is true, <see cref="Updated"/> is the original (unchanged) blueprint.
/// </summary>
public record PatchResult
{
    public required AuthoredServiceBlueprint Updated { get; init; }
    public IReadOnlyList<ProjectionDiagnostic> Diagnostics { get; init; } = [];
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}
