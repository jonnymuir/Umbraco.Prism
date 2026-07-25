using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// <see cref="IBusinessAppWorkflowClient"/> for CMS Workflow — the seam that lets
/// <c>PrismWorkflowPageController{TViewModel}</c>'s entire existing
/// GET/POST/antiforgery/nonce/PRG stack run a CMS Workflow with zero new controller or
/// rendering code. Where <c>BusinessAppWorkflowClient</c> makes an HTTP call to a remote
/// Business App, this calls <see cref="CmsWorkflowEngine"/> directly in-process.
/// </summary>
/// <remarks>
/// Registered as a keyed service (key <c>"cms"</c>) so the default, unkeyed
/// <see cref="IBusinessAppWorkflowClient"/> registration used by every business-workflow demo
/// page is untouched.
/// </remarks>
public sealed class InProcessCmsWorkflowClient(
    CmsWorkflowEngine engine,
    CmsWorkflowVisitorIdentityResolver identityResolver) : IBusinessAppWorkflowClient
{
    public Task<WorkflowResponseEnvelope> GetCurrentAsync(
        string workflowKey,
        string? instanceId = null,
        string? action = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _) = identityResolver.Resolve();
        return Task.FromResult(
            engine.GetCurrent(workflowKey, tenantId, userId, CmsWorkflowQueue.AccessProfile, instanceId, action));
    }

    public Task<WorkflowResponseEnvelope> AdvanceAsync(
        string workflowKey,
        string instanceId,
        string action,
        int stateVersion,
        Dictionary<string, object?>? fieldValues = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _) = identityResolver.Resolve();
        return Task.FromResult(
            engine.Advance(instanceId, tenantId, userId, CmsWorkflowQueue.AccessProfile, action, stateVersion, fieldValues));
    }

    public Task<WorkflowInstanceListEnvelope> GetInstancesAsync(
        bool allowRefreshRetry = true,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId, _) = identityResolver.Resolve();
        return Task.FromResult(engine.GetInstances(tenantId, userId));
    }
}
