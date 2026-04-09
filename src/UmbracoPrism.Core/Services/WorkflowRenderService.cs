using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for rendering workflow states into UI-ready payloads.
/// </summary>
public class WorkflowRenderService(
    ILogger<WorkflowRenderService> logger,
    IContentTypeService contentTypeService) : IWorkflowRenderService
{
    private readonly ILogger<WorkflowRenderService> _logger = logger;
    private readonly IContentTypeService _contentTypeService = contentTypeService;

    /// <inheritdoc/>
    public Task<WorkflowRenderPayload> RenderAsync(WorkflowInstance instance, WorkflowDefinition definition)
    {
        var currentState = definition.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
        if (currentState == null)
        {
            _logger.LogWarning("Current state {StateKey} not found in definition {DefinitionKey}",
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

        var fieldGroups = BuildFieldGroups(currentState);

        var payload = new WorkflowRenderPayload
        {
            Archetype = currentState.Archetype,
            StateDisplayName = currentState.DisplayName,
            FieldGroups = fieldGroups,
            AvailableActions = actions
        };

        return Task.FromResult(payload);
    }

    private IReadOnlyList<FieldGroupRenderPayload> BuildFieldGroups(WorkflowState state)
    {
        if (string.IsNullOrWhiteSpace(state.ElementTypeAlias))
        {
            return Array.Empty<FieldGroupRenderPayload>();
        }

        var contentType = _contentTypeService.Get(state.ElementTypeAlias);
        if (contentType == null)
        {
            _logger.LogWarning("Element type {ElementTypeAlias} not found for state {StateKey}",
                state.ElementTypeAlias, state.StateKey);
            return Array.Empty<FieldGroupRenderPayload>();
        }

        var fields = contentType.PropertyTypes.Select(pt => new FieldRenderPayload
        {
            FieldKey = pt.Alias,
            Label = pt.Name,
            Hint = pt.Description,
            FieldType = PrismPropertyTypeMapper.ToFieldType(pt.PropertyEditorAlias),
            Required = pt.Mandatory,
            Value = null,
            Options = GetOptionsForPropertyType(pt)
        }).ToList();

        return new[]
        {
            new FieldGroupRenderPayload
            {
                GroupKey = state.StateKey,
                DisplayName = state.DisplayName,
                Fields = fields
            }
        };
    }

    private IReadOnlyList<string>? GetOptionsForPropertyType(Umbraco.Cms.Core.Models.IPropertyType propertyType)
    {
        var editorAlias = propertyType.PropertyEditorAlias;
        
        if (editorAlias != "Umbraco.DropDown.Flexible" &&
            editorAlias != "Umbraco.CheckBoxList" &&
            editorAlias != "Umbraco.RadioButtonList")
        {
            return null;
        }

        return Array.Empty<string>();
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
