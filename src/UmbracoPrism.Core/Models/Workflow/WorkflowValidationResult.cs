namespace UmbracoPrism.Core.Models.Workflow;

/// <summary>
/// Result of server-side structural validation of a workflow form submission.
/// </summary>
public record WorkflowValidationResult
{
    /// <summary>True when all fields passed validation.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Field-level errors keyed by field key. Multiple errors per field are collapsed
    /// to the first (most important) error — mirrors GDS validation pattern.
    /// </summary>
    public IReadOnlyDictionary<string, string> Errors { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Creates a passing result.</summary>
    public static WorkflowValidationResult Pass() =>
        new() { Errors = new Dictionary<string, string>() };

    /// <summary>Creates a failing result with the given errors.</summary>
    public static WorkflowValidationResult Fail(Dictionary<string, string> errors) =>
        new() { Errors = errors };
}
