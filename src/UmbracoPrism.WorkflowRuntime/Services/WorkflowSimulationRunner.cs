using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Calculations;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.WorkflowRuntime.Services;

/// <summary>
/// One action to advance a simulated workflow instance, optionally supplying field values
/// for the stage being submitted.
/// </summary>
public sealed record WorkflowRuntimeSimulationStep(string Action, Dictionary<string, object?>? FieldValues = null);

/// <summary>
/// Raw calculated values for one step of a simulation trace — the same
/// <see cref="CalculationResult"/> the real engine computed, not the display-formatted
/// values baked into rendered components. <c>null</c> when the step's state had no
/// calculations block, or evaluation failed.
/// </summary>
public sealed record WorkflowSimulationStepCalculations(
    IReadOnlyDictionary<string, object?> Fields,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Series);

/// <summary>
/// Result of a <see cref="WorkflowSimulationRunner.Run"/> call — the state trace, exactly as
/// <c>IWorkflowRuntimeEngine.GetCurrent</c>/<c>Advance</c> would report to a real client, plus
/// the raw calculated values for each step (parallel to <see cref="Trace"/>; entries are
/// <c>null</c> for steps whose state has no calculations block or whose calculations failed).
/// </summary>
public sealed record WorkflowSimulationResult(
    IReadOnlyList<WorkflowResponseEnvelope> Trace,
    IReadOnlyList<WorkflowSimulationStepCalculations?> Calculations);

/// <summary>
/// Dry-runs a <see cref="WorkflowDefinitionFile"/> through the real <see cref="WorkflowRuntimeEngine"/> —
/// no persistence, no host, no HTTP. Lets a caller (e.g. an AI authoring tool) script a sequence of
/// actions against a definition and inspect the resulting state trace, exactly as
/// <c>IWorkflowRuntimeEngine.GetCurrent</c>/<c>Advance</c> would report to a real client.
/// </summary>
public sealed class WorkflowSimulationRunner
{
    /// <param name="mockServiceInputs">
    /// Values to hand back for any <c>source: "service"</c> calculation field — the same shape
    /// a real host's <c>ResolveServiceInputs</c> override would supply (e.g.
    /// <c>{ "member": { "age": 47, ... } }</c>). Without this, a definition with a
    /// service-sourced field simulates with those fields unresolved, exactly as it would
    /// against a host that hasn't wired one up.
    /// </param>
    public WorkflowSimulationResult Run(
        WorkflowDefinitionFile definition,
        IReadOnlyList<WorkflowRuntimeSimulationStep> steps,
        IReadOnlyDictionary<string, object?>? mockServiceInputs = null,
        string tenantId = "simulation-tenant",
        string userId = "simulation-user",
        ILogger? logger = null)
    {
        var engine = new WorkflowRuntimeEngine(
            logger ?? NullLogger.Instance,
            new SingleDefinitionWorkflowStore(definition),
            new SimulationContentSanitizer(),
            mockServiceInputs is null ? null : (_, _, _) => mockServiceInputs);

        var trace = new List<WorkflowResponseEnvelope>();
        var calculations = new List<WorkflowSimulationStepCalculations?>();

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

        return new WorkflowSimulationResult(trace, calculations);
    }

    private static WorkflowSimulationStepCalculations? ToStepCalculations(CalculationResult? result) =>
        result is null ? null : new WorkflowSimulationStepCalculations(result.Fields, result.Series);
}
