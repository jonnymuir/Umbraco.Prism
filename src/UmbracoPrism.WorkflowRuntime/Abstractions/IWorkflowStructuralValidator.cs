using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowRuntime.Abstractions;

/// <summary>
/// A host-supplied structural constraint, checked alongside <see cref="Services.WorkflowAuthoringService"/>'s
/// own generic validation (gateway routing, data-display bindings, calculations, showWhen) — for
/// rules the shared runtime has no business knowing about (e.g. a host that only ever wants a
/// single well-known queue). Registered via DI; every registered validator runs on every
/// <c>Validate</c>/<c>SaveAsync</c> call, so a host adds one per constraint rather than
/// subclassing or forking the authoring service.
/// </summary>
public interface IWorkflowStructuralValidator
{
    IEnumerable<WorkflowDiagnostic> Validate(WorkflowDefinitionFile workflow);
}
