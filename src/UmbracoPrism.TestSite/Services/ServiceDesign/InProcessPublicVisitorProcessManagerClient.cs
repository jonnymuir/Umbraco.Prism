using Wayfinder.Models.ServiceDesign;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// <see cref="IBusinessAppProcessManagerClient"/> for TestSite's own public service request demo —
/// the seam that lets <c>ServiceRequestPageController{TViewModel}</c>'s entire existing
/// GET/POST/antiforgery/nonce/PRG stack run this demo with zero new controller or rendering
/// code. Where <c>BusinessAppProcessManagerClient</c> makes an HTTP call to a remote Business
/// App, this calls <see cref="UmbracoProcessManagerEngine"/> directly in-process.
/// </summary>
/// <remarks>
/// Registered as a keyed service (key <c>"public"</c>) so the default, unkeyed
/// <see cref="IBusinessAppProcessManagerClient"/> registration used by every business-workflow demo
/// page is untouched.
/// </remarks>
public sealed class InProcessPublicVisitorProcessManagerClient(
    UmbracoProcessManagerEngine engine,
    PublicVisitorIdentityResolver identityResolver) : IBusinessAppProcessManagerClient
{
    public Task<ServiceRequestResponseEnvelope> GetCurrentAsync(
        string blueprintKey,
        string? instanceId = null,
        string? action = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _) = identityResolver.Resolve();
        return Task.FromResult(
            engine.GetCurrent(blueprintKey, tenantId, userId, PublicVisitorQueue.AccessProfile, instanceId, action));
    }

    public Task<ServiceRequestResponseEnvelope> AdvanceAsync(
        string blueprintKey,
        string instanceId,
        string action,
        int stateVersion,
        Dictionary<string, object?>? fieldValues = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _) = identityResolver.Resolve();
        return Task.FromResult(
            engine.Advance(instanceId, tenantId, userId, PublicVisitorQueue.AccessProfile, action, stateVersion, fieldValues));
    }

    public Task<ServiceRequestListEnvelope> GetInstancesAsync(
        bool allowRefreshRetry = true,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _) = identityResolver.Resolve();
        return Task.FromResult(engine.GetInstances(tenantId, userId));
    }
}
