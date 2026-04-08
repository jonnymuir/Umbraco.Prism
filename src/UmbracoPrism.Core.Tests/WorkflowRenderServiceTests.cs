using FluentAssertions;
using Moq;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class WorkflowRenderServiceTests
{
    private const string InstanceId = "wf-instance-123";

    private static WorkflowInstance MakeInstance(string state, int stateVersion = 1) => new()
    {
        InstanceId = InstanceId,
        TenantId = "tenant-a",
        CurrentState = state,
        StateVersion = stateVersion,
        Status = "Active"
    };

    private static WorkflowDefinition MakeDefinition() => new()
    {
        Id = 1,
        DefinitionKey = "info-request",
        Version = "1",
        DisplayName = "Information Request",
        InitialState = "draft",
        States = new List<WorkflowState>
        {
            new() { StateKey = "draft", DisplayName = "Draft", Archetype = "Collect", AllowedActions = new() { "submit", "save-draft" } },
            new() { StateKey = "under-review", DisplayName = "Under Review", Archetype = "TaskQueue", AllowedActions = new() },
            new() { StateKey = "complete", DisplayName = "Request Complete", Archetype = "Completion", AllowedActions = new() { "start-another" } }
        },
        Transitions = new List<WorkflowTransition>
        {
            new() { FromState = "draft", ToState = "under-review", Action = "submit" },
            new() { FromState = "draft", ToState = "draft", Action = "save-draft" },
            new() { FromState = "under-review", ToState = "complete", Action = "approve", RequiresRole = "reviewer" }
        }
    };

    [Fact]
    public async Task Render_ReturnsCorrectArchetype_ForCurrentState()
    {
        var instance = MakeInstance("draft");
        var definition = MakeDefinition();
        var renderService = new Mock<IWorkflowRenderService>();
        renderService.Setup(s => s.RenderAsync(instance, definition))
            .ReturnsAsync(new WorkflowRenderPayload
            {
                Archetype = "Collect",
                StateDisplayName = "Draft",
                FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                AvailableActions = Array.Empty<WorkflowAction>()
            });

        var result = await renderService.Object.RenderAsync(instance, definition);

        result.Archetype.Should().Be("Collect");
    }

    [Fact]
    public async Task Render_ReturnsStateDisplayName()
    {
        var instance = MakeInstance("under-review", stateVersion: 2);
        var definition = MakeDefinition();
        var renderService = new Mock<IWorkflowRenderService>();
        renderService.Setup(s => s.RenderAsync(instance, definition))
            .ReturnsAsync(new WorkflowRenderPayload
            {
                Archetype = "TaskQueue",
                StateDisplayName = "Under Review",
                FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                AvailableActions = Array.Empty<WorkflowAction>()
            });

        var result = await renderService.Object.RenderAsync(instance, definition);

        result.StateDisplayName.Should().Be("Under Review");
    }

    [Fact]
    public async Task Render_IncludesFieldGroups_WhenArchetypeIsCollect()
    {
        var instance = MakeInstance("draft");
        var definition = MakeDefinition();
        var renderService = new Mock<IWorkflowRenderService>();
        renderService.Setup(s => s.RenderAsync(instance, definition))
            .ReturnsAsync(new WorkflowRenderPayload
            {
                Archetype = "Collect",
                StateDisplayName = "Draft",
                FieldGroups = new List<FieldGroupRenderPayload>
                {
                    new()
                    {
                        GroupKey = "contact-info",
                        DisplayName = "Contact Information",
                        Fields = new List<FieldRenderPayload>
                        {
                            new() { FieldKey = "email", Label = "Email", FieldType = "text", Required = true }
                        }
                    }
                },
                AvailableActions = Array.Empty<WorkflowAction>()
            });

        var result = await renderService.Object.RenderAsync(instance, definition);

        result.FieldGroups.Should().HaveCount(1);
        result.FieldGroups[0].GroupKey.Should().Be("contact-info");
        result.FieldGroups[0].DisplayName.Should().Be("Contact Information");
    }

    [Fact]
    public async Task Render_ReturnsAvailableActions_ForCurrentState()
    {
        var instance = MakeInstance("draft");
        var definition = MakeDefinition();
        var renderService = new Mock<IWorkflowRenderService>();
        renderService.Setup(s => s.RenderAsync(instance, definition))
            .ReturnsAsync(new WorkflowRenderPayload
            {
                Archetype = "Collect",
                StateDisplayName = "Draft",
                FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                AvailableActions = new List<WorkflowAction>
                {
                    new() { ActionKey = "submit", Label = "Submit for Review", Style = "primary" },
                    new() { ActionKey = "save-draft", Label = "Save Draft", Style = "secondary" }
                }
            });

        var result = await renderService.Object.RenderAsync(instance, definition);

        result.AvailableActions.Should().HaveCount(2);
        result.AvailableActions[0].ActionKey.Should().Be("submit");
        result.AvailableActions[1].ActionKey.Should().Be("save-draft");
    }

    [Fact]
    public async Task Render_DoesNotIncludeActions_ForOtherStates()
    {
        var instance = MakeInstance("draft");
        var definition = MakeDefinition();
        var renderService = new Mock<IWorkflowRenderService>();
        renderService.Setup(s => s.RenderAsync(instance, definition))
            .ReturnsAsync(new WorkflowRenderPayload
            {
                Archetype = "Collect",
                StateDisplayName = "Draft",
                FieldGroups = Array.Empty<FieldGroupRenderPayload>(),
                AvailableActions = new List<WorkflowAction>
                {
                    new() { ActionKey = "submit", Label = "Submit for Review", Style = "primary" }
                }
            });

        var result = await renderService.Object.RenderAsync(instance, definition);

        result.AvailableActions.Should().NotContain(a => a.ActionKey == "approve");
        result.AvailableActions.Should().NotContain(a => a.ActionKey == "reject");
    }
}
