using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>
/// Deterministic compiler from <see cref="AuthoredWorkflow"/> to <see cref="WorkflowDefinitionFile"/>.
///
/// Pipeline stages:
///   1. Validate   — structural correctness checks; errors block emission.
///   2. Normalise  — sort stages, transitions and fields into canonical order.
///   3. Emit       — build StepDefinitions and WorkflowTransitionFiles from authored graph.
///   4. Checksum   — SHA-256 of canonical UTF-8 JSON (no BOM).
///
/// Determinism contract: identical <see cref="AuthoredWorkflow"/> input → byte-identical output on
/// every invocation, every platform, every .NET version (within the same major). Locked by
/// <c>WorkflowProjectorDeterminismTests</c>.
/// </summary>
public sealed class WorkflowProjector : IWorkflowProjector
{
    /// <summary>
    /// Canonical serialization options used for both the checksum computation and external consumers
    /// that need to reproduce the projected JSON. Exposed as a public static so tests can verify
    /// byte-identical output without duplicating option configuration.
    /// </summary>
    public static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <inheritdoc/>
    public ProjectionResult Project(AuthoredWorkflow authored)
    {
        var diagnostics = new List<ProjectionDiagnostic>();

        // 1. Validate
        Validate(authored, diagnostics);

        // 2. Normalise — sort everything into canonical order before emitting
        var normalisedStages = authored.Stages
            .OrderBy(s => s.StageKey, StringComparer.Ordinal)
            .ToList();

        var normalisedTransitions = authored.Transitions
            .OrderBy(t => t.FromStage, StringComparer.Ordinal)
            .ThenBy(t => t.ToStage, StringComparer.Ordinal)
            .ThenBy(t => t.Action, StringComparer.Ordinal)
            .ToList();

        // 3. Emit
        var states = normalisedStages
            .Select(s => EmitStage(authored, s, diagnostics))
            .ToList();

        var transitions = normalisedTransitions
            .Select(EmitTransition)
            .ToList();

        var file = new WorkflowDefinitionFile
        {
            DefinitionKey = authored.DefinitionKey,
            DisplayName = authored.DisplayName,
            Version = authored.Version,
            InitialState = authored.InitialStageKey,
            InstancePolicy = authored.InstancePolicy,
            States = states,
            Transitions = transitions
        };

        // 4. Checksum — SHA-256 of canonical UTF-8 JSON (no BOM)
        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, CanonicalOptions);
        var hash = SHA256.HashData(bytes);
        var checksum = Convert.ToHexString(hash).ToLowerInvariant();

        return new ProjectionResult
        {
            File = file,
            Checksum = checksum,
            Diagnostics = diagnostics
        };
    }

    // ─── Stage 1: Validate ────────────────────────────────────────────────────

    private static void Validate(AuthoredWorkflow authored, List<ProjectionDiagnostic> diagnostics)
    {
        var stageKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var stage in authored.Stages)
        {
            if (string.IsNullOrWhiteSpace(stage.StageKey))
            {
                diagnostics.Add(Error("PROJ001", "A stage has an empty StageKey.", null));
                continue;
            }

            if (!stageKeys.Add(stage.StageKey))
            {
                diagnostics.Add(Error("PROJ002",
                    $"Duplicate StageKey '{stage.StageKey}'. All stage keys must be unique.", stage.StageKey));
            }
        }

        if (!string.IsNullOrWhiteSpace(authored.InitialStageKey) &&
            !stageKeys.Contains(authored.InitialStageKey))
        {
            diagnostics.Add(Error("PROJ003",
                $"InitialStageKey '{authored.InitialStageKey}' does not reference any defined stage.", null));
        }

        foreach (var transition in authored.Transitions)
        {
            if (!stageKeys.Contains(transition.FromStage))
            {
                diagnostics.Add(Warning("PROJ004",
                    $"Transition FromStage '{transition.FromStage}' does not reference a defined stage.",
                    transition.FromStage));
            }

            if (!stageKeys.Contains(transition.ToStage))
            {
                diagnostics.Add(Warning("PROJ004",
                    $"Transition ToStage '{transition.ToStage}' does not reference a defined stage.",
                    transition.ToStage));
            }
        }
    }

    // ─── Stage 3: Emit ────────────────────────────────────────────────────────

    private static StepDefinition EmitStage(
        AuthoredWorkflow authored,
        AuthoredStage stage,
        List<ProjectionDiagnostic> diagnostics)
    {
        var components = EmitComponents(authored, stage, diagnostics);

        return new StepDefinition
        {
            StateKey = stage.StageKey,
            DisplayName = stage.DisplayName,
            Components = components
        };
    }

    private static IReadOnlyList<PrismComponent> EmitComponents(
        AuthoredWorkflow authored,
        AuthoredStage stage,
        List<ProjectionDiagnostic> diagnostics)
    {
        return stage.Kind switch
        {
            StageKind.Question => EmitQuestionComponents(stage),
            StageKind.CheckAnswers => EmitCheckAnswersComponents(authored),
            StageKind.Confirmation => EmitConfirmationComponents(stage),
            StageKind.TaskList => EmitTaskListComponents(),
            StageKind.Waiting or StageKind.StatusTimeline => EmitWaitingComponents(stage),
            _ => EmitUnknownKind(stage, diagnostics)
        };
    }

    private static IReadOnlyList<PrismComponent> EmitQuestionComponents(AuthoredStage stage)
    {
        var normalisedFields = stage.Fields
            .OrderBy(f => f.Key, StringComparer.Ordinal)
            .ToList();

        var children = normalisedFields
            .Select(f => (PrismComponent)MapFieldToInputComponent(f))
            .ToList();

        return [new FieldsetComponent { Children = children }];
    }

    private static IReadOnlyList<PrismComponent> EmitCheckAnswersComponents(AuthoredWorkflow authored)
    {
        // Gather all fields from question stages in canonical (sorted) order so the output is stable.
        var questionFields = authored.Stages
            .Where(s => s.Kind == StageKind.Question)
            .OrderBy(s => s.StageKey, StringComparer.Ordinal)
            .SelectMany(s => s.Fields.OrderBy(f => f.Key, StringComparer.Ordinal))
            .Select(f => (PrismComponent)MapFieldToInputComponent(f))
            .ToList();

        return [new SummaryListComponent { Children = questionFields }];
    }

    private static IReadOnlyList<PrismComponent> EmitConfirmationComponents(AuthoredStage stage)
        => [new PanelComponent { Heading = stage.DisplayName }];

    private static IReadOnlyList<PrismComponent> EmitTaskListComponents()
        => [new TaskListComponent()];

    private static IReadOnlyList<PrismComponent> EmitWaitingComponents(AuthoredStage stage)
    {
        var meta = stage.Waiting ?? new WaitingMetadata();
        return
        [
            new WaitingComponent
            {
                Content = meta.Content,
                ExpectedWaitSeconds = meta.ExpectedWaitSeconds,
                PollIntervalMs = meta.PollIntervalMs,
                AllowDefer = meta.AllowDefer,
                DeferMessage = meta.DeferMessage
            }
        ];
    }

    private static IReadOnlyList<PrismComponent> EmitUnknownKind(
        AuthoredStage stage,
        List<ProjectionDiagnostic> diagnostics)
    {
        diagnostics.Add(Warning("PROJ005",
            $"Unknown StageKind value for stage '{stage.StageKey}'. Defaulting to question shell.",
            stage.StageKey));

        return [new FieldsetComponent()];
    }

    private static WorkflowTransitionFile EmitTransition(AuthoredTransition t) =>
        new()
        {
            FromState = t.FromStage,
            ToState = t.ToStage,
            Action = t.Action,
            RequiresRole = t.RequiresRole
        };

    // ─── Field-to-component mapping ───────────────────────────────────────────

    private static InputComponent MapFieldToInputComponent(AuthoredField field) => field.Type switch
    {
        FieldType.Textarea => new TextareaComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Email => new EmailComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Pattern = field.ValidationPattern
        },
        FieldType.Number => new NumberInputComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Decimal => new DecimalInputComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Date => new DateInputComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Boolean => new BooleanComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Select => new SelectComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Options = field.Options
        },
        FieldType.Radios => new RadiosComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Options = field.Options
        },
        FieldType.Checkboxes => new CheckboxesComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Options = field.Options
        },
        _ => new TextInputComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Pattern = field.ValidationPattern
        }
    };

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ProjectionDiagnostic Error(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Error, Code = code, Message = message, StageKey = stageKey };

    private static ProjectionDiagnostic Warning(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Warning, Code = code, Message = message, StageKey = stageKey };
}
