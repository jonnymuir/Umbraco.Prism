using System.Text.Json;
using Wayfinder.Models.ServiceDesign;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.ProcessManager.Api;

/// <summary>Request body for the simulate endpoint — bundles the inputs a dry-run needs.</summary>
/// <param name="MockServiceInputs">
/// Mock values for any <c>source: "service"</c> calculation field, e.g.
/// <c>{ "member": { "age": 47 } }</c>. Omit if the definition has none.
/// </param>
public sealed record SimulateServiceBlueprintRequest(
    ServiceBlueprint Blueprint,
    IReadOnlyList<ProcessManagerSimulationStep> Steps,
    JsonElement? MockServiceInputs = null);
