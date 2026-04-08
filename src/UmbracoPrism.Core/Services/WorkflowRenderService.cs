using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for rendering workflow states into UI-ready payloads.
/// </summary>
public class WorkflowRenderService(ILogger<WorkflowRenderService> logger) : IWorkflowRenderService
{
    /// <inheritdoc/>
    public Task<WorkflowRenderPayload> RenderAsync(WorkflowInstance instance, WorkflowDefinition definition)
    {
        var currentState = definition.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
        if (currentState == null)
        {
            logger.LogWarning("Current state {StateKey} not found in definition {DefinitionKey}",
                instance.CurrentState, definition.DefinitionKey);

            return Task.FromResult(new WorkflowRenderPayload
            {
                Archetype = "Error",
                StateDisplayName = "Unknown State",
                FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                AvailableActions = Array.Empty<WorkflowAction>()
            });
        }

        var availableTransitions = definition.Transitions
            .Where(t => t.FromState == instance.CurrentState)
            .ToList();

        var actions = availableTransitions.Select(t => new WorkflowAction
        {
            ActionKey = t.Action,
            Label = GetActionLabel(t.Action),
            Style = GetActionStyle(t.Action)
        }).ToList();

        var payload = new WorkflowRenderPayload
        {
            Archetype = currentState.Archetype,
            StateDisplayName = currentState.DisplayName,
            FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
            AvailableActions = actions
        };

        return Task.FromResult(payload);
    }

    private string GetActionLabel(string actionKey)
    {
        return actionKey switch
        {
            "submit" => "Submit",
            "approve" => "Approve",
            "reject" => "Reject",
            "cancel" => "Cancel",
            "request-changes" => "Request Changes",
            _ => actionKey
        };
    }

    private string GetActionStyle(string actionKey)
    {
        return actionKey switch
        {
            "submit" or "approve" => "primary",
            "reject" or "cancel" => "destructive",
            _ => "secondary"
        };
    }
}
