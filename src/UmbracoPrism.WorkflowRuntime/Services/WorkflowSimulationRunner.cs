using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.WorkflowRuntime.Services;

/// <summary>
/// One action to advance a simulated workflow instance, optionally supplying field values
/// for the stage being submitted.
/// </summary>
public sealed record WorkflowRuntimeSimulationStep(string Action, Dictionary<string, object?>? FieldValues = null);

/// <summary>
/// Dry-runs a <see cref="WorkflowDefinitionFile"/> through the real <see cref="WorkflowRuntimeEngine"/> —
/// no persistence, no host, no HTTP. Lets a caller (e.g. an AI authoring tool) script a sequence of
/// actions against a definition and inspect the resulting state trace, exactly as
/// <c>IWorkflowRuntimeEngine.GetCurrent</c>/<c>Advance</c> would report to a real client.
/// </summary>
public sealed class WorkflowSimulationRunner
{
    public IReadOnlyList<WorkflowResponseEnvelope> Run(
        WorkflowDefinitionFile definition,
        IReadOnlyList<WorkflowRuntimeSimulationStep> steps,
        string tenantId = "simulation-tenant",
        string userId = "simulation-user",
        ILogger? logger = null)
    {
        var engine = new WorkflowRuntimeEngine(
            logger ?? NullLogger.Instance,
            new SingleDefinitionWorkflowStore(definition),
            new SimulationContentSanitizer());

        var trace = new List<WorkflowResponseEnvelope>();
        var current = engine.GetCurrent(definition.DefinitionKey, tenantId, userId, action: "start-new");
        trace.Add(current);

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
        }

        return trace;
    }
}
