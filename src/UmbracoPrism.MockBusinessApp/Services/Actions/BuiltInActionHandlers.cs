using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.MockBusinessApp.Services.Actions;

internal static class WorkflowActionParameterReader
{
    public static string GetRequiredString(ActionDefinition action, string key)
    {
        var value = action.Parameters[key]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Workflow action '{action.Type}' requires parameter '{key}'.")
            : value;
    }

    public static string? GetOptionalString(ActionDefinition action, string key) =>
        action.Parameters[key]?.GetValue<string>();

    public static bool GetBoolean(ActionDefinition action, string key, bool defaultValue = false) =>
        action.Parameters[key]?.GetValue<bool>() ?? defaultValue;
}

public abstract class WorkflowActionHandlerBase(string actionType) : IWorkflowActionHandler
{
    public string ActionType { get; } = actionType;

    public async Task<WorkflowActionExecutionResult> ExecuteAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteCoreAsync(action, context, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return WorkflowActionExecutionResult.Failure("ACTION_PARAMETERS_INVALID", ex.Message);
        }
    }

    protected abstract Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken);
}

public sealed class FormsLoadWorkflowActionHandler : WorkflowActionHandlerBase
{
    public FormsLoadWorkflowActionHandler() : base("forms.load")
    {
    }

    protected override Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var formDefinitionId = WorkflowActionParameterReader.GetRequiredString(action, "formDefinitionId");
        return Task.FromResult(WorkflowActionExecutionResult.Success(
            $"Loaded forms definition '{formDefinitionId}' for state '{context.TargetState.StageKey}'.",
            new Dictionary<string, object?>
            {
                ["operation"] = "load",
                ["formDefinitionId"] = formDefinitionId
            }));
    }
}

public sealed class FormsSaveWorkflowActionHandler : WorkflowActionHandlerBase
{
    public FormsSaveWorkflowActionHandler() : base("forms.save")
    {
    }

    protected override Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var formDefinitionId = WorkflowActionParameterReader.GetRequiredString(action, "formDefinitionId");
        return Task.FromResult(WorkflowActionExecutionResult.Success(
            $"Saved forms payload for '{formDefinitionId}'.",
            new Dictionary<string, object?>
            {
                ["operation"] = "save",
                ["formDefinitionId"] = formDefinitionId,
                ["fieldCount"] = context.FieldValues.Count
            }));
    }
}

public sealed class FormsSubmitWorkflowActionHandler : WorkflowActionHandlerBase
{
    public FormsSubmitWorkflowActionHandler() : base("forms.submit")
    {
    }

    protected override Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var formDefinitionId = WorkflowActionParameterReader.GetRequiredString(action, "formDefinitionId");
        return Task.FromResult(WorkflowActionExecutionResult.Success(
            $"Submitted forms definition '{formDefinitionId}'.",
            new Dictionary<string, object?>
            {
                ["operation"] = "submit",
                ["formDefinitionId"] = formDefinitionId,
                ["fieldCount"] = context.FieldValues.Count
            }));
    }
}

public sealed class CaseAssignWorkflowActionHandler : WorkflowActionHandlerBase
{
    public CaseAssignWorkflowActionHandler() : base("case.assign")
    {
    }

    protected override Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var assigneeType = WorkflowActionParameterReader.GetRequiredString(action, "assigneeType");
        var assigneeValue = WorkflowActionParameterReader.GetRequiredString(action, "assigneeValue");
        var overwriteExisting = WorkflowActionParameterReader.GetBoolean(action, "overwriteExisting");

        return Task.FromResult(WorkflowActionExecutionResult.Success(
            $"Assigned case to {assigneeType} '{assigneeValue}'.",
            new Dictionary<string, object?>
            {
                ["assigneeType"] = assigneeType,
                ["assigneeValue"] = assigneeValue,
                ["overwriteExisting"] = overwriteExisting
            }));
    }
}

public sealed class CaseEnqueueWorkflowActionHandler : WorkflowActionHandlerBase
{
    public CaseEnqueueWorkflowActionHandler() : base("case.enqueue")
    {
    }

    protected override Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var queue = WorkflowActionParameterReader.GetRequiredString(action, "queue");
        var priority = WorkflowActionParameterReader.GetOptionalString(action, "priority") ?? "normal";

        return Task.FromResult(WorkflowActionExecutionResult.Success(
            $"Enqueued case into '{queue}' with priority '{priority}'.",
            new Dictionary<string, object?>
            {
                ["queue"] = queue,
                ["priority"] = priority
            }));
    }
}

public sealed class CaseSetStatusWorkflowActionHandler : WorkflowActionHandlerBase
{
    public CaseSetStatusWorkflowActionHandler() : base("case.set-status")
    {
    }

    protected override Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var status = WorkflowActionParameterReader.GetRequiredString(action, "status");
        var reason = WorkflowActionParameterReader.GetOptionalString(action, "reason");

        return Task.FromResult(WorkflowActionExecutionResult.Success(
            $"Set case status to '{status}'.",
            new Dictionary<string, object?>
            {
                ["status"] = status,
                ["reason"] = reason ?? string.Empty
            }));
    }
}

public sealed class CaseAddNoteWorkflowActionHandler : WorkflowActionHandlerBase
{
    public CaseAddNoteWorkflowActionHandler() : base("case.add-note")
    {
    }

    protected override Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var note = WorkflowActionParameterReader.GetRequiredString(action, "note");
        var visibility = WorkflowActionParameterReader.GetOptionalString(action, "visibility") ?? "internal";

        return Task.FromResult(WorkflowActionExecutionResult.Success(
            $"Added {visibility} case note.",
            new Dictionary<string, object?>
            {
                ["note"] = note,
                ["visibility"] = visibility
            }));
    }
}

public sealed class NotificationsSendEmailWorkflowActionHandler : WorkflowActionHandlerBase
{
    public NotificationsSendEmailWorkflowActionHandler() : base("notifications.send-email")
    {
    }

    protected override Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var templateId = WorkflowActionParameterReader.GetRequiredString(action, "templateId");
        var recipientEmail = WorkflowActionParameterReader.GetRequiredString(action, "recipientEmail");
        var subject = WorkflowActionParameterReader.GetOptionalString(action, "subject");

        return Task.FromResult(WorkflowActionExecutionResult.Success(
            $"Queued email '{templateId}' to '{recipientEmail}'.",
            new Dictionary<string, object?>
            {
                ["templateId"] = templateId,
                ["recipientEmail"] = recipientEmail,
                ["subject"] = subject ?? string.Empty
            }));
    }
}

public sealed class NotificationsSendSmsWorkflowActionHandler : WorkflowActionHandlerBase
{
    public NotificationsSendSmsWorkflowActionHandler() : base("notifications.send-sms")
    {
    }

    protected override Task<WorkflowActionExecutionResult> ExecuteCoreAsync(
        ActionDefinition action,
        WorkflowActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var templateId = WorkflowActionParameterReader.GetRequiredString(action, "templateId");
        var recipientNumber = WorkflowActionParameterReader.GetRequiredString(action, "recipientNumber");

        return Task.FromResult(WorkflowActionExecutionResult.Success(
            $"Queued SMS '{templateId}' to '{recipientNumber}'.",
            new Dictionary<string, object?>
            {
                ["templateId"] = templateId,
                ["recipientNumber"] = recipientNumber
            }));
    }
}
