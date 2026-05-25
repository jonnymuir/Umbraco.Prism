using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Models;

namespace UmbracoPrism.WorkflowRuntime.Services;

/// <summary>
/// Generic in-memory runtime engine that executes Prism workflow definitions.
/// </summary>
public class WorkflowRuntimeEngine : IWorkflowRuntimeEngine
{
    private readonly IWorkflowContentSanitizer _sanitizer;
    private readonly Dictionary<string, WorkflowDefinitionFile> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WorkflowInstanceState> _instancesById = new();
    private readonly ConcurrentDictionary<string, string> _instanceLookup = new();

    public WorkflowRuntimeEngine(
        ILogger logger,
        IWorkflowDefinitionStore definitionStore,
        IWorkflowContentSanitizer sanitizer)
    {
        Logger = logger;
        _sanitizer = sanitizer;

        foreach (var (lookupKey, definition) in definitionStore.LoadDefinitions(logger))
        {
            var runtimeLookupKey = !string.IsNullOrWhiteSpace(lookupKey)
                ? lookupKey
                : definition.DefinitionKey;

            if (!string.IsNullOrWhiteSpace(runtimeLookupKey))
            {
                _definitions[runtimeLookupKey] = definition;
            }
        }

        Logger.LogInformation("Workflow runtime ready: {Defs} definition(s).", _definitions.Count);
    }

    protected ILogger Logger { get; }

    public WorkflowResponseEnvelope GetCurrent(
        string workflowKey,
        string tenantId,
        string userId,
        string? instanceId = null,
        string? action = null)
    {
        if (!_definitions.TryGetValue(workflowKey, out var definition))
        {
            Logger.LogWarning("Workflow definition not found: {Key}", workflowKey);
            return ErrorEnvelope(
                $"Workflow '{workflowKey}' is not registered with this application.",
                "DEFINITION_NOT_FOUND");
        }

        if (!string.IsNullOrEmpty(instanceId))
        {
            if (!_instancesById.TryGetValue(instanceId, out var specificInstance))
            {
                return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");
            }

            if (!string.Equals(specificInstance.TenantId, tenantId, StringComparison.Ordinal)
                || !string.Equals(specificInstance.UserId, userId, StringComparison.Ordinal))
            {
                return ErrorEnvelope("Access denied to this workflow instance.", "ACCESS_DENIED");
            }

            Logger.LogInformation("Resuming specific instance {Id}", instanceId);
            return BuildEnvelope(specificInstance, definition);
        }

        var lookupKey = LookupKey(tenantId, userId, workflowKey);

        if (string.Equals(action, "start-new", StringComparison.OrdinalIgnoreCase))
        {
            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                lookupKey,
                action,
                persistLookup: true,
                "Created new workflow instance {Id} for key={Key} (action=start-new)",
                workflowKey);
        }

        if (string.Equals(action, "resume", StringComparison.OrdinalIgnoreCase))
        {
            if (_instanceLookup.TryGetValue(lookupKey, out var resumeInstanceId)
                && _instancesById.TryGetValue(resumeInstanceId, out var resumeInstance))
            {
                Logger.LogInformation("Resuming existing instance {Id} (action=resume)", resumeInstanceId);
                return BuildEnvelope(resumeInstance, definition);
            }

            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                lookupKey,
                action,
                persistLookup: true,
                "Created workflow instance {Id} for key={Key} (action=resume, no existing)",
                workflowKey);
        }

        var policy = definition.InstancePolicy;

        if (string.Equals(policy, "multiple", StringComparison.OrdinalIgnoreCase))
        {
            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                lookupKey,
                action,
                persistLookup: false,
                "Created new workflow instance {Id} for key={Key} (policy=multiple)",
                workflowKey);
        }

        if (string.Equals(policy, "prompt", StringComparison.OrdinalIgnoreCase))
        {
            if (_instanceLookup.TryGetValue(lookupKey, out var promptInstanceId)
                && _instancesById.TryGetValue(promptInstanceId, out var promptInstance))
            {
                var currentState = definition.States.FirstOrDefault(s => s.StateKey == promptInstance.CurrentState);
                var isTerminal = currentState != null && currentState.Components.InferStepType() == "confirmation";

                if (!isTerminal)
                {
                    Logger.LogInformation(
                        "Active instance {Id} exists for key={Key}; returning instance_picker",
                        promptInstanceId,
                        workflowKey);

                    return new WorkflowResponseEnvelope
                    {
                        InstanceId = promptInstanceId,
                        ResponseState = "instance_picker",
                        StateVersion = promptInstance.StateVersion,
                        CorrelationId = promptInstanceId,
                        ServerTimeUtc = DateTimeOffset.UtcNow,
                        InstancePolicy = "prompt",
                        Render = new StepContent
                        {
                            StepType = currentState?.Components.InferStepType() ?? "question",
                            StateDisplayName = currentState?.DisplayName ?? definition.DisplayName,
                            Components = Array.Empty<PrismComponentRenderPayload>(),
                            AvailableActions = Array.Empty<WorkflowAction>()
                        }
                    };
                }
            }

            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                lookupKey,
                action,
                persistLookup: true,
                "Created workflow instance {Id} for key={Key} (policy=prompt, no active)",
                workflowKey);
        }

        if (!_instanceLookup.TryGetValue(lookupKey, out var singleInstanceId)
            || !_instancesById.TryGetValue(singleInstanceId, out var singleInstance))
        {
            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                lookupKey,
                action,
                persistLookup: true,
                "Created workflow instance {Id} for key={Key} tenant={Tenant}",
                workflowKey,
                tenantId);
        }

        return BuildEnvelope(singleInstance, definition);
    }

    public virtual WorkflowResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues)
    {
        if (!_instancesById.TryGetValue(instanceId, out var instance))
        {
            return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");
        }

        if (!string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(instance.UserId, userId, StringComparison.Ordinal))
        {
            return ErrorEnvelope("Access denied to this workflow instance.", "ACCESS_DENIED");
        }

        if (instance.StateVersion != expectedStateVersion)
        {
            return ErrorEnvelope(
                $"State version mismatch: expected {expectedStateVersion}, actual {instance.StateVersion}.",
                "VERSION_MISMATCH");
        }

        if (!_definitions.TryGetValue(instance.WorkflowKey, out var definition))
        {
            return ErrorEnvelope($"Workflow '{instance.WorkflowKey}' not found.", "DEFINITION_NOT_FOUND");
        }

        if (action.StartsWith("change:", StringComparison.OrdinalIgnoreCase))
        {
            var targetStateKey = action["change:".Length..];
            if (definition.States.All(s => s.StateKey != targetStateKey))
            {
                return ErrorEnvelope($"State '{targetStateKey}' not found in definition.", "STATE_NOT_FOUND");
            }

            var jumped = instance with
            {
                CurrentState = targetStateKey,
                StateVersion = instance.StateVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            SaveInstance(jumped);
            Logger.LogInformation(
                "Change-link: jumped instance {Id} to state '{State}'",
                instanceId,
                targetStateKey);
            return BuildEnvelope(jumped, definition);
        }

        var transition = definition.Transitions.FirstOrDefault(
            t => t.FromState == instance.CurrentState
                 && t.Action == action
                 && t.RequiresRole == null);

        if (transition == null)
        {
            return ErrorEnvelope(
                $"Action '{action}' is not valid from state '{instance.CurrentState}'.",
                "INVALID_TRANSITION");
        }

        if (ValidateAdvance(instance, definition, fieldValues) is { } validationEnvelope)
        {
            return validationEnvelope;
        }

        // Check if the target is a gateway rather than a plain stage.
        var nextGateway = FindGateway(definition, transition.ToState);
        if (nextGateway != null)
        {
            return string.Equals(nextGateway.GatewayType, "Split", StringComparison.Ordinal)
                ? HandleSplitGatewayAdvance(instance, definition, transition, nextGateway, fieldValues)
                : HandleJoinGatewayAdvance(instance, definition, transition, nextGateway, fieldValues);
        }

        // Regular stage transition (single- or multi-cursor).
        if (instance.Cursors.Count > 0)
        {
            // Multi-cursor: advance only the cursor currently at this stage.
            var sourceCursor = instance.Cursors.FirstOrDefault(c => c.CurrentNodeKey == instance.CurrentState && !c.IsAtGateway);
            var updatedCursors = MoveCursor(instance.Cursors, sourceCursor?.CursorId, transition.ToState, isAtGateway: false);
            var primaryState = FirstActiveStageCursorKey(updatedCursors) ?? transition.ToState;
            var updatedMulti = instance with
            {
                CurrentState = primaryState,
                Cursors = updatedCursors,
                StateVersion = instance.StateVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                FieldValues = Merge(instance.FieldValues, fieldValues)
            };
            SaveInstance(updatedMulti);
            Logger.LogInformation(
                "Multi-cursor advance instance {Id}: cursor {CursorId} → {To}",
                instanceId, sourceCursor?.CursorId ?? "(none)", transition.ToState);
            return BuildEnvelope(updatedMulti, definition);
        }

        var updated = instance with
        {
            CurrentState = transition.ToState,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            FieldValues = Merge(instance.FieldValues, fieldValues)
        };

        SaveInstance(updated);
        Logger.LogInformation(
            "Advanced instance {Id}: {From} → {To}",
            instanceId,
            instance.CurrentState,
            transition.ToState);

        return BuildEnvelope(updated, definition);
    }

    public IEnumerable<WorkflowInstanceState> GetAllInstances() => _instancesById.Values;

    public WorkflowInstanceListEnvelope GetInstances(string tenantId, string userId)
    {
        var userInstances = _instancesById.Values
            .Where(i => string.Equals(i.TenantId, tenantId, StringComparison.Ordinal)
                     && string.Equals(i.UserId, userId, StringComparison.Ordinal))
            .Select(instance =>
            {
                _definitions.TryGetValue(instance.WorkflowKey, out var definition);
                var state = definition?.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
                var stepType = state?.Components.InferStepType() ?? "question";

                return new WorkflowInstanceSummary
                {
                    InstanceId = instance.InstanceId,
                    WorkflowKey = instance.WorkflowKey,
                    WorkflowDisplayName = definition?.DisplayName ?? instance.WorkflowKey,
                    CurrentStateKey = instance.CurrentState,
                    CurrentStateDisplayName = state?.DisplayName ?? instance.CurrentState,
                    StepType = stepType,
                    CreatedAt = instance.CreatedAt.DateTime,
                    LastUpdatedAt = instance.UpdatedAt.DateTime,
                    CanContinue = stepType != "confirmation",
                    IsCompleted = stepType == "confirmation",
                    WorkflowPageUrl = null,
                    InstancePolicy = definition?.InstancePolicy ?? "single"
                };
            })
            .ToList();

        return new WorkflowInstanceListEnvelope
        {
            Instances = userInstances
        };
    }

    public IEnumerable<WorkflowDefinitionFile> GetAllDefinitions() => _definitions.Values;

    public WorkflowDefinitionFile? GetDefinition(string key) =>
        _definitions.TryGetValue(key, out var definition) ? definition : null;

    public bool UpdateDefinition(string key, WorkflowDefinitionFile updated)
    {
        if (!_definitions.ContainsKey(key))
        {
            return false;
        }

        _definitions[key] = updated;
        Logger.LogInformation("Workflow definition updated in-memory: {Key}", key);
        return true;
    }

    public bool Reset(string instanceId)
    {
        if (!_instancesById.TryRemove(instanceId, out var instance))
        {
            return false;
        }

        var lookupKey = LookupKey(instance.TenantId, instance.UserId, instance.WorkflowKey);
        _instanceLookup.TryRemove(lookupKey, out _);
        Logger.LogInformation("Reset (deleted) instance {Id}", instanceId);
        return true;
    }

    public void ResetAll()
    {
        _instancesById.Clear();
        _instanceLookup.Clear();
        Logger.LogInformation("ResetAll: all workflow instances cleared");
    }

    protected virtual WorkflowResponseEnvelope? ValidateAdvance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        Dictionary<string, object?>? fieldValues) => null;

    protected virtual WorkflowResponseEnvelope? InitializeNewInstance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        string? action) => null;

    protected bool TryGetInstance(string instanceId, out WorkflowInstanceState instance) =>
        _instancesById.TryGetValue(instanceId, out instance!);

    protected void SaveInstance(WorkflowInstanceState instance) =>
        _instancesById[instance.InstanceId] = instance;

    protected WorkflowResponseEnvelope BuildEnvelope(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition)
    {
        // In multi-cursor mode the CurrentState already reflects the first active stage cursor.
        // If all remaining cursors are at join gateways (waiting), show the join waiting information
        // for the owning lane rather than looking for a stage.
        if (instance.Cursors.Count > 0)
        {
            var stageCursors = instance.Cursors.Where(c => !c.IsAtGateway).ToList();
            if (stageCursors.Count == 0)
            {
                // All active cursors are at join gateways — show the first join's waiting info.
                var joinCursor = instance.Cursors.First(c => c.IsAtGateway);
                var joinGateway = FindGateway(definition, joinCursor.CurrentNodeKey);
                if (joinGateway != null)
                {
                    return BuildJoinWaitingEnvelope(instance, definition, joinGateway);
                }
            }
        }

        var state = definition.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
        if (state == null)
        {
            return ErrorEnvelope(
                $"State '{instance.CurrentState}' not found in definition '{definition.DefinitionKey}'.",
                "STATE_NOT_FOUND");
        }

        var actions = definition.Transitions
            .Where(t => t.FromState == instance.CurrentState && t.RequiresRole == null)
            .Select(t => new WorkflowAction
            {
                ActionKey = t.Action,
                Label = ActionLabel(t.Action),
                Style = ActionStyle(t.Action)
            })
            .ToArray();

        var components = BuildComponents(state.Components, instance.FieldValues);
        var effectiveStepType = state.Components.InferStepType();
        var waitingComponent = state.Components.OfType<WaitingComponent>().FirstOrDefault();

        var render = new StepContent
        {
            StepType = effectiveStepType,
            StateDisplayName = state.DisplayName,
            Components = components,
            AvailableActions = actions
        };

        var responseState = effectiveStepType switch
        {
            "status-timeline" => "defer",
            "confirmation" => "complete",
            _ => "render"
        };

        return new WorkflowResponseEnvelope
        {
            InstanceId = instance.InstanceId,
            ResponseState = responseState,
            StateVersion = instance.StateVersion,
            CorrelationId = instance.InstanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            PollAfterMs = waitingComponent?.PollIntervalMs,
            Render = render,
            InstancePolicy = definition.InstancePolicy
        };
    }

    protected static WorkflowResponseEnvelope ErrorEnvelope(string message, string code) =>
        new()
        {
            InstanceId = string.Empty,
            ResponseState = "error",
            StateVersion = 0,
            CorrelationId = Guid.NewGuid().ToString(),
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Problems = [new WorkflowProblem { FieldKey = string.Empty, Message = message, Code = code }]
        };

    private WorkflowResponseEnvelope CreateAndRegisterNewInstance(
        string workflowKey,
        string tenantId,
        string userId,
        WorkflowDefinitionFile definition,
        string lookupKey,
        string? action,
        bool persistLookup,
        string logMessage,
        params object?[] additionalLogArgs)
    {
        var instance = CreateNewInstance(workflowKey, tenantId, userId, definition.InitialState);
        if (InitializeNewInstance(instance, definition, action) is { } error)
        {
            return error;
        }

        _instancesById[instance.InstanceId] = instance;
        if (persistLookup)
        {
            _instanceLookup[lookupKey] = instance.InstanceId;
        }

        Logger.LogInformation(logMessage, [instance.InstanceId, .. additionalLogArgs]);
        return BuildEnvelope(instance, definition);
    }

    private static WorkflowInstanceState CreateNewInstance(
        string workflowKey,
        string tenantId,
        string userId,
        string initialState)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowInstanceState
        {
            InstanceId = Guid.NewGuid().ToString(),
            WorkflowKey = workflowKey,
            TenantId = tenantId,
            UserId = userId,
            CurrentState = initialState,
            StateVersion = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private PrismComponentRenderPayload[] BuildComponents(
        IReadOnlyList<PrismComponent> componentDefinitions,
        Dictionary<string, object?> savedValues)
    {
        var result = new List<PrismComponentRenderPayload>();

        foreach (var component in componentDefinitions)
        {
            switch (component)
            {
                case FieldsetComponent fieldset:
                {
                    var fields = BuildFields(fieldset.Children, savedValues);
                    if (fields.Length == 0)
                    {
                        Logger.LogWarning("Fieldset component contains no renderable fields");
                        continue;
                    }

                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "fieldset",
                        Legend = fieldset.Legend,
                        LegendSize = fieldset.LegendSize,
                        Fields = fields
                    });
                    break;
                }

                case SummaryListComponent summary:
                {
                    var fields = BuildFields(summary.Children, savedValues);
                    if (fields.Length == 0)
                    {
                        Logger.LogWarning("Summary-list component contains no renderable fields");
                        continue;
                    }

                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "summary-list",
                        Title = summary.Title,
                        SourceStateKey = summary.ChangeStateKey,
                        Fields = fields
                    });
                    break;
                }

                case AccordionComponent accordion:
                {
                    var sections = accordion.Sections
                        .Select(section => new PrismAccordionSectionPayload
                        {
                            Heading = section.Heading,
                            Summary = section.Summary,
                            Fields = BuildFields(section.Children, savedValues)
                        })
                        .ToArray();

                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "accordion",
                        AccordionSections = sections
                    });
                    break;
                }

                case WaitingComponent waiting:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "waiting",
                        Content = _sanitizer.Sanitize(waiting.Content),
                        ExpectedWaitSeconds = waiting.ExpectedWaitSeconds,
                        PollIntervalMs = waiting.PollIntervalMs,
                        AllowDefer = waiting.AllowDefer,
                        DeferMessage = waiting.DeferMessage
                    });
                    break;

                case PanelComponent panel:
                    result.Add(new PrismComponentRenderPayload { Type = "panel", Heading = panel.Heading });
                    break;

                case BodyComponent body:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "body",
                        Content = _sanitizer.Sanitize(body.Content)
                    });
                    break;

                case HeadingComponent heading:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "heading",
                        Content = heading.Content,
                        Level = heading.Level
                    });
                    break;

                case InsetTextComponent inset:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "inset-text",
                        Content = _sanitizer.Sanitize(inset.Content)
                    });
                    break;

                case WarningTextComponent warning:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "warning-text",
                        Content = _sanitizer.Sanitize(warning.Content)
                    });
                    break;

                case DetailsComponent details:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "details",
                        Heading = details.Heading,
                        Content = _sanitizer.Sanitize(details.Content)
                    });
                    break;

                case NotificationBannerComponent banner:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "notification-banner",
                        Heading = banner.Heading,
                        Content = _sanitizer.Sanitize(banner.Content),
                        BannerType = banner.BannerType
                    });
                    break;

                case TaskListComponent taskList:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "task-list",
                        TaskSections = taskList.Sections?.Select(section => new PrismTaskSection
                        {
                            Heading = section.Heading,
                            Tasks = section.Tasks.Select(task => new PrismTaskItem
                            {
                                Label = task.Label,
                                Href = task.Href ?? task.StateKey,
                                Status = "not-started"
                            }).ToArray()
                        }).ToArray()
                    });
                    break;

                case InputComponent input:
                {
                    var fields = BuildFields(new[] { (PrismComponent)input }, savedValues);
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "fieldset",
                        Fields = fields
                    });
                    break;
                }
            }
        }

        return result.ToArray();
    }

    private static FieldRenderPayload[] BuildFields(
        IEnumerable<PrismComponent> children,
        Dictionary<string, object?> savedValues)
    {
        var fields = new List<FieldRenderPayload>();

        foreach (var child in children)
        {
            switch (child)
            {
                case InputComponent input:
                    fields.Add(BuildInputPayload(input, savedValues));

                    var conditional = (child as RadiosComponent)?.ConditionalChildren
                                      ?? (child as CheckboxesComponent)?.ConditionalChildren;
                    if (conditional != null)
                    {
                        foreach (var (optionValue, subComponents) in conditional)
                        {
                            foreach (var sub in subComponents.GetAllInputs())
                            {
                                fields.Add(BuildInputPayload(sub, savedValues) with
                                {
                                    ConditionalOn = input.FieldKey,
                                    VisibleWhen = optionValue
                                });
                            }
                        }
                    }

                    break;

                case FieldsetComponent nestedFieldset:
                    fields.AddRange(BuildFields(nestedFieldset.Children, savedValues));
                    break;
            }
        }

        return fields.ToArray();
    }

    private static FieldRenderPayload BuildInputPayload(
        InputComponent input,
        Dictionary<string, object?> savedValues)
    {
        var fieldType = InputFieldType(input);
        return new FieldRenderPayload
        {
            FieldKey = input.FieldKey,
            Label = input.Label,
            Hint = input.Hint,
            FieldType = fieldType,
            Required = input.Required,
            Options = input switch
            {
                SelectComponent select => select.Options,
                RadiosComponent radios => radios.Options,
                CheckboxesComponent checkboxes => checkboxes.Options,
                _ => null
            },
            Value = GetDisplayValue(input, fieldType, savedValues),
            MinLength = input switch
            {
                TextInputComponent text => text.MinLength,
                TextareaComponent textarea => textarea.MinLength,
                _ => null
            },
            MaxLength = input switch
            {
                TextInputComponent text => text.MaxLength,
                TextareaComponent textarea => textarea.MaxLength,
                _ => null
            },
            Pattern = input switch
            {
                TextInputComponent text => text.Pattern,
                EmailComponent email => email.Pattern,
                TelComponent tel => tel.Pattern,
                _ => null
            },
            Min = input switch
            {
                NumberInputComponent number => number.Min,
                DecimalInputComponent decimalInput => decimalInput.Min,
                _ => null
            },
            Max = input switch
            {
                NumberInputComponent number => number.Max,
                DecimalInputComponent decimalInput => decimalInput.Max,
                _ => null
            },
            Prefix = input switch
            {
                TextInputComponent text => text.Prefix,
                NumberInputComponent number => number.Prefix,
                DecimalInputComponent decimalInput => decimalInput.Prefix,
                _ => null
            },
            ConditionalOn = input.ConditionalOn,
            VisibleWhen = input.VisibleWhen
        };
    }

    private static string InputFieldType(InputComponent input) => input switch
    {
        TextInputComponent => "text",
        NumberInputComponent => "number",
        DecimalInputComponent => "decimal",
        SelectComponent => "select",
        RadiosComponent => "radio",
        CheckboxesComponent => "checkboxlist",
        DateInputComponent => "date",
        EmailComponent => "email",
        TelComponent => "tel",
        TextareaComponent => "textarea",
        BooleanComponent => "boolean",
        _ => "text"
    };

    private static object? GetDisplayValue(
        InputComponent input,
        string fieldType,
        Dictionary<string, object?> savedValues)
    {
        var raw = savedValues.TryGetValue(input.FieldKey, out var value) ? value : null;
        if (raw == null)
        {
            return null;
        }

        if (fieldType == "checkboxlist" || fieldType == "checkboxes")
        {
            var rawString = raw switch
            {
                string stringValue => stringValue,
                JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
                _ => null
            };

            if (rawString != null)
            {
                raw = string.Join(
                    ", ",
                    rawString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        var prefix = input switch
        {
            TextInputComponent text => text.Prefix,
            NumberInputComponent number => number.Prefix,
            DecimalInputComponent decimalInput => decimalInput.Prefix,
            _ => null
        };

        return !string.IsNullOrEmpty(prefix)
            ? $"{prefix}{raw}"
            : raw;
    }

    // ─── Gateway helpers ──────────────────────────────────────────────────────

    private static WorkflowGatewayDefinition? FindGateway(WorkflowDefinitionFile definition, string nodeKey) =>
        definition.Metadata?.Gateways?.FirstOrDefault(g =>
            string.Equals(g.Key, nodeKey, StringComparison.Ordinal));

    private WorkflowResponseEnvelope HandleSplitGatewayAdvance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowTransitionFile arrivingTransition,
        WorkflowGatewayDefinition splitGateway,
        Dictionary<string, object?>? fieldValues)
    {
        // Find all outgoing branches from the split gateway.
        // Split gateway transitions carry the action "split-auto" by convention or any action
        // — we follow ALL outgoing transitions from the gateway deterministically.
        var outgoing = definition.Transitions
            .Where(t => string.Equals(t.FromState, splitGateway.Key, StringComparison.Ordinal))
            .OrderBy(t => t.ToState, StringComparer.Ordinal)
            .ToList();

        if (outgoing.Count == 0)
        {
            return ErrorEnvelope(
                $"Split gateway '{splitGateway.Key}' has no outgoing transitions.",
                "GATEWAY_NO_OUTGOING");
        }

        // Identify the cursor being advanced (if we are already in multi-cursor mode).
        var sourceCursorId = instance.Cursors
            .FirstOrDefault(c => c.CurrentNodeKey == instance.CurrentState && !c.IsAtGateway)?.CursorId;

        // Remove the arriving cursor (or primary state in single-cursor mode) and fan out.
        var remainingCursors = sourceCursorId != null
            ? instance.Cursors.Where(c => c.CursorId != sourceCursorId).ToList()
            : new List<WorkflowCursor>();

        var newCursors = outgoing.Select(t =>
        {
            var targetGateway = FindGateway(definition, t.ToState);
            var targetLaneKey = targetGateway?.LaneKey
                                ?? definition.States.FirstOrDefault(s => s.StateKey == t.ToState)?.Metadata?.LaneKey
                                ?? splitGateway.LaneKey;

            return new WorkflowCursor
            {
                CursorId = Guid.NewGuid().ToString(),
                LaneKey = targetLaneKey,
                CurrentNodeKey = t.ToState,
                IsAtGateway = targetGateway != null
            };
        }).ToList();

        var allCursors = remainingCursors.Concat(newCursors).ToArray();
        var primaryState = FirstActiveStageCursorKey(allCursors) ?? newCursors[0].CurrentNodeKey;

        var updated = instance with
        {
            CurrentState = primaryState,
            Cursors = allCursors,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            FieldValues = Merge(instance.FieldValues, fieldValues)
        };

        SaveInstance(updated);
        Logger.LogInformation(
            "Split gateway '{Gateway}': instance {Id} fanned out to {Count} cursors.",
            splitGateway.Key, instance.InstanceId, newCursors.Count);

        return BuildEnvelope(updated, definition);
    }

    private WorkflowResponseEnvelope HandleJoinGatewayAdvance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowTransitionFile arrivingTransition,
        WorkflowGatewayDefinition joinGateway,
        Dictionary<string, object?>? fieldValues)
    {
        var gatewayKey = joinGateway.Key;
        var requiredLanes = joinGateway.RequiredIncomingLanes ?? [];

        // Identify the arriving cursor.
        var arrivingCursor = instance.Cursors.Count > 0
            ? instance.Cursors.FirstOrDefault(c => c.CurrentNodeKey == instance.CurrentState && !c.IsAtGateway)
            : new WorkflowCursor
            {
                CursorId = Guid.NewGuid().ToString(),
                LaneKey = joinGateway.LaneKey,
                CurrentNodeKey = instance.CurrentState,
                IsAtGateway = false
            };

        var arrivingCursorId = arrivingCursor?.CursorId ?? Guid.NewGuid().ToString();
        var arrivingLaneKey = arrivingCursor?.LaneKey ?? joinGateway.LaneKey;

        // Record arrival in join token bookkeeping.
        var existingArrivals = instance.JoinArrivals.TryGetValue(gatewayKey, out var existing)
            ? existing.ToList()
            : new List<string>();

        if (!existingArrivals.Contains(arrivingCursorId))
            existingArrivals.Add(arrivingCursorId);

        // Move the arriving cursor to the join gateway.
        var cursorsAfterArrival = instance.Cursors.Count > 0
            ? MoveCursor(instance.Cursors, arrivingCursor?.CursorId, gatewayKey, isAtGateway: true)
            : [new WorkflowCursor { CursorId = arrivingCursorId, LaneKey = arrivingLaneKey, CurrentNodeKey = gatewayKey, IsAtGateway = true }];

        var updatedArrivals = new Dictionary<string, IReadOnlyList<string>>(instance.JoinArrivals)
        {
            [gatewayKey] = existingArrivals
        };

        // Check if all required lanes have a cursor at this gateway.
        // A lane is "arrived" when at least one of its cursors is in the join arrivals list.
        var arrivedCursorIds = new HashSet<string>(existingArrivals, StringComparer.Ordinal);
        var arrivedLanes = cursorsAfterArrival
            .Where(c => c.IsAtGateway && string.Equals(c.CurrentNodeKey, gatewayKey, StringComparison.Ordinal)
                                      && arrivedCursorIds.Contains(c.CursorId))
            .Select(c => c.LaneKey)
            .ToHashSet(StringComparer.Ordinal);

        var allRequiredArrived = requiredLanes.Count == 0 || requiredLanes.All(l => arrivedLanes.Contains(l));

        if (!allRequiredArrived)
        {
            // Waiting — record arrival but do not release.
            var waitingInstance = instance with
            {
                CurrentState = FirstActiveStageCursorKey(cursorsAfterArrival) ?? gatewayKey,
                Cursors = cursorsAfterArrival,
                JoinArrivals = updatedArrivals,
                StateVersion = instance.StateVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                FieldValues = Merge(instance.FieldValues, fieldValues)
            };

            SaveInstance(waitingInstance);
            Logger.LogInformation(
                "Join gateway '{Gateway}': instance {Id} waiting ({Arrived}/{Required} lanes).",
                gatewayKey, instance.InstanceId, arrivedLanes.Count, requiredLanes.Count);

            return BuildJoinWaitingEnvelope(waitingInstance, definition, joinGateway);
        }

        // All required lanes arrived — release the join.
        var outgoing = definition.Transitions
            .Where(t => string.Equals(t.FromState, gatewayKey, StringComparison.Ordinal))
            .OrderBy(t => t.ToState, StringComparer.Ordinal)
            .ToList();

        if (outgoing.Count == 0)
        {
            return ErrorEnvelope(
                $"Join gateway '{gatewayKey}' has no outgoing transitions.",
                "GATEWAY_NO_OUTGOING");
        }

        // Remove all cursors that were held at this join and create the release cursor(s).
        var cursorsWithoutJoin = cursorsAfterArrival
            .Where(c => !(c.IsAtGateway && string.Equals(c.CurrentNodeKey, gatewayKey, StringComparison.Ordinal)))
            .ToList();

        var releaseCursors = outgoing.Select(t =>
        {
            var targetGateway = FindGateway(definition, t.ToState);
            return new WorkflowCursor
            {
                CursorId = Guid.NewGuid().ToString(),
                LaneKey = targetGateway?.LaneKey
                          ?? definition.States.FirstOrDefault(s => s.StateKey == t.ToState)?.Metadata?.LaneKey
                          ?? joinGateway.LaneKey,
                CurrentNodeKey = t.ToState,
                IsAtGateway = targetGateway != null
            };
        }).ToList();

        var releasedCursors = cursorsWithoutJoin.Concat(releaseCursors).ToArray();

        // Clean up join arrivals for this gateway.
        var cleanedArrivals = new Dictionary<string, IReadOnlyList<string>>(updatedArrivals);
        cleanedArrivals.Remove(gatewayKey);

        var primaryStateAfterRelease = FirstActiveStageCursorKey(releasedCursors) ?? outgoing[0].ToState;

        var releasedInstance = instance with
        {
            CurrentState = primaryStateAfterRelease,
            Cursors = releasedCursors,
            JoinArrivals = cleanedArrivals,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            FieldValues = Merge(instance.FieldValues, fieldValues)
        };

        SaveInstance(releasedInstance);
        Logger.LogInformation(
            "Join gateway '{Gateway}': instance {Id} released. All {Count} required lanes arrived.",
            gatewayKey, instance.InstanceId, requiredLanes.Count);

        return BuildEnvelope(releasedInstance, definition);
    }

    private WorkflowResponseEnvelope BuildJoinWaitingEnvelope(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowGatewayDefinition joinGateway)
    {
        var waitingContent = joinGateway.WaitingContent
                             ?? "Please wait while other parts of this workflow are completed.";
        var pollMs = joinGateway.WaitingPollIntervalMs > 0 ? joinGateway.WaitingPollIntervalMs : 3000;
        var expectedSeconds = joinGateway.WaitingExpectedSeconds > 0 ? joinGateway.WaitingExpectedSeconds : 30;

        var waitingArrivals = instance.JoinArrivals.TryGetValue(joinGateway.Key, out var arr) ? arr : [];
        var requiredLanes = joinGateway.RequiredIncomingLanes ?? [];
        var pendingLanes = requiredLanes
            .Where(l => instance.Cursors.All(c =>
                !(c.IsAtGateway
                  && string.Equals(c.CurrentNodeKey, joinGateway.Key, StringComparison.Ordinal)
                  && string.Equals(c.LaneKey, l, StringComparison.Ordinal))))
            .ToArray();

        var statusContent = pendingLanes.Length > 0
            ? $"{waitingContent} Waiting for: {string.Join(", ", pendingLanes)}."
            : waitingContent;

        var render = new StepContent
        {
            StepType = "status-timeline",
            StateDisplayName = joinGateway.DisplayName,
            Components =
            [
                new PrismComponentRenderPayload
                {
                    Type = "waiting",
                    Content = statusContent,
                    ExpectedWaitSeconds = expectedSeconds,
                    PollIntervalMs = pollMs,
                    AllowDefer = true
                }
            ],
            AvailableActions = Array.Empty<WorkflowAction>()
        };

        return new WorkflowResponseEnvelope
        {
            InstanceId = instance.InstanceId,
            ResponseState = "defer",
            StateVersion = instance.StateVersion,
            CorrelationId = instance.InstanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            PollAfterMs = pollMs,
            Render = render,
            InstancePolicy = definition.InstancePolicy
        };
    }

    private static IReadOnlyList<WorkflowCursor> MoveCursor(
        IReadOnlyList<WorkflowCursor> cursors,
        string? cursorId,
        string newNodeKey,
        bool isAtGateway)
    {
        if (cursorId == null)
            return cursors;

        return cursors
            .Select(c => c.CursorId == cursorId
                ? c with { CurrentNodeKey = newNodeKey, IsAtGateway = isAtGateway }
                : c)
            .ToArray();
    }

    private static string? FirstActiveStageCursorKey(IReadOnlyList<WorkflowCursor> cursors) =>
        cursors.FirstOrDefault(c => !c.IsAtGateway)?.CurrentNodeKey;

    // ─── end Gateway helpers ──────────────────────────────────────────────────

    private static string LookupKey(string tenantId, string userId, string workflowKey) =>
        $"{tenantId}:{userId}:{workflowKey}";

    private static Dictionary<string, object?> Merge(
        Dictionary<string, object?> existing,
        Dictionary<string, object?>? incoming)
    {
        if (incoming == null || incoming.Count == 0)
        {
            return existing;
        }

        var merged = new Dictionary<string, object?>(existing);
        foreach (var kvp in incoming)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private static string ActionLabel(string key) => key switch
    {
        "submit" => "Submit",
        "save-draft" => "Save Draft",
        "start-another" => "Start Another",
        "approve" => "Approve",
        "request-changes" => "Request Changes",
        _ => key
    };

    private static string ActionStyle(string key) => key switch
    {
        "submit" or "approve" => "primary",
        "reject" or "cancel" => "destructive",
        _ => "secondary"
    };
}
