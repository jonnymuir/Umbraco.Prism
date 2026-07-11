using System.Text.Json.Serialization;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Calculations;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.WorkflowRuntime.Services;

/// <summary>Outcome of validating a workflow definition.</summary>
public sealed record WorkflowValidationOutcome(bool IsValid, IReadOnlyList<string> Errors)
{
    public static WorkflowValidationOutcome Valid { get; } = new(true, Array.Empty<string>());
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkflowSaveStatus>))]
public enum WorkflowSaveStatus { Saved, Invalid, Conflict }

/// <summary>
/// Outcome of a <see cref="WorkflowAuthoringService.SaveAsync"/> call — distinguishes a
/// successful save from a validation failure (<see cref="Errors"/> from <see cref="WorkflowAuthoringService.Validate"/>)
/// and from an optimistic-concurrency conflict (<see cref="CurrentVersion"/> is what's actually
/// persisted now; the caller's <c>expectedVersion</c> was stale).
/// </summary>
public sealed record WorkflowSaveOutcome(
    WorkflowSaveStatus Status,
    IReadOnlyList<string> Errors,
    int? CurrentVersion = null,
    int? NewVersion = null)
{
    public bool IsSaved => Status == WorkflowSaveStatus.Saved;

    public static WorkflowSaveOutcome Saved(int newVersion) =>
        new(WorkflowSaveStatus.Saved, Array.Empty<string>(), NewVersion: newVersion);

    public static WorkflowSaveOutcome Invalid(IReadOnlyList<string> errors) =>
        new(WorkflowSaveStatus.Invalid, errors);

    public static WorkflowSaveOutcome Conflict(int currentVersion) =>
        new(
            WorkflowSaveStatus.Conflict,
            [$"Workflow has changed since it was loaded — current version is {currentVersion}, which didn't match the expected version. Reload and reapply your change."],
            CurrentVersion: currentVersion);
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

    /// <summary>
    /// Validates, then saves only if <paramref name="expectedVersion"/> still matches what's
    /// currently persisted (see <see cref="IWorkflowSourceStore.SaveAsync"/>). Pass <c>0</c> for
    /// a workflow you expect doesn't exist yet.
    /// </summary>
    public async Task<WorkflowSaveOutcome> SaveAsync(WorkflowDefinitionFile workflow, int expectedVersion, CancellationToken ct = default)
    {
        var validation = Validate(workflow);
        if (!validation.IsValid)
        {
            return WorkflowSaveOutcome.Invalid(validation.Errors);
        }

        var result = await store.SaveAsync(workflow, expectedVersion, ct);
        return result.Saved
            ? WorkflowSaveOutcome.Saved(result.CurrentVersion)
            : WorkflowSaveOutcome.Conflict(result.CurrentVersion);
    }

    public IReadOnlyList<WorkflowResponseEnvelope> Simulate(
        WorkflowDefinitionFile workflow,
        IReadOnlyList<WorkflowRuntimeSimulationStep> steps) =>
        new WorkflowSimulationRunner().Run(workflow, steps);
}
