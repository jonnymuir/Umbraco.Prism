using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Validates a workflow form submission against its authoritative field definitions.
/// Checks field key whitelist, required, type coercion, options whitelist, and constraints.
/// </summary>
public interface IWorkflowFieldValidator
{
    /// <summary>
    /// Validates the submitted form values against the step's authoritative field definitions.
    /// </summary>
    /// <param name="authoritative">Field definitions from the nonce cache (server-authoritative).</param>
    /// <param name="submitted">Form values submitted by the client, keyed by field key.</param>
    WorkflowValidationResult Validate(
        IReadOnlyList<FieldRenderPayload> authoritative,
        IReadOnlyDictionary<string, string> submitted);
}
