using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.ProcessManager.Abstractions;

/// <summary>
/// A host-supplied structural constraint, checked alongside <see cref="Services.ServiceBlueprintAuthoringService"/>'s
/// own generic validation (gateway routing, data-display bindings, calculations, showWhen) — for
/// rules the shared runtime has no business knowing about (e.g. a host that only ever wants a
/// single well-known queue). Registered via DI; every registered validator runs on every
/// <c>Validate</c>/<c>SaveAsync</c> call, so a host adds one per constraint rather than
/// subclassing or forking the authoring service.
/// </summary>
public interface IServiceBlueprintStructuralValidator
{
    IEnumerable<ServiceBlueprintDiagnostic> Validate(ServiceBlueprint blueprint);
}
