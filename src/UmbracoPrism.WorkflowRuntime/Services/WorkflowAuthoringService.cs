using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Calculations;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.WorkflowRuntime.Services;

/// <summary>Outcome of validating (and optionally saving) a workflow definition.</summary>
public sealed record WorkflowValidationOutcome(bool IsValid, IReadOnlyList<string> Errors)
{
    public static WorkflowValidationOutcome Valid { get; } = new(true, Array.Empty<string>());
}

/// <summary>
/// Transport-agnostic workflow authoring surface: list/read/validate/save/simulate
/// definitions against a host-supplied <see cref="IWorkflowSourceStore"/>. Reusable by
/// any front door (MCP tools, a CLI, a host's own code) — no MCP dependency here.
/// </summary>
public sealed class WorkflowAuthoringService(IWorkflowSourceStore store)
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyFieldValues =
        new Dictionary<string, object?>();

    public Task<IReadOnlyList<WorkflowSourceSummary>> ListAsync(CancellationToken ct = default) =>
        store.ListAsync(ct);

    public Task<WorkflowDefinitionFile?> ReadAsync(string definitionKey, CancellationToken ct = default) =>
        store.LoadAsync(definitionKey, ct);

    public WorkflowValidationOutcome Validate(WorkflowDefinitionFile workflow)
    {
        var errors = new List<string>(workflow.ValidateGatewayRouting());

        if (workflow.Calculations is not null)
        {
            try
            {
                var scope = CalculationScopeBuilder.Build(workflow, EmptyFieldValues);
                new CalculationEvaluator().Evaluate(workflow.Calculations, scope);
            }
            catch (CalculationException ex)
            {
                errors.Add($"Calculations block failed to evaluate: {ex.Message}");
            }
        }

        return errors.Count == 0 ? WorkflowValidationOutcome.Valid : new WorkflowValidationOutcome(false, errors);
    }

    public async Task<WorkflowValidationOutcome> SaveAsync(WorkflowDefinitionFile workflow, CancellationToken ct = default)
    {
        var outcome = Validate(workflow);
        if (!outcome.IsValid)
        {
            return outcome;
        }

        await store.SaveAsync(workflow, ct);
        return outcome;
    }

    public IReadOnlyList<WorkflowResponseEnvelope> Simulate(
        WorkflowDefinitionFile workflow,
        IReadOnlyList<WorkflowRuntimeSimulationStep> steps) =>
        new WorkflowSimulationRunner().Run(workflow, steps);
}
