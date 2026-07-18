using Microsoft.Extensions.Logging;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Models;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// The CMS Workflow implementation's own <see cref="IWorkflowRuntimeEngine"/> — a distinctly
/// named singleton so it's discoverable in DI registration/debugging, kept separate from any
/// business-app-hosted engine a host might also run (registered under the "cms" DI key). No
/// override logic lives here: <c>serviceInputsResolver</c> (the toolkit's existing extension
/// point for <c>source: "service"</c> calculation fields — see <see cref="WorkflowRuntimeEngine.ResolveServiceInputs"/>)
/// is supplied as a plain delegate at registration time, so a demo host (e.g. TestSite's
/// juggling-society membership lookup) needs no subclass of its own.
/// </summary>
public sealed class CmsWorkflowEngine(
    ILogger<CmsWorkflowEngine> logger,
    IWorkflowDefinitionStore definitionStore,
    IWorkflowContentSanitizer sanitizer,
    IWorkflowInstanceStore instanceStore,
    Func<WorkflowInstanceState, WorkflowDefinitionFile, StepDefinition, IReadOnlyDictionary<string, object?>?>? serviceInputsResolver = null)
    : WorkflowRuntimeEngine(logger, definitionStore, sanitizer, serviceInputsResolver, instanceStore);
