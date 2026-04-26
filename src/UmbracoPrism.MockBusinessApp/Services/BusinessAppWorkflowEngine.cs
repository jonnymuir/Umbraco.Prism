using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Singleton workflow engine for the Mock Business App.
/// Loads workflow definitions from <c>workflow-seeds/</c> at startup and maintains
/// in-memory instance state — simulating a real business application's workflow service.
/// </summary>
public class BusinessAppWorkflowEngine
{
    private readonly ILogger<BusinessAppWorkflowEngine> _logger;
    private readonly Dictionary<string, WorkflowDefinitionFile> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WorkflowInstanceState> _instancesById = new();

    /// <summary>
    /// Secondary index: "{tenantId}:{userId}:{workflowKey}" → instanceId.
    /// Enables resuming the active instance when a user returns to a workflow.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _instanceLookup = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public BusinessAppWorkflowEngine(ILogger<BusinessAppWorkflowEngine> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        LoadSeedData(env.ContentRootPath);
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the current state for the given user/tenant/workflow combination,
    /// creating a fresh instance if none exists.
    /// </summary>
    /// <param name="workflowKey">The workflow definition key to start or resume.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="instanceId">Optional specific instance ID to resume (used by "multiple" policy).</param>
    /// <param name="action">Optional action: "start-new" or "resume" (used by "prompt" policy).</param>
    /// <returns>A WorkflowResponseEnvelope describing the current state and what to render.</returns>
    /// <remarks>
    /// Behaviour depends on instancePolicy and parameters:
    /// - If instanceId is provided: resume that specific instance (validate tenant+user ownership).
    /// - If action="start-new": create a new instance and update lookup key.
    /// - If action="resume": behave like "single" (find or create via lookup key).
    /// - Otherwise, based on definition.InstancePolicy:
    ///   - "single": find or create via lookup key (current behaviour).
    ///   - "multiple": always create a new instance (no reuse).
    ///   - "prompt": if active instance exists, return "instance_picker" response; else create new.
    /// </remarks>
    public WorkflowResponseEnvelope GetCurrent(string workflowKey, string tenantId, string userId, string? instanceId = null, string? action = null)
    {
        if (!_definitions.TryGetValue(workflowKey, out var definition))
        {
            _logger.LogWarning("Workflow definition not found: {Key}", workflowKey);
            return ErrorEnvelope($"Workflow '{workflowKey}' is not registered with this application.", "DEFINITION_NOT_FOUND");
        }

        // If specific instanceId provided, resume that instance
        if (!string.IsNullOrEmpty(instanceId))
        {
            if (!_instancesById.TryGetValue(instanceId, out var specificInstance))
                return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");

            if (!string.Equals(specificInstance.TenantId, tenantId, StringComparison.Ordinal)
                || !string.Equals(specificInstance.UserId, userId, StringComparison.Ordinal))
                return ErrorEnvelope("Access denied to this workflow instance.", "ACCESS_DENIED");

            _logger.LogInformation("Resuming specific instance {Id}", instanceId);
            return BuildEnvelope(specificInstance, definition);
        }

        var lookupKey = LookupKey(tenantId, userId, workflowKey);

        // If action="start-new", create a fresh instance and update lookup
        if (string.Equals(action, "start-new", StringComparison.OrdinalIgnoreCase))
        {
            var newInstanceId = Guid.NewGuid().ToString();
            var newInstance = new WorkflowInstanceState
            {
                InstanceId = newInstanceId,
                WorkflowKey = workflowKey,
                TenantId = tenantId,
                UserId = userId,
                CurrentState = definition.InitialState,
                StateVersion = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _instancesById[newInstanceId] = newInstance;
            _instanceLookup[lookupKey] = newInstanceId;
            _logger.LogInformation("Created new workflow instance {Id} for key={Key} (action=start-new)", newInstanceId, workflowKey);
            return BuildEnvelope(newInstance, definition);
        }

        // If action="resume", behave like "single" (find or create via lookup)
        if (string.Equals(action, "resume", StringComparison.OrdinalIgnoreCase))
        {
            if (_instanceLookup.TryGetValue(lookupKey, out var resumeInstanceId)
                && _instancesById.TryGetValue(resumeInstanceId, out var resumeInstance))
            {
                _logger.LogInformation("Resuming existing instance {Id} (action=resume)", resumeInstanceId);
                return BuildEnvelope(resumeInstance, definition);
            }

            var newInstanceId = Guid.NewGuid().ToString();
            var newInstance = new WorkflowInstanceState
            {
                InstanceId = newInstanceId,
                WorkflowKey = workflowKey,
                TenantId = tenantId,
                UserId = userId,
                CurrentState = definition.InitialState,
                StateVersion = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _instancesById[newInstanceId] = newInstance;
            _instanceLookup[lookupKey] = newInstanceId;
            _logger.LogInformation("Created workflow instance {Id} for key={Key} (action=resume, no existing)", newInstanceId, workflowKey);
            return BuildEnvelope(newInstance, definition);
        }

        // Policy-based behaviour
        var policy = definition.InstancePolicy;

        if (string.Equals(policy, "multiple", StringComparison.OrdinalIgnoreCase))
        {
            // Always create a new instance (no reuse)
            var multipleInstanceId = Guid.NewGuid().ToString();
            var multipleInstance = new WorkflowInstanceState
            {
                InstanceId = multipleInstanceId,
                WorkflowKey = workflowKey,
                TenantId = tenantId,
                UserId = userId,
                CurrentState = definition.InitialState,
                StateVersion = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _instancesById[multipleInstanceId] = multipleInstance;
            _logger.LogInformation("Created new workflow instance {Id} for key={Key} (policy=multiple)", multipleInstanceId, workflowKey);
            return BuildEnvelope(multipleInstance, definition);
        }

        if (string.Equals(policy, "prompt", StringComparison.OrdinalIgnoreCase))
        {
            // Check if an active (non-terminal) instance exists
            if (_instanceLookup.TryGetValue(lookupKey, out var promptInstanceId)
                && _instancesById.TryGetValue(promptInstanceId, out var promptInstance))
            {
                // Check if instance is terminal
                var currentState = definition.States.FirstOrDefault(s => s.StateKey == promptInstance.CurrentState);
                bool isTerminal = currentState != null && currentState.Components.InferStepType() == "confirmation";

                if (!isTerminal)
                {
                    // Return instance_picker response
                    _logger.LogInformation("Active instance {Id} exists for key={Key}; returning instance_picker", promptInstanceId, workflowKey);
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

            // No active instance: create new
            var newPromptInstanceId = Guid.NewGuid().ToString();
            var newPromptInstance = new WorkflowInstanceState
            {
                InstanceId = newPromptInstanceId,
                WorkflowKey = workflowKey,
                TenantId = tenantId,
                UserId = userId,
                CurrentState = definition.InitialState,
                StateVersion = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _instancesById[newPromptInstanceId] = newPromptInstance;
            _instanceLookup[lookupKey] = newPromptInstanceId;
            _logger.LogInformation("Created workflow instance {Id} for key={Key} (policy=prompt, no active)", newPromptInstanceId, workflowKey);
            return BuildEnvelope(newPromptInstance, definition);
        }

        // Default "single" behaviour: find or create via lookup key
        if (!_instanceLookup.TryGetValue(lookupKey, out var singleInstanceId)
            || !_instancesById.TryGetValue(singleInstanceId, out var singleInstance))
        {
            singleInstanceId = Guid.NewGuid().ToString();
            singleInstance = new WorkflowInstanceState
            {
                InstanceId = singleInstanceId,
                WorkflowKey = workflowKey,
                TenantId = tenantId,
                UserId = userId,
                CurrentState = definition.InitialState,
                StateVersion = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _instancesById[singleInstanceId] = singleInstance;
            _instanceLookup[lookupKey] = singleInstanceId;
            _logger.LogInformation("Created workflow instance {Id} for key={Key} tenant={Tenant}", singleInstanceId, workflowKey, tenantId);
        }

        return BuildEnvelope(singleInstance, definition);
    }

    /// <summary>
    /// Advances a workflow instance on behalf of a member (non-reviewer action).
    /// Validates access and state version, applies the transition, and updates the instance.
    /// </summary>
    /// <param name="instanceId">The instance to advance.</param>
    /// <param name="tenantId">The tenant ID; must match the instance's tenant for access control.</param>
    /// <param name="userId">The user ID; must match the instance's owner for access control.</param>
    /// <param name="action">The action to perform (e.g. "submit", "save-draft").</param>
    /// <param name="expectedStateVersion">The state version the client expects; used for optimistic concurrency control.</param>
    /// <param name="fieldValues">Field values collected from the form (if any).</param>
    /// <returns>A WorkflowResponseEnvelope describing the next state.</returns>
    /// <remarks>
    /// This method performs three validations: instance existence, access control (tenant/user match), 
    /// and optimistic concurrency (state version). The transition is looked up by fromState, action, 
    /// and RequiresRole == null (user, not reviewer). On success, the instance is updated and the new state is returned.
    /// </remarks>
    public WorkflowResponseEnvelope Advance(
        string instanceId, string tenantId, string userId,
        string action, int expectedStateVersion,
        Dictionary<string, object?>? fieldValues)
    {
        if (!_instancesById.TryGetValue(instanceId, out var instance))
            return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");

        if (!string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(instance.UserId, userId, StringComparison.Ordinal))
            return ErrorEnvelope("Access denied to this workflow instance.", "ACCESS_DENIED");

        if (instance.StateVersion != expectedStateVersion)
            return ErrorEnvelope(
                $"State version mismatch: expected {expectedStateVersion}, actual {instance.StateVersion}.",
                "VERSION_MISMATCH");

        if (!_definitions.TryGetValue(instance.WorkflowKey, out var definition))
            return ErrorEnvelope($"Workflow '{instance.WorkflowKey}' not found.", "DEFINITION_NOT_FOUND");

        // Handle change-link navigation: jump directly to a named state (from check-answers).
        if (action.StartsWith("change:", StringComparison.OrdinalIgnoreCase))
        {
            var targetStateKey = action["change:".Length..];
            if (definition.States.All(s => s.StateKey != targetStateKey))
                return ErrorEnvelope($"State '{targetStateKey}' not found in definition.", "STATE_NOT_FOUND");

            var jumped = instance with
            {
                CurrentState = targetStateKey,
                StateVersion = instance.StateVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _instancesById[instanceId] = jumped;
            _logger.LogInformation("Change-link: jumped instance {Id} to state '{State}'", instanceId, targetStateKey);
            return BuildEnvelope(jumped, definition);
        }

        var transition = definition.Transitions.FirstOrDefault(
            t => t.FromState == instance.CurrentState && t.Action == action && t.RequiresRole == null);

        if (transition == null)
            return ErrorEnvelope(
                $"Action '{action}' is not valid from state '{instance.CurrentState}'.", "INVALID_TRANSITION");
        if (fieldValues != null &&
            fieldValues.TryGetValue("enquiry-type", out var enquiryTypeObj) &&
            enquiryTypeObj?.ToString() == "Technical support" &&
            fieldValues.TryGetValue("message", out var messageObj))
        {
            var message = messageObj?.ToString() ?? string.Empty;
            
            // Check for version number (v1.2, v17, 1.0.0), URL (http/https), or error code (ERR-, 0x, #)
            var hasVersionNumber = Regex.IsMatch(message, @"\bv?\d+\.\d+", RegexOptions.IgnoreCase);
            var hasUrl = Regex.IsMatch(message, @"https?://\S+", RegexOptions.IgnoreCase);
            var hasErrorRef = Regex.IsMatch(message, @"\b(ERR[-_]\w+|0x[0-9A-Fa-f]+|#\d{3,})\b");
            
            if (!hasVersionNumber && !hasUrl && !hasErrorRef)
            {
                return new WorkflowResponseEnvelope
                {
                    InstanceId = instanceId,
                    StateVersion = instance.StateVersion,
                    ResponseState = "validation_error",
                    CorrelationId = instance.InstanceId,
                    ServerTimeUtc = DateTimeOffset.UtcNow,
                    Problems = new List<WorkflowProblem>
                    {
                        new WorkflowProblem
                        {
                            FieldKey = "message",
                            Code = "diagnostic-info-required",
                            Message = "Technical support requests should include a version number (e.g. v1.2.3), a URL, or an error reference so our team can help you faster."
                        }
                    }
                };
            }
        }

        var updated = instance with
        {
            CurrentState = transition.ToState,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            FieldValues = Merge(instance.FieldValues, fieldValues)
        };

        _instancesById[instanceId] = updated;
        _logger.LogInformation("Advanced instance {Id}: {From} → {To}", instanceId, instance.CurrentState, transition.ToState);

        return BuildEnvelope(updated, definition);
    }

    /// <summary>
    /// Advances a workflow instance using a reviewer-only action (emulator use only).
    /// Finds and applies a transition that requires the "reviewer" role.
    /// </summary>
    /// <param name="instanceId">The instance to advance.</param>
    /// <param name="action">The reviewer action to perform (e.g. "approve", "request-changes").</param>
    /// <returns>A WorkflowResponseEnvelope describing the next state, or an error if the action is invalid.</returns>
    /// <remarks>
    /// This is provided for the emulator dashboard to simulate reviewer actions outside the normal user workflow.
    /// It does NOT validate tenant/user access; it is intended only for developer/admin use.
    /// </remarks>
    public WorkflowResponseEnvelope AdvanceAsReviewer(string instanceId, string action)
    {
        if (!_instancesById.TryGetValue(instanceId, out var instance))
            return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");

        if (!_definitions.TryGetValue(instance.WorkflowKey, out var definition))
            return ErrorEnvelope($"Workflow '{instance.WorkflowKey}' not found.", "DEFINITION_NOT_FOUND");

        var transition = definition.Transitions.FirstOrDefault(
            t => t.FromState == instance.CurrentState && t.Action == action
                 && string.Equals(t.RequiresRole, "reviewer", StringComparison.OrdinalIgnoreCase));

        if (transition == null)
            return ErrorEnvelope(
                $"Reviewer action '{action}' is not valid from state '{instance.CurrentState}'.", "INVALID_TRANSITION");

        var updated = instance with
        {
            CurrentState = transition.ToState,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _instancesById[instanceId] = updated;
        _logger.LogInformation("Reviewer advanced instance {Id}: {From} → {To}", instanceId, instance.CurrentState, transition.ToState);

        return BuildEnvelope(updated, definition);
    }

    /// <summary>Returns a snapshot of all active instances (for the emulator dashboard).</summary>
    /// <returns>An enumerable of all active WorkflowInstanceState objects.</returns>
    public IEnumerable<WorkflowInstanceState> GetAllInstances() => _instancesById.Values;

    /// <summary>
    /// Returns all workflow instances for a specific user and tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <returns>A WorkflowInstanceListEnvelope containing summaries of all instances.</returns>
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
                    WorkflowPageUrl = null, // Controller will resolve this
                    InstancePolicy = definition?.InstancePolicy ?? "single"
                };
            })
            .ToList();

        return new WorkflowInstanceListEnvelope
        {
            Instances = userInstances
        };
    }

    /// <summary>Returns all loaded workflow definitions.</summary>
    public IEnumerable<WorkflowDefinitionFile> GetAllDefinitions() => _definitions.Values;

    /// <summary>Returns a specific workflow definition by key.</summary>
    /// <param name="key">The workflow definition key.</param>
    /// <returns>The workflow definition or null if not found.</returns>
    public WorkflowDefinitionFile? GetDefinition(string key) =>
        _definitions.TryGetValue(key, out var def) ? def : null;

    /// <summary>Updates a workflow definition in-memory.</summary>
    /// <param name="key">The workflow definition key to update.</param>
    /// <param name="updated">The new workflow definition.</param>
    /// <returns>True if the definition was found and updated; false if not found.</returns>
    public bool UpdateDefinition(string key, WorkflowDefinitionFile updated)
    {
        if (!_definitions.ContainsKey(key)) return false;
        _definitions[key] = updated;
        _logger.LogInformation("Workflow definition updated in-memory: {Key}", key);
        return true;
    }

    /// <summary>
    /// Removes an instance from in-memory state entirely (TUI reset command).
    /// The next call to <see cref="GetCurrent"/> for the same user/tenant/key will create a fresh instance.
    /// </summary>
    /// <param name="instanceId">The instance ID to remove.</param>
    /// <returns><c>true</c> if the instance was found and removed; <c>false</c> if it did not exist.</returns>
    public bool Reset(string instanceId)
    {
        if (!_instancesById.TryRemove(instanceId, out var instance))
            return false;

        var lookupKey = LookupKey(instance.TenantId, instance.UserId, instance.WorkflowKey);
        _instanceLookup.TryRemove(lookupKey, out _);
        _logger.LogInformation("Reset (deleted) instance {Id}", instanceId);
        return true;
    }

    /// <summary>
    /// Removes all in-memory workflow instances. Test-only — resets the engine to its initial state
    /// so each test starts with a fresh workflow.
    /// </summary>
    public void ResetAll()
    {
        _instancesById.Clear();
        _instanceLookup.Clear();
        _logger.LogInformation("ResetAll: all workflow instances cleared");
    }

    // -----------------------------------------------------------------------
    // Seed loading
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads workflow definitions and field groups from JSON seed files in the <c>workflow-seeds/</c> directory.
    /// Called at startup; missing files are logged but do not cause failure.
    /// </summary>
    /// <param name="contentRoot">The application's content root path.</param>
    private void LoadSeedData(string contentRoot)
    {
        var seedsDir = Path.Combine(contentRoot, "workflow-seeds");
        if (!Directory.Exists(seedsDir))
        {
            _logger.LogWarning("workflow-seeds directory not found at {Path}; no workflow definitions loaded.", seedsDir);
            return;
        }

        foreach (var file in Directory.GetFiles(seedsDir, "*.json"))
        {
            try
            {
                var def = JsonSerializer.Deserialize<WorkflowDefinitionFile>(File.ReadAllText(file), JsonOptions);
                if (def != null && !string.IsNullOrEmpty(def.DefinitionKey))
                {
                    _definitions[def.DefinitionKey] = def;
                    _logger.LogInformation("Loaded workflow definition '{Key}' from {File}", def.DefinitionKey, Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load workflow definition from {File}", file);
            }
        }

        _logger.LogInformation(
            "Workflow engine ready: {Defs} definition(s).",
            _definitions.Count);
    }

    // -----------------------------------------------------------------------
    // Response building
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a WorkflowResponseEnvelope from an instance and its definition.
    /// Resolves the current state, builds component render payloads, and determines the response state archetype.
    /// </summary>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="definition">The workflow definition.</param>
    /// <returns>A fully populated WorkflowResponseEnvelope ready to send to the client.</returns>
    private WorkflowResponseEnvelope BuildEnvelope(WorkflowInstanceState instance, WorkflowDefinitionFile definition)
    {
        var state = definition.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
        if (state == null)
            return ErrorEnvelope($"State '{instance.CurrentState}' not found in definition '{definition.DefinitionKey}'.", "STATE_NOT_FOUND");

        var actions = definition.Transitions
            .Where(t => t.FromState == instance.CurrentState && t.RequiresRole == null)
            .Select(t => new WorkflowAction
            {
                ActionKey = t.Action,
                Label = ActionLabel(t.Action),
                Style = ActionStyle(t.Action)
            }).ToArray();

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

    /// <summary>
    /// Builds a list of <see cref="PrismComponentRenderPayload"/> from the polymorphic v2.0 component tree.
    /// </summary>
    /// <param name="componentDefinitions">The components from the state.</param>
    /// <param name="savedValues">Previously collected field values to populate.</param>
    /// <returns>An array of component render payloads ready to send to the view.</returns>
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
                        _logger.LogWarning("Fieldset component contains no renderable fields");
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
                    var fields = BuildSummaryFields(summary.FieldRefs, componentDefinitions, savedValues);
                    if (fields.Length == 0)
                    {
                        _logger.LogWarning("Summary-list component contains no renderable fields");
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
                    var sections = accordion.Sections.Select(s => new PrismAccordionSectionPayload
                    {
                        Heading = s.Heading,
                        Summary = s.Summary,
                        Fields = BuildFields(s.Children, savedValues)
                    }).ToArray();

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
                        Content = waiting.Content,
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
                    result.Add(new PrismComponentRenderPayload { Type = "body", Content = body.Content });
                    break;

                case HeadingComponent heading:
                    result.Add(new PrismComponentRenderPayload { Type = "heading", Content = heading.Content, Level = heading.Level });
                    break;

                case InsetTextComponent inset:
                    result.Add(new PrismComponentRenderPayload { Type = "inset-text", Content = inset.Content });
                    break;

                case WarningTextComponent warning:
                    result.Add(new PrismComponentRenderPayload { Type = "warning-text", Content = warning.Content });
                    break;

                case DetailsComponent details:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "details",
                        Heading = details.Heading,
                        Content = details.Content
                    });
                    break;

                case NotificationBannerComponent banner:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "notification-banner",
                        Heading = banner.Heading,
                        Content = banner.Content,
                        BannerType = banner.BannerType
                    });
                    break;

                case TaskListComponent taskList:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "task-list",
                        TaskSections = taskList.Sections?.Select(s => new PrismTaskSection
                        {
                            Heading = s.Heading,
                            Tasks = s.Tasks.Select(t => new PrismTaskItem
                            {
                                Label = t.Label,
                                Href = t.Href ?? t.StateKey,
                                Status = "not-started"
                            }).ToArray()
                        }).ToArray()
                    });
                    break;

                case InputComponent input:
                {
                    // Bare input at state level (not wrapped in a fieldset). Wrap as a single-field fieldset.
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

    /// <summary>
    /// Builds <see cref="FieldRenderPayload"/>s from the children of a container, walking
    /// any conditional children of radios/checkboxes and flattening them with
    /// ConditionalOn/VisibleWhen so the UI can show/hide them.
    /// </summary>
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

                    // Flatten conditional children (radios/checkboxes) into ConditionalOn/VisibleWhen field payloads.
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

    /// <summary>
    /// Builds summary-list payloads by resolving the supplied field-keys against every
    /// input component reachable from the current state's component tree.
    /// </summary>
    private static FieldRenderPayload[] BuildSummaryFields(
        IReadOnlyList<string> fieldRefs,
        IReadOnlyList<PrismComponent> stateComponents,
        Dictionary<string, object?> savedValues)
    {
        if (fieldRefs.Count == 0) return Array.Empty<FieldRenderPayload>();

        var lookup = stateComponents.GetAllInputs()
            .GroupBy(i => i.FieldKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var payloads = new List<FieldRenderPayload>();
        foreach (var key in fieldRefs)
        {
            if (lookup.TryGetValue(key, out var input))
            {
                payloads.Add(BuildInputPayload(input, savedValues));
            }
            else
            {
                // Not found in this state's tree: emit a minimal payload so the summary still renders.
                payloads.Add(new FieldRenderPayload
                {
                    FieldKey = key,
                    Label = key,
                    FieldType = "text",
                    Required = false,
                    Value = savedValues.TryGetValue(key, out var v) ? v : null
                });
            }
        }
        return payloads.ToArray();
    }

    /// <summary>Maps a polymorphic <see cref="InputComponent"/> to a <see cref="FieldRenderPayload"/>.</summary>
    private static FieldRenderPayload BuildInputPayload(InputComponent input, Dictionary<string, object?> savedValues)
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
                SelectComponent s => s.Options,
                RadiosComponent r => r.Options,
                CheckboxesComponent c => c.Options,
                _ => null
            },
            Value = GetDisplayValue(input, fieldType, savedValues),
            MinLength = input switch
            {
                TextInputComponent t => t.MinLength,
                TextareaComponent t => t.MinLength,
                _ => null
            },
            MaxLength = input switch
            {
                TextInputComponent t => t.MaxLength,
                TextareaComponent t => t.MaxLength,
                _ => null
            },
            Pattern = input switch
            {
                TextInputComponent t => t.Pattern,
                EmailComponent e => e.Pattern,
                TelComponent t => t.Pattern,
                _ => null
            },
            Min = input switch
            {
                NumberInputComponent n => n.Min,
                DecimalInputComponent d => d.Min,
                _ => null
            },
            Max = input switch
            {
                NumberInputComponent n => n.Max,
                DecimalInputComponent d => d.Max,
                _ => null
            },
            Prefix = input switch
            {
                TextInputComponent t => t.Prefix,
                NumberInputComponent n => n.Prefix,
                DecimalInputComponent d => d.Prefix,
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

    private static object? GetDisplayValue(InputComponent input, string fieldType, Dictionary<string, object?> savedValues)
    {
        var raw = savedValues.TryGetValue(input.FieldKey, out var v) ? v : null;
        if (raw == null) return null;

        var prefix = input switch
        {
            TextInputComponent t => t.Prefix,
            NumberInputComponent n => n.Prefix,
            DecimalInputComponent d => d.Prefix,
            _ => null
        };

        if (fieldType.Equals("currency", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(prefix))
            return $"{prefix}{raw}";

        return raw;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a composite lookup key for finding active instances by user/tenant/workflow.
    /// Format: "{tenantId}:{userId}:{workflowKey}".
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="workflowKey">The workflow definition key.</param>
    /// <returns>A composite lookup key string.</returns>
    private static string LookupKey(string tenantId, string userId, string workflowKey)
        => $"{tenantId}:{userId}:{workflowKey}";

    /// <summary>
    /// Merges incoming field values into the existing saved values, overwriting duplicates.
    /// </summary>
    /// <param name="existing">Previously saved field values.</param>
    /// <param name="incoming">New or updated field values from the current submission.</param>
    /// <returns>A merged dictionary.</returns>
    private static Dictionary<string, object?> Merge(Dictionary<string, object?> existing, Dictionary<string, object?>? incoming)
    {
        if (incoming == null || incoming.Count == 0) return existing;
        var merged = new Dictionary<string, object?>(existing);
        foreach (var kv in incoming) merged[kv.Key] = kv.Value;
        return merged;
    }

    /// <summary>Creates a standard error envelope with a message and code.</summary>
    /// <param name="message">The error message to display to the user.</param>
    /// <param name="code">The error code for programmatic handling (e.g. "INSTANCE_NOT_FOUND").</param>
    /// <returns>A WorkflowResponseEnvelope in error state.</returns>
    private static WorkflowResponseEnvelope ErrorEnvelope(string message, string code) =>
        new()
        {
            InstanceId = string.Empty,
            ResponseState = "error",
            StateVersion = 0,
            CorrelationId = Guid.NewGuid().ToString(),
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Problems = [new WorkflowProblem { FieldKey = string.Empty, Message = message, Code = code }]
        };

    /// <summary>Translates an action key into a user-friendly label.</summary>
    /// <param name="key">The action key (e.g. "submit", "save-draft").</param>
    /// <returns>A display label; returns the key unchanged if no mapping exists.</returns>
    private static string ActionLabel(string key) => key switch
    {
        "submit" => "Submit",
        "save-draft" => "Save Draft",
        "start-another" => "Start Another",
        "approve" => "Approve",
        "request-changes" => "Request Changes",
        _ => key
    };

    /// <summary>Determines the button style (CSS class) for a given action.</summary>
    /// <param name="key">The action key.</param>
    /// <returns>A style identifier ("primary", "destructive", or "secondary").</returns>
    private static string ActionStyle(string key) => key switch
    {
        "submit" or "approve" => "primary",
        "reject" or "cancel" => "destructive",
        _ => "secondary"
    };
}
