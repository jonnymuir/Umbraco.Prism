using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Calculations;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.WorkflowRuntime.Services;

/// <summary>Outcome of validating a workflow definition.</summary>
public sealed record WorkflowValidationOutcome(bool IsValid, IReadOnlyList<WorkflowDiagnostic> Diagnostics)
{
    public static WorkflowValidationOutcome Valid { get; } = new(true, Array.Empty<WorkflowDiagnostic>());
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkflowSaveStatus>))]
public enum WorkflowSaveStatus { Saved, Invalid, Conflict }

/// <summary>
/// Outcome of a <see cref="WorkflowAuthoringService.SaveAsync"/> call — distinguishes a
/// successful save from a validation failure (<see cref="Diagnostics"/> from <see cref="WorkflowAuthoringService.Validate"/>)
/// and from an optimistic-concurrency conflict (<see cref="CurrentVersion"/> is what's actually
/// persisted now; the caller's <c>expectedVersion</c> was stale).
/// </summary>
public sealed record WorkflowSaveOutcome(
    WorkflowSaveStatus Status,
    IReadOnlyList<WorkflowDiagnostic> Diagnostics,
    int? CurrentVersion = null,
    int? NewVersion = null)
{
    public bool IsSaved => Status == WorkflowSaveStatus.Saved;

    public static WorkflowSaveOutcome Saved(int newVersion) =>
        new(WorkflowSaveStatus.Saved, Array.Empty<WorkflowDiagnostic>(), NewVersion: newVersion);

    public static WorkflowSaveOutcome Invalid(IReadOnlyList<WorkflowDiagnostic> diagnostics) =>
        new(WorkflowSaveStatus.Invalid, diagnostics);

    public static WorkflowSaveOutcome Conflict(int currentVersion) =>
        new(
            WorkflowSaveStatus.Conflict,
            [new WorkflowDiagnostic(
                "SAVE_VERSION_CONFLICT",
                "version",
                $"Workflow has changed since it was loaded — current version is {currentVersion}, which didn't match the expected version. Reload and reapply your change.")],
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

    /// <summary>
    /// Validates gateway routing, every stat-group/chart binding against the fields and series
    /// that actually exist, the <c>calculations</c> block, and every component's
    /// <c>showWhen</c> expression, collecting every diagnostic rather than stopping at the
    /// first. A declared <c>source: "service"</c> field not covered by
    /// <paramref name="mockServiceInputs"/> can't be verified statically — it's reported as a
    /// <see cref="WorkflowDiagnosticSeverity.Warning"/>, not an error, and (since the calculated
    /// fields and any showWhen depending on them can't be evaluated without it) the rest of the
    /// calculations/showWhen checks are skipped for that call. Pass real values via
    /// <paramref name="mockServiceInputs"/>, or use <c>Simulate</c>, to verify those fully.
    /// </summary>
    public WorkflowValidationOutcome Validate(
        WorkflowDefinitionFile workflow,
        IReadOnlyDictionary<string, object?>? mockServiceInputs = null)
    {
        var diagnostics = new List<WorkflowDiagnostic>(workflow.ValidateGatewayRouting());
        diagnostics.AddRange(workflow.ValidateDataDisplayBindings());
        var evaluator = new CalculationEvaluator();

        IReadOnlyDictionary<string, object?> showWhenScope;

        if (workflow.Calculations is null)
        {
            showWhenScope = CalculationScopeBuilder.Build(workflow, EmptyFieldValues, mockServiceInputs);
        }
        else
        {
            var unresolvedServiceFields = workflow.Calculations.Fields
                .Where(f => string.Equals(f.Value.Source, "service", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Key)
                .Where(name => mockServiceInputs is null || !mockServiceInputs.ContainsKey(name))
                .ToList();

            if (unresolvedServiceFields.Count > 0)
            {
                foreach (var name in unresolvedServiceFields)
                {
                    diagnostics.Add(new WorkflowDiagnostic(
                        "CALC_SERVICE_FIELD_UNVERIFIED",
                        $"calculations.fields.{name}",
                        $"Field '{name}' is service-sourced; validate cannot supply a real value for it " +
                        "statically. Pass mockServiceInputs, or use simulate_workflow, to verify " +
                        "calculations that depend on it.",
                        WorkflowDiagnosticSeverity.Warning));
                }

                // Calculated fields, and any showWhen depending on them, can't be reliably
                // evaluated without every service field resolved — stop here for this call.
                return new WorkflowValidationOutcome(
                    !diagnostics.Any(d => d.Severity == WorkflowDiagnosticSeverity.Error), diagnostics);
            }

            var scope = CalculationScopeBuilder.Build(workflow, EmptyFieldValues, mockServiceInputs);
            var inputsWithoutDefault = CalculationScopeBuilder.DescribeInputs(workflow)
                .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value.Default))
                .Select(kvp => kvp.Key)
                .ToHashSet(StringComparer.Ordinal);
            var evaluation = evaluator.EvaluateCollectingErrors(workflow.Calculations, scope);
            foreach (var fieldOrSeries in evaluation.Diagnostics)
            {
                var (code, path) = fieldOrSeries.Kind == CalculationDiagnosticKind.Field
                    ? ("CALC_FIELD_ERROR", $"calculations.fields.{fieldOrSeries.Name}")
                    : ("CALC_SERIES_ERROR", $"calculations.series.{fieldOrSeries.Name}");
                diagnostics.Add(new WorkflowDiagnostic(
                    code, path, ExplainIfMissingDefault(fieldOrSeries.Message, inputsWithoutDefault)));
            }

            var mergedScope = new Dictionary<string, object?>(scope, StringComparer.Ordinal);
            foreach (var (name, value) in evaluation.Result.Fields)
            {
                mergedScope[name] = value;
            }

            showWhenScope = mergedScope;
        }

        foreach (var state in workflow.States)
        {
            foreach (var (component, path) in state.Components.FlattenWithPaths($"states.{state.StateKey}.components"))
            {
                if (string.IsNullOrWhiteSpace(component.ShowWhen))
                {
                    continue;
                }

                try
                {
                    evaluator.EvaluateExpression(component.ShowWhen, showWhenScope, workflow.Calculations);
                }
                catch (CalculationException ex)
                {
                    diagnostics.Add(new WorkflowDiagnostic("SHOW_WHEN_EVAL_ERROR", $"{path}.showWhen", ex.Message));
                }
            }
        }

        return new WorkflowValidationOutcome(
            !diagnostics.Any(d => d.Severity == WorkflowDiagnosticSeverity.Error), diagnostics);
    }

    private static readonly Regex UnknownNamePattern = new(@"^Unknown name '([^']+)' in", RegexOptions.Compiled);

    /// <summary>
    /// Validation has no real submitted data — <see cref="CalculationScopeBuilder.Build"/> can
    /// only put a required input in scope if it has a declared <c>default</c>. A calculation
    /// referencing a required field with no default is completely normal (the field's real value
    /// only exists once a user fills it in) but surfaces here as an opaque "Unknown name", which
    /// reads like the field doesn't exist at all — exactly the false lead that sent an AI agent
    /// down five wrong-syntax retries in practice before giving up. When the unknown name matches
    /// a real input missing only its default, say so directly instead.
    /// </summary>
    private static string ExplainIfMissingDefault(string message, IReadOnlySet<string> inputsWithoutDefault)
    {
        var match = UnknownNamePattern.Match(message);
        if (!match.Success || !inputsWithoutDefault.Contains(match.Groups[1].Value))
        {
            return message;
        }

        var fieldKey = match.Groups[1].Value;
        return $"{message} '{fieldKey}' is a real input field on this workflow, but validation can't " +
            "evaluate a calculation against it until it has a declared \"default\" value (there's no real " +
            "submitted data to fall back on outside a live instance) — add one to that component. This is " +
            "why simulate_workflow can succeed with real field values while validate_workflow reports this " +
            "field as unknown.";
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
            return WorkflowSaveOutcome.Invalid(validation.Diagnostics);
        }

        var result = await store.SaveAsync(workflow, expectedVersion, ct);
        return result.Saved
            ? WorkflowSaveOutcome.Saved(result.CurrentVersion)
            : WorkflowSaveOutcome.Conflict(result.CurrentVersion);
    }

    public WorkflowSimulationResult Simulate(
        WorkflowDefinitionFile workflow,
        IReadOnlyList<WorkflowRuntimeSimulationStep> steps,
        IReadOnlyDictionary<string, object?>? mockServiceInputs = null) =>
        new WorkflowSimulationRunner().Run(workflow, steps, mockServiceInputs);
}
