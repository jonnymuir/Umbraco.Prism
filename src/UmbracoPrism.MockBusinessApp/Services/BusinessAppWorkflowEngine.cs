using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Models;
using UmbracoPrism.WorkflowRuntime.Services;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.MockBusinessApp.Services;

public class BusinessAppWorkflowEngine : WorkflowRuntimeEngine
{
    public WorkflowResponseEnvelope AdvanceAsReviewer(string instanceId, string action)
    {
        if (!TryGetInstance(instanceId, out var instance))
        {
            return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");
        }

        var definition = GetDefinition(instance.WorkflowKey);
        if (definition == null)
        {
            return ErrorEnvelope($"Workflow '{instance.WorkflowKey}' not found.", "DEFINITION_NOT_FOUND");
        }

        var transition = definition.Transitions.FirstOrDefault(
            t => t.FromState == instance.CurrentState && t.Action == action
                 && string.Equals(t.RequiresRole, "reviewer", StringComparison.OrdinalIgnoreCase));

        if (transition == null)
        {
            return ErrorEnvelope(
                $"Reviewer action '{action}' is not valid from state '{instance.CurrentState}'.",
                "INVALID_TRANSITION");
        }

        var updated = instance with
        {
            CurrentState = transition.ToState,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        SaveInstance(updated);
        Logger.LogInformation(
            "Reviewer advanced instance {Id}: {From} → {To}",
            instanceId,
            instance.CurrentState,
            transition.ToState);

        return BuildEnvelope(updated, definition);
    }

    public BusinessAppWorkflowEngine(
        ILogger<BusinessAppWorkflowEngine> logger,
        IWebHostEnvironment env,
        IWorkflowContentSanitizer sanitizer,
        IWorkflowDefinitionStore? definitionStore = null)
        : base(
            logger,
            definitionStore ?? new FilesystemWorkflowDefinitionStore(Path.Combine(env.ContentRootPath, "workflow-seeds")),
            sanitizer)
    {
    }

    protected override WorkflowResponseEnvelope? ValidateAdvance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        Dictionary<string, object?>? fieldValues)
    {
        if (fieldValues == null
            || !fieldValues.TryGetValue("enquiry-type", out var enquiryTypeObj)
            || enquiryTypeObj?.ToString() != "Technical support"
            || !fieldValues.TryGetValue("message", out var messageObj))
        {
            return null;
        }

        var message = messageObj?.ToString() ?? string.Empty;
        var hasVersionNumber = Regex.IsMatch(message, @"\bv?\d+\.\d+", RegexOptions.IgnoreCase);
        var hasUrl = Regex.IsMatch(message, @"https?://\S+", RegexOptions.IgnoreCase);
        var hasErrorRef = Regex.IsMatch(message, @"\b(ERR[-_]\w+|0x[0-9A-Fa-f]+|#\d{3,})\b");

        if (hasVersionNumber || hasUrl || hasErrorRef)
        {
            return null;
        }
        return new WorkflowResponseEnvelope
        {
            InstanceId = instance.InstanceId,
            StateVersion = instance.StateVersion,
            ResponseState = "validation_error",
            CorrelationId = instance.InstanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Problems =
            [
                new WorkflowProblem
                {
                    FieldKey = "message",
                    Code = "diagnostic-info-required",
                    Message = "Technical support requests should include a version number (e.g. v1.2.3), a URL, or an error reference so our team can help you faster."
                }
            ]
        };
    }
}
