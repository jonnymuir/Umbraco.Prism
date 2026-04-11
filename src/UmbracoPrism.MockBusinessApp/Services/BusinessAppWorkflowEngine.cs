using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;

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
    private readonly Dictionary<string, FieldGroupFile> _fieldGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WorkflowInstanceState> _instancesById = new();

    /// <summary>
    /// Secondary index: "{tenantId}:{userId}:{workflowKey}" → instanceId.
    /// Enables resuming the active instance when a user returns to a workflow.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _instanceLookup = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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
    /// <returns>A WorkflowResponseEnvelope describing the current state and what to render.</returns>
    /// <remarks>
    /// This method creates a new instance on first call and reuses it on subsequent calls for the same 
    /// user/tenant/workflow combination. Instances are stored in-memory and survive until application restart.
    /// </remarks>
    public WorkflowResponseEnvelope GetCurrent(string workflowKey, string tenantId, string userId)
    {
        if (!_definitions.TryGetValue(workflowKey, out var definition))
        {
            _logger.LogWarning("Workflow definition not found: {Key}", workflowKey);
            return ErrorEnvelope($"Workflow '{workflowKey}' is not registered with this application.", "DEFINITION_NOT_FOUND");
        }

        var lookupKey = LookupKey(tenantId, userId, workflowKey);

        if (!_instanceLookup.TryGetValue(lookupKey, out var instanceId)
            || !_instancesById.TryGetValue(instanceId, out var instance))
        {
            instanceId = Guid.NewGuid().ToString();
            instance = new WorkflowInstanceState
            {
                InstanceId = instanceId,
                WorkflowKey = workflowKey,
                TenantId = tenantId,
                UserId = userId,
                CurrentState = definition.InitialState,
                StateVersion = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _instancesById[instanceId] = instance;
            _instanceLookup[lookupKey] = instanceId;
            _logger.LogInformation("Created workflow instance {Id} for key={Key} tenant={Tenant}", instanceId, workflowKey, tenantId);
        }

        return BuildEnvelope(instance, definition);
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

        var transition = definition.Transitions.FirstOrDefault(
            t => t.FromState == instance.CurrentState && t.Action == action && t.RequiresRole == null);

        if (transition == null)
            return ErrorEnvelope(
                $"Action '{action}' is not valid from state '{instance.CurrentState}'.", "INVALID_TRANSITION");

        // BA cross-field validation: Technical Support requires diagnostic info
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

                return new WorkflowInstanceSummary
                {
                    InstanceId = instance.InstanceId,
                    WorkflowKey = instance.WorkflowKey,
                    WorkflowDisplayName = definition?.DisplayName ?? instance.WorkflowKey,
                    CurrentStateKey = instance.CurrentState,
                    CurrentStateDisplayName = state?.DisplayName ?? instance.CurrentState,
                    Archetype = state?.Archetype ?? "Collect",
                    CreatedAt = instance.CreatedAt.DateTime,
                    LastUpdatedAt = instance.UpdatedAt.DateTime,
                    CanContinue = state?.Archetype != "Completion",
                    IsCompleted = state?.Archetype == "Completion",
                    WorkflowPageUrl = null // Controller will resolve this
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

        var fieldGroupsDir = Path.Combine(seedsDir, "field-groups");
        if (Directory.Exists(fieldGroupsDir))
        {
            foreach (var file in Directory.GetFiles(fieldGroupsDir, "*.json"))
            {
                try
                {
                    var fg = JsonSerializer.Deserialize<FieldGroupFile>(File.ReadAllText(file), JsonOptions);
                    if (fg != null)
                    {
                        _fieldGroups[fg.GroupKey] = fg;
                        _logger.LogDebug("Loaded field group '{Key}' from {File}", fg.GroupKey, Path.GetFileName(file));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load field group from {File}", file);
                }
            }
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
            "Workflow engine ready: {Defs} definition(s), {Groups} field group(s).",
            _definitions.Count, _fieldGroups.Count);
    }

    // -----------------------------------------------------------------------
    // Response building
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a WorkflowResponseEnvelope from an instance and its definition.
    /// Resolves the current state, loads field groups, and determines the response state archetype.
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

        var fieldGroups = state.FieldGroupKeys
            .Select(key => _fieldGroups.TryGetValue(key, out var fg) ? BuildFieldGroup(fg, instance.FieldValues) : null)
            .Where(fg => fg != null)
            .Cast<FieldGroupRenderPayload>()
            .ToArray();

        var render = new WorkflowRenderPayload
        {
            Archetype = state.Archetype,
            StateDisplayName = state.DisplayName,
            FieldGroups = fieldGroups,
            AvailableActions = actions
        };

        var responseState = state.Archetype switch
        {
            "StatusTimeline" => "wait",
            "Completion" => "complete",
            _ => "ask_now"
        };

        return new WorkflowResponseEnvelope
        {
            InstanceId = instance.InstanceId,
            ResponseState = responseState,
            StateVersion = instance.StateVersion,
            CorrelationId = instance.InstanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Render = render,
            InstancePolicy = definition.InstancePolicy
        };
    }

    /// <summary>
    /// Builds a FieldGroupRenderPayload from a field group definition, pre-populating field values.
    /// </summary>
    /// <param name="group">The field group definition.</param>
    /// <param name="savedValues">Previously collected field values to populate.</param>
    /// <returns>A FieldGroupRenderPayload ready to render in the UI.</returns>
    private static FieldGroupRenderPayload BuildFieldGroup(FieldGroupFile group, Dictionary<string, object?> savedValues)
    {
        var fields = group.Fields.Select(f => new FieldRenderPayload
        {
            FieldKey = f.FieldKey,
            Label = f.Label,
            Hint = f.Hint,
            FieldType = f.FieldType,
            Required = f.Required,
            Options = f.Options,
            Value = savedValues.TryGetValue(f.FieldKey, out var v) ? v : null,
            MinLength = f.MinLength,
            MaxLength = f.MaxLength,
            Pattern = f.Pattern,
            Min = f.Min,
            Max = f.Max,
            ConditionalOn = f.ConditionalOn,
            VisibleWhen = f.VisibleWhen
        }).ToArray();

        return new FieldGroupRenderPayload
        {
            GroupKey = group.GroupKey,
            DisplayName = group.DisplayName,
            Fields = fields
        };
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
