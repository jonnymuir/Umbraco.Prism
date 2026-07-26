using UmbracoPrism.Core.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// HTTP client interface for communicating with the external Business Application's workflow API.
/// The Business App is the authoritative source of workflow definitions and instance state.
/// Umbraco calls this to ask "what should the member do next?" and to submit collected data.
///
/// The authenticated member's Entra Bearer token is forwarded on every request.
/// The Business App derives tenant and user identity from the token — they are not sent in the body.
/// </summary>
public interface IBusinessAppProcessManagerClient
{
    /// <summary>
    /// Asks the Business App for the current workflow state for the calling member,
    /// creating a new workflow instance if none exists.
    /// </summary>
    /// <param name="blueprintKey">The workflow key configured on the Umbraco page (e.g. "community-enquiry").</param>
    /// <param name="instanceId">Optional specific instance ID to resume.</param>
    /// <param name="action">Optional action: "start-new" or "resume" (used by "prompt" policy).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A workflow response envelope describing the current step and what to render.</returns>
    Task<ServiceRequestResponseEnvelope> GetCurrentAsync(
        string blueprintKey,
        string? instanceId = null,
        string? action = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits collected field data to the Business App and asks it to advance the workflow.
    /// Returns the envelope for the next step (or completion).
    /// </summary>
    /// <param name="blueprintKey">The workflow key configured on the Umbraco page.</param>
    /// <param name="instanceId">The running workflow instance identifier (from a previous GetCurrentAsync call).</param>
    /// <param name="action">The action being performed (e.g. "submit", "save-draft").</param>
    /// <param name="stateVersion">Expected state version for optimistic concurrency control.</param>
    /// <param name="fieldValues">Field values collected from the member.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A workflow response envelope describing the next step.</returns>
    Task<ServiceRequestResponseEnvelope> AdvanceAsync(
        string blueprintKey,
        string instanceId,
        string action,
        int stateVersion,
        Dictionary<string, object?>? fieldValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a list of all workflow instances for the calling member.
    /// The BA filters by authenticated user identity (from the bearer token).
    /// </summary>
    /// <param name="allowRefreshRetry">
    /// When <see langword="true"/>, a downstream <c>401</c> triggers one forced
    /// refresh-token exchange and retry. Page-render callers can disable this to
    /// avoid mutating the member cookie during route rendering.
    /// </param>
    Task<ServiceRequestListEnvelope> GetInstancesAsync(
        bool allowRefreshRetry = true,
        CancellationToken cancellationToken = default);
}
