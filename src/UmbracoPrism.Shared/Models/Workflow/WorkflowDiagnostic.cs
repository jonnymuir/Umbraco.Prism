namespace UmbracoPrism.Shared.Models.Workflow;

/// <summary>Severity of a <see cref="WorkflowDiagnostic"/>.</summary>
public enum WorkflowDiagnosticSeverity
{
    /// <summary>Blocks <c>IsValid</c>/save — the workflow is structurally or semantically broken.</summary>
    Error,

    /// <summary>Does not block <c>IsValid</c> — something couldn't be statically verified.</summary>
    Warning
}

/// <summary>
/// A single authoring-time diagnostic against a <see cref="WorkflowDefinitionFile"/> — gateway
/// routing, calculation/showWhen expression errors, or unverifiable service-sourced fields.
/// Distinct from <see cref="UmbracoPrism.Core.Models.Workflow.WorkflowProblem"/>, which addresses
/// a runtime field-submission problem by field key, not a document path.
/// </summary>
/// <param name="Code">Machine-readable diagnostic kind, e.g. "CALC_FIELD_ERROR", "SHOW_WHEN_EVAL_ERROR".</param>
/// <param name="Path">
/// Document path to the offending element, e.g. <c>states.review.components[2].showWhen</c> or
/// <c>calculations.fields.member</c>. Uses stable keys (state key, field name) over array indices
/// where a key exists.
/// </param>
/// <param name="Message">Human-readable explanation, safe to surface directly to an author.</param>
public sealed record WorkflowDiagnostic(
    string Code,
    string Path,
    string Message,
    WorkflowDiagnosticSeverity Severity = WorkflowDiagnosticSeverity.Error);
