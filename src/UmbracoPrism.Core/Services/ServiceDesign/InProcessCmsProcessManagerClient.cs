using UmbracoPrism.Core.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Engine.Abstractions;

namespace UmbracoPrism.Core.Services.ServiceDesign;

/// <summary>
/// <see cref="IBusinessAppProcessManagerClient"/> for CMS Workflow — the seam that lets
/// <c>PrismServiceRequestPageController{TViewModel}</c>'s entire existing
/// GET/POST/antiforgery/nonce/PRG stack run a CMS Workflow with zero new controller or
/// rendering code. Where <c>BusinessAppProcessManagerClient</c> makes an HTTP call to a remote
/// Business App, this calls <see cref="CmsProcessManager"/> directly in-process.
/// </summary>
/// <remarks>
/// Registered as a keyed service (key <c>"cms"</c>) so the default, unkeyed
/// <see cref="IBusinessAppProcessManagerClient"/> registration used by every business-workflow demo
/// page is untouched.
/// </remarks>
public sealed class InProcessCmsProcessManagerClient(
    CmsProcessManager engine,
    CmsServiceRequestVisitorIdentityResolver identityResolver) : IBusinessAppProcessManagerClient
{
    public Task<ServiceRequestResponseEnvelope> GetCurrentAsync(
        string blueprintKey,
        string? instanceId = null,
        string? action = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _) = identityResolver.Resolve();
        return Task.FromResult(
            engine.GetCurrent(blueprintKey, tenantId, userId, CmsQueue.AccessProfile, instanceId, action));
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
            engine.Advance(instanceId, tenantId, userId, CmsQueue.AccessProfile, action, stateVersion, fieldValues));
    }

    public Task<ServiceRequestListEnvelope> GetInstancesAsync(
        bool allowRefreshRetry = true,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _) = identityResolver.Resolve();
        return Task.FromResult(engine.GetInstances(tenantId, userId));
    }
}
