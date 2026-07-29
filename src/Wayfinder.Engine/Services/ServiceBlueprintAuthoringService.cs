using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Extensions;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Calculations;
using Wayfinder.Engine.Abstractions;

namespace Wayfinder.Engine.Services;

/// <summary>Outcome of validating a service blueprint.</summary>
public sealed record ServiceBlueprintValidationOutcome(bool IsValid, IReadOnlyList<ServiceBlueprintDiagnostic> Diagnostics)
{
    public static ServiceBlueprintValidationOutcome Valid { get; } = new(true, Array.Empty<ServiceBlueprintDiagnostic>());
}

[JsonConverter(typeof(JsonStringEnumConverter<ServiceBlueprintSaveStatus>))]
public enum ServiceBlueprintSaveStatus { Saved, Invalid, Conflict }

/// <summary>
/// Outcome of a <see cref="ServiceBlueprintAuthoringService.SaveAsync"/> call — distinguishes a
/// successful save from a validation failure (<see cref="Diagnostics"/> from <see cref="ServiceBlueprintAuthoringService.Validate"/>)
/// and from an optimistic-concurrency conflict (<see cref="CurrentVersion"/> is what's actually
/// persisted now; the caller's <c>expectedVersion</c> was stale).
/// </summary>
public sealed record ServiceBlueprintSaveOutcome(
    ServiceBlueprintSaveStatus Status,
    IReadOnlyList<ServiceBlueprintDiagnostic> Diagnostics,
    int? CurrentVersion = null,
    int? NewVersion = null)
{
    public bool IsSaved => Status == ServiceBlueprintSaveStatus.Saved;

    public static ServiceBlueprintSaveOutcome Saved(int newVersion) =>
        new(ServiceBlueprintSaveStatus.Saved, Array.Empty<ServiceBlueprintDiagnostic>(), NewVersion: newVersion);

    public static ServiceBlueprintSaveOutcome Invalid(IReadOnlyList<ServiceBlueprintDiagnostic> diagnostics) =>
        new(ServiceBlueprintSaveStatus.Invalid, diagnostics);

    public static ServiceBlueprintSaveOutcome Conflict(int currentVersion) =>
        new(
            ServiceBlueprintSaveStatus.Conflict,
            [new ServiceBlueprintDiagnostic(
                "SAVE_VERSION_CONFLICT",
                "version",
                $"Blueprint has changed since it was loaded — current version is {currentVersion}, which didn't match the expected version. Reload and reapply your change.")],
            CurrentVersion: currentVersion);
}

/// <summary>
/// Transport-agnostic service blueprint authoring surface: list/read/validate/save/simulate
/// definitions against a host-supplied <see cref="IServiceBlueprintSourceStore"/>. Reusable by
/// any front door (MCP tools, a CLI, a host's own code) — no MCP dependency here.
/// </summary>
public sealed class ServiceBlueprintAuthoringService(
    IServiceBlueprintSourceStore store,
    IEnumerable<IServiceBlueprintStructuralValidator>? structuralValidators = null,
    IQueueCapabilitiesProvider? queueCapabilities = null)
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyFieldValues =
        new Dictionary<string, object?>();

    private readonly IReadOnlyList<IServiceBlueprintStructuralValidator> _structuralValidators =
        structuralValidators?.ToArray() ?? [];

    public Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default) =>
        store.ListAsync(ct);

    public Task<ServiceBlueprint?> ReadAsync(string definitionKey, CancellationToken ct = default) =>
        store.LoadAsync(definitionKey, ct);

    public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default) =>
        store.DeleteAsync(definitionKey, ct);

    /// <summary>
    /// Every queue this host has declared render capabilities for, per
    /// <see cref="IQueueCapabilitiesProvider.GetAllDeclaredCapabilities"/> — empty if no
    /// provider is registered.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetQueueCapabilities() =>
        queueCapabilities?.GetAllDeclaredCapabilities() ?? new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// Validates gateway routing, every stat-group/chart binding against the fields and series
    /// that actually exist, the <c>calculations</c> block, and every component's
    /// <c>showWhen</c> expression, collecting every diagnostic rather than stopping at the
    /// first. A declared <c>source: "service"</c> field not covered by
    /// <paramref name="mockServiceInputs"/> can't be verified statically — it's reported as a
    /// <see cref="ServiceBlueprintDiagnosticSeverity.Warning"/>, not an error, and (since the calculated
    /// fields and any showWhen depending on them can't be evaluated without it) the rest of the
    /// calculations/showWhen checks are skipped for that call. Pass real values via
    /// <paramref name="mockServiceInputs"/>, or use <c>Simulate</c>, to verify those fully.
    /// </summary>
    public ServiceBlueprintValidationOutcome Validate(
        ServiceBlueprint blueprint,
        IReadOnlyDictionary<string, object?>? mockServiceInputs = null)
    {
        var diagnostics = new List<ServiceBlueprintDiagnostic>(blueprint.ValidateGatewayRouting());
        diagnostics.AddRange(blueprint.ValidateDataDisplayBindings());
        diagnostics.AddRange(blueprint.ValidateReachability());
        diagnostics.AddRange(blueprint.ValidateStageVocabulary());
        diagnostics.AddRange(ValidateQueueCapabilities(blueprint));
        foreach (var validator in _structuralValidators)
        {
            diagnostics.AddRange(validator.Validate(blueprint));
        }
        var evaluator = new CalculationEvaluator();

        IReadOnlyDictionary<string, object?> showWhenScope;

        if (blueprint.Calculations is null)
        {
            showWhenScope = CalculationScopeBuilder.Build(blueprint, EmptyFieldValues, mockServiceInputs);
        }
        else
        {
            var unresolvedServiceFields = blueprint.Calculations.Fields
                .Where(f => string.Equals(f.Value.Source, "service", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Key)
                .Where(name => mockServiceInputs is null || !mockServiceInputs.ContainsKey(name))
                .ToList();

            if (unresolvedServiceFields.Count > 0)
            {
                foreach (var name in unresolvedServiceFields)
                {
                    diagnostics.Add(new ServiceBlueprintDiagnostic(
                        "CALC_SERVICE_FIELD_UNVERIFIED",
                        $"calculations.fields.{name}",
                        $"Field '{name}' is service-sourced; validate cannot supply a real value for it " +
                        "statically. Pass mockServiceInputs, or use simulate_service_blueprint, to verify " +
                        "calculations that depend on it.",
                        ServiceBlueprintDiagnosticSeverity.Warning));
                }

                // Calculated fields, and any showWhen depending on them, can't be reliably
                // evaluated without every service field resolved — stop here for this call.
                return new ServiceBlueprintValidationOutcome(
                    !diagnostics.Any(d => d.Severity == ServiceBlueprintDiagnosticSeverity.Error), diagnostics);
            }

            var scope = CalculationScopeBuilder.Build(blueprint, EmptyFieldValues, mockServiceInputs);
            var inputsWithoutDefault = CalculationScopeBuilder.DescribeInputs(blueprint)
                .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value.Default))
                .Select(kvp => kvp.Key)
                .ToHashSet(StringComparer.Ordinal);
            var evaluation = evaluator.EvaluateCollectingErrors(blueprint.Calculations, scope);
            foreach (var fieldOrSeries in evaluation.Diagnostics)
            {
                var (code, path) = fieldOrSeries.Kind == CalculationDiagnosticKind.Field
                    ? ("CALC_FIELD_ERROR", $"calculations.fields.{fieldOrSeries.Name}")
                    : ("CALC_SERIES_ERROR", $"calculations.series.{fieldOrSeries.Name}");
                diagnostics.Add(new ServiceBlueprintDiagnostic(
                    code, path, ExplainIfMissingDefault(fieldOrSeries.Message, inputsWithoutDefault)));
            }

            var mergedScope = new Dictionary<string, object?>(scope, StringComparer.Ordinal);
            foreach (var (name, value) in evaluation.Result.Fields)
            {
                mergedScope[name] = value;
            }

            showWhenScope = mergedScope;
        }

        foreach (var stage in blueprint.Stages)
        {
            foreach (var (component, path) in stage.Components.FlattenWithPaths($"stages.{stage.StageKey}.components"))
            {
                if (string.IsNullOrWhiteSpace(component.ShowWhen))
                {
                    continue;
                }

                try
                {
                    evaluator.EvaluateExpression(component.ShowWhen, showWhenScope, blueprint.Calculations);
                }
                catch (CalculationException ex)
                {
                    diagnostics.Add(new ServiceBlueprintDiagnostic("SHOW_WHEN_EVAL_ERROR", $"{path}.showWhen", ex.Message));
                }
            }
        }

        return new ServiceBlueprintValidationOutcome(
            !diagnostics.Any(d => d.Severity == ServiceBlueprintDiagnosticSeverity.Error), diagnostics);
    }

    /// <summary>
    /// When a host registers <see cref="IQueueCapabilitiesProvider"/>, reject any stage whose
    /// components exceed what its queue's host can actually render — otherwise a blueprint can
    /// be authored/saved with a component that silently renders as nothing at runtime. A queue
    /// with no declaration at all is unrestricted (not this host's concern).
    /// </summary>
    private IEnumerable<ServiceBlueprintDiagnostic> ValidateQueueCapabilities(ServiceBlueprint blueprint)
    {
        if (queueCapabilities is null)
        {
            yield break;
        }

        foreach (var stage in blueprint.Stages)
        {
            var supportedTypes = queueCapabilities.GetSupportedComponentTypes(stage.QueueKey);
            if (supportedTypes is null)
            {
                continue;
            }

            foreach (var (component, path) in stage.Components.FlattenWithPaths($"stages.{stage.StageKey}.components"))
            {
                var discriminator = PrismComponentTypeCatalog.DiscriminatorFor(component);
                if (supportedTypes.Contains(discriminator, StringComparer.Ordinal))
                {
                    continue;
                }

                yield return new ServiceBlueprintDiagnostic(
                    "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT",
                    path,
                    $"State '{stage.StageKey}' uses component type '{discriminator}', which queue " +
                    $"'{stage.QueueKey}''s host does not declare support for " +
                    (supportedTypes.Count == 0
                        ? "(it currently supports no component types at all). "
                        : $"(it supports: {string.Join(", ", supportedTypes)}). ") +
                    "Remove/replace this component, or extend that host's IQueueCapabilitiesProvider " +
                    "declaration once it can actually render it. Call list_queue_capabilities to check " +
                    "what a queue supports before drafting for it.");
            }
        }
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
        return $"{message} '{fieldKey}' is a real input field on this blueprint, but validation can't " +
            "evaluate a calculation against it until it has a declared \"default\" value (there's no real " +
            "submitted data to fall back on outside a live instance) — add one to that component. This is " +
            "why simulate_service_blueprint can succeed with real field values while validate_service_blueprint reports this " +
            "field as unknown.";
    }

    /// <summary>
    /// Validates, then saves only if <paramref name="expectedVersion"/> still matches what's
    /// currently persisted (see <see cref="IServiceBlueprintSourceStore.SaveAsync"/>). Pass <c>0</c> for
    /// a blueprint you expect doesn't exist yet.
    /// </summary>
    public async Task<ServiceBlueprintSaveOutcome> SaveAsync(ServiceBlueprint blueprint, int expectedVersion, CancellationToken ct = default)
    {
        var validation = Validate(blueprint);
        if (!validation.IsValid)
        {
            return ServiceBlueprintSaveOutcome.Invalid(validation.Diagnostics);
        }

        var result = await store.SaveAsync(blueprint, expectedVersion, ct);
        return result.Saved
            ? ServiceBlueprintSaveOutcome.Saved(result.CurrentVersion)
            : ServiceBlueprintSaveOutcome.Conflict(result.CurrentVersion);
    }

    public ServiceBlueprintSimulationResult Simulate(
        ServiceBlueprint blueprint,
        IReadOnlyList<ProcessManagerSimulationStep> steps,
        IReadOnlyDictionary<string, object?>? mockServiceInputs = null) =>
        new ServiceBlueprintSimulationRunner().Run(blueprint, steps, mockServiceInputs);
}
