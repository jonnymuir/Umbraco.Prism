using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// HTTP client interface for communicating with the external Business Application's workflow API.
/// The Business App is the authoritative source of workflow definitions and instance state.
/// Umbraco calls this to ask "what should the member do next?" and to submit collected data.
///
/// The authenticated member's Entra Bearer token is forwarded on every request.
/// The Business App derives tenant and user identity from the token — they are not sent in the body.
/// </summary>
public interface IBusinessAppWorkflowClient
{
    /// <summary>
    /// Asks the Business App for the current workflow state for the calling member,
    /// creating a new workflow instance if none exists.
    /// </summary>
    /// <param name="workflowKey">The workflow key configured on the Umbraco page (e.g. "community-enquiry").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A workflow response envelope describing the current step and what to render.</returns>
    Task<WorkflowResponseEnvelope> GetCurrentAsync(
        string workflowKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits collected field data to the Business App and asks it to advance the workflow.
    /// Returns the envelope for the next step (or completion).
    /// </summary>
    /// <param name="workflowKey">The workflow key configured on the Umbraco page.</param>
    /// <param name="instanceId">The running workflow instance identifier (from a previous GetCurrentAsync call).</param>
    /// <param name="action">The action being performed (e.g. "submit", "save-draft").</param>
    /// <param name="stateVersion">Expected state version for optimistic concurrency control.</param>
    /// <param name="fieldValues">Field values collected from the member.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A workflow response envelope describing the next step.</returns>
    Task<WorkflowResponseEnvelope> AdvanceAsync(
        string workflowKey,
        string instanceId,
        string action,
        int stateVersion,
        Dictionary<string, object?>? fieldValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a list of all workflow instances for the calling member.
    /// The BA filters by authenticated user identity (from the bearer token).
    /// </summary>
    Task<WorkflowInstanceListEnvelope> GetInstancesAsync(CancellationToken cancellationToken = default);
}
