using FluentAssertions;
using UmbracoPrism.Shared.Builders;

namespace UmbracoPrism.Core.Tests.Builders;

public class WorkflowDefinitionBuilderTests
{
    [Fact]
    public void Build_WithAllProperties_ReturnsCompleteDefinition()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("pension-application")
            .DisplayName("Pension Application")
            .Version(2)
            .StartsAt("collect-details")
            .InstancePolicy("multiple")
            .AddState("collect-details", s => s
                .DisplayName("Your Details")
                .StepType("question")
                .WithFieldGroups("personal-info"))
            .AddTransition("collect-details", "submitted", "submit", "admin")
            .Build();

        result.DefinitionKey.Should().Be("pension-application");
        result.DisplayName.Should().Be("Pension Application");
        result.Version.Should().Be(2);
        result.InitialState.Should().Be("collect-details");
        result.InstancePolicy.Should().Be("multiple");
    }

    [Fact]
    public void Build_WithoutVersionCall_DefaultsToOne()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test-workflow")
            .Build();

        result.Version.Should().Be(1);
    }

    [Fact]
    public void Build_WithoutInstancePolicyCall_DefaultsToSingle()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test-workflow")
            .Build();

        result.InstancePolicy.Should().Be("single");
    }

    [Fact]
    public void AddState_WithQuestionStepType_SetsStepTypeCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("step1", s => s
                .DisplayName("Step One")
                .StepType("question"))
            .Build();

        var state = result.States.Single();
        state.StepType.Should().Be("question");
    }

    [Fact]
    public void AddState_WithCheckAnswersStepType_SetsStepTypeCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("check", s => s
                .DisplayName("Check Answers")
                .StepType("check-answers"))
            .Build();

        var state = result.States.Single();
        state.StepType.Should().Be("check-answers");
    }

    [Fact]
    public void AddState_WithConfirmationStepType_SetsStepTypeCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("done", s => s
                .DisplayName("Completed")
                .StepType("confirmation"))
            .Build();

        var state = result.States.Single();
        state.StepType.Should().Be("confirmation");
    }

    [Fact]
    public void AddState_WithStatusTimelineStepType_SetsStepTypeCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("status", s => s
                .DisplayName("Status")
                .StepType("status-timeline"))
            .Build();

        var state = result.States.Single();
        state.StepType.Should().Be("status-timeline");
    }

    [Fact]
    public void AddState_WithTaskListStepType_SetsStepTypeCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("tasks", s => s
                .DisplayName("Tasks")
                .StepType("task-list"))
            .Build();

        var state = result.States.Single();
        state.StepType.Should().Be("task-list");
    }

    [Fact]
    public void AddState_WithMultipleStates_AllStatesPresent()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("multi-step")
            .AddState("step1", s => s.DisplayName("First"))
            .AddState("step2", s => s.DisplayName("Second"))
            .AddState("step3", s => s.DisplayName("Third"))
            .Build();

        result.States.Should().HaveCount(3);
        result.States[0].StateKey.Should().Be("step1");
        result.States[1].StateKey.Should().Be("step2");
        result.States[2].StateKey.Should().Be("step3");
    }

    [Fact]
    public void AddState_WithFieldGroupKeys_SetsFieldGroupKeysCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("collect", s => s
                .DisplayName("Collect Data")
                .WithFieldGroups("personal-info", "contact-details", "preferences"))
            .Build();

        var state = result.States.Single();
        state.FieldGroupKeys.Should().HaveCount(3);
        state.FieldGroupKeys.Should().ContainInOrder("personal-info", "contact-details", "preferences");
    }

    [Fact]
    public void AddTransition_WithAllParameters_SetsAllPropertiesCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddTransition("from-state", "to-state", "approve", "reviewer")
            .Build();

        var transition = result.Transitions.Single();
        transition.FromState.Should().Be("from-state");
        transition.ToState.Should().Be("to-state");
        transition.Action.Should().Be("approve");
        transition.RequiresRole.Should().Be("reviewer");
    }

    [Fact]
    public void AddTransition_WithoutRequiresRole_SetsRequiresRoleToNull()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddTransition("from-state", "to-state", "submit")
            .Build();

        var transition = result.Transitions.Single();
        transition.RequiresRole.Should().BeNull();
    }

    [Fact]
    public void AddTransition_WithMultipleTransitions_AllTransitionsPresent()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddTransition("step1", "step2", "continue")
            .AddTransition("step2", "step3", "next")
            .AddTransition("step3", "done", "finish")
            .Build();

        result.Transitions.Should().HaveCount(3);
        result.Transitions[0].Action.Should().Be("continue");
        result.Transitions[1].Action.Should().Be("next");
        result.Transitions[2].Action.Should().Be("finish");
    }

    [Fact]
    public void Build_WithComplexWorkflow_ReturnsFullStructure()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("complex-workflow")
            .DisplayName("Complex Workflow Example")
            .Version(3)
            .StartsAt("initial")
            .InstancePolicy("prompt")
            .AddState("initial", s => s
                .DisplayName("Initial Step")
                .StepType("question")
                .WithFieldGroups("form1", "form2")
                .AllowActions("submit", "save"))
            .AddState("review", s => s
                .DisplayName("Review Step")
                .StepType("check-answers"))
            .AddState("complete", s => s
                .DisplayName("Complete")
                .StepType("confirmation"))
            .AddTransition("initial", "review", "submit")
            .AddTransition("review", "complete", "confirm", "admin")
            .Build();

        result.DefinitionKey.Should().Be("complex-workflow");
        result.States.Should().HaveCount(3);
        result.Transitions.Should().HaveCount(2);
        result.States[0].AllowedActions.Should().ContainInOrder("submit", "save");
        result.Transitions[1].RequiresRole.Should().Be("admin");
    }

    [Fact]
    public void Key_SetsDefinitionKey()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("unique-key")
            .Build();

        result.DefinitionKey.Should().Be("unique-key");
    }

    [Fact]
    public void DisplayName_SetsDisplayName()
    {
        var result = new WorkflowDefinitionBuilder()
            .DisplayName("My Workflow")
            .Build();

        result.DisplayName.Should().Be("My Workflow");
    }

    [Fact]
    public void StartsAt_SetsInitialState()
    {
        var result = new WorkflowDefinitionBuilder()
            .StartsAt("first-state")
            .Build();

        result.InitialState.Should().Be("first-state");
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multiple")]
    [InlineData("prompt")]
    public void InstancePolicy_SetsInstancePolicy(string policy)
    {
        var result = new WorkflowDefinitionBuilder()
            .InstancePolicy(policy)
            .Build();

        result.InstancePolicy.Should().Be(policy);
    }

    [Fact]
    public void AddState_WithAllowedActions_SetsAllowedActions()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("state1", s => s
                .AllowActions("action1", "action2", "action3"))
            .Build();

        var state = result.States.Single();
        state.AllowedActions.Should().HaveCount(3);
        state.AllowedActions.Should().ContainInOrder("action1", "action2", "action3");
    }
}
