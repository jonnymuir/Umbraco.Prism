namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>Severity level of a projection diagnostic message.</summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// A diagnostic message produced during projection of an <see cref="AuthoredWorkflow"/>.
/// Errors prevent a valid result from being emitted; warnings are informational.
/// </summary>
public record ProjectionDiagnostic
{
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Short machine-readable code (e.g. "PROJ001").</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable explanation.</summary>
    public required string Message { get; init; }

    /// <summary>The <see cref="AuthoredStage.StageKey"/> that triggered this diagnostic, if applicable.</summary>
    public string? StageKey { get; init; }
}
