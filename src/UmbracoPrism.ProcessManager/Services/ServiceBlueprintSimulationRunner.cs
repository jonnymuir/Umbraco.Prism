using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Services.Calculations;
using UmbracoPrism.ProcessManager.Stores;

namespace UmbracoPrism.ProcessManager.Services;

/// <summary>
/// One action to advance a simulated service request, optionally supplying field values
/// for the stage being submitted.
/// </summary>
public sealed record ProcessManagerSimulationStep(string Action, Dictionary<string, object?>? FieldValues = null);

/// <summary>
/// Raw calculated values for one step of a simulation trace — the same
/// <see cref="CalculationResult"/> the real engine computed, not the display-formatted
/// values baked into rendered components. <c>null</c> when the step's stage had no
/// calculations block, or evaluation failed.
/// </summary>
public sealed record ServiceBlueprintSimulationStepCalculations(
    IReadOnlyDictionary<string, object?> Fields,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Series);

/// <summary>
/// Result of a <see cref="ServiceBlueprintSimulationRunner.Run"/> call — the stage trace, exactly as
/// <c>IProcessManager.GetCurrent</c>/<c>Advance</c> would report to a real client, plus
/// the raw calculated values for each step (parallel to <see cref="Trace"/>; entries are
/// <c>null</c> for steps whose stage has no calculations block or whose calculations failed).
/// </summary>
public sealed record ServiceBlueprintSimulationResult(
    IReadOnlyList<ServiceRequestResponseEnvelope> Trace,
    IReadOnlyList<ServiceBlueprintSimulationStepCalculations?> Calculations);

/// <summary>
/// Dry-runs a <see cref="ServiceBlueprint"/> through the real <see cref="ProcessManagerEngine"/> —
/// no persistence, no host, no HTTP. Lets a caller (e.g. an AI authoring tool) script a sequence of
/// actions against a definition and inspect the resulting stage trace, exactly as
/// <c>IProcessManager.GetCurrent</c>/<c>Advance</c> would report to a real client.
/// </summary>
public sealed class ServiceBlueprintSimulationRunner
{
    /// <param name="mockServiceInputs">
    /// Values to hand back for any <c>source: "service"</c> calculation field — the same shape
    /// a real host's <c>ResolveServiceInputs</c> override would supply (e.g.
    /// <c>{ "member": { "age": 47, ... } }</c>). Without this, a definition with a
    /// service-sourced field simulates with those fields unresolved, exactly as it would
    /// against a host that hasn't wired one up.
    /// </param>
    public ServiceBlueprintSimulationResult Run(
        ServiceBlueprint definition,
        IReadOnlyList<ProcessManagerSimulationStep> steps,
        IReadOnlyDictionary<string, object?>? mockServiceInputs = null,
        string tenantId = "simulation-tenant",
        string userId = "simulation-user",
        ILogger? logger = null)
    {
        var engine = new ProcessManagerEngine(
            logger ?? NullLogger.Instance,
            new SingleDefinitionServiceBlueprintStore(definition),
            new SimulationContentSanitizer(),
            mockServiceInputs is null ? null : (_, _, _) => mockServiceInputs);

        var trace = new List<ServiceRequestResponseEnvelope>();
        var calculations = new List<ServiceBlueprintSimulationStepCalculations?>();

        var current = engine.GetCurrent(definition.DefinitionKey, tenantId, userId, action: "start-new");
        trace.Add(current);
        calculations.Add(ToStepCalculations(engine.GetLastCalculationResult(current.InstanceId)));

        foreach (var step in steps)
        {
            current = engine.Advance(
                current.InstanceId,
                tenantId,
                userId,
                step.Action,
                current.StateVersion,
                step.FieldValues);
            trace.Add(current);
            calculations.Add(ToStepCalculations(engine.GetLastCalculationResult(current.InstanceId)));
        }

        return new ServiceBlueprintSimulationResult(trace, calculations);
    }

    private static ServiceBlueprintSimulationStepCalculations? ToStepCalculations(CalculationResult? result) =>
        result is null ? null : new ServiceBlueprintSimulationStepCalculations(result.Fields, result.Series);
}
