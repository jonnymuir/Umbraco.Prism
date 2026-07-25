using FluentAssertions;
using UmbracoPrism.Shared.Builders;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.Workflow.Components;

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
                .Fieldset(f => f.Legend("Personal info")))
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
    public void AddState_WithFieldsetComponent_InferredAsQuestion()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("step1", s => s
                .DisplayName("Step One")
                .Fieldset(f => f
                    .TextInput("name", "Name", required: true)))
            .Build();

        var state = result.States.Single();
        state.Components.Should().ContainSingle();
        state.Components[0].Should().BeOfType<FieldsetComponent>();
        state.Components.InferStepType().Should().Be("question");
    }

    [Fact]
    public void AddState_WithSummaryListComponent_InferredAsCheckAnswers()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("check", s => s
                .DisplayName("Check Answers")
                .SummaryList(sl => sl.Children(c => c.TextInput("name", "Name"))))
            .Build();

        var state = result.States.Single();
        state.Components.Should().ContainSingle();
        state.Components[0].Should().BeOfType<SummaryListComponent>();
        state.Components.InferStepType().Should().Be("check-answers");
    }

    [Fact]
    public void AddState_WithConfirmationContent_CreatesConfirmationState()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("done", s => s
                .DisplayName("Completed")
                .Panel("Your application has been submitted."))
            .Build();

        var state = result.States.Single();
        state.Components.Should().ContainSingle();
        state.Components[0].Should().BeOfType<PanelComponent>();
        state.Components.InferStepType().Should().Be("confirmation");
    }

    [Fact]
    public void AddState_WithWaitingComponent_InferredAsStatusTimeline()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("status", s => s
                .DisplayName("Status")
                .Waiting("Processing...", expectedWaitSeconds: 30))
            .Build();

        var state = result.States.Single();
        state.Components.Should().ContainSingle();
        state.Components[0].Should().BeOfType<WaitingComponent>();
        state.Components.InferStepType().Should().Be("status-timeline");
    }

    [Fact]
    public void AddState_WithTaskListComponent_CreatesTaskListState()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("tasks", s => s
                .DisplayName("Tasks")
                .Add(new TaskListComponent()))
            .Build();

        var state = result.States.Single();
        state.Components.Should().ContainSingle();
        state.Components[0].Should().BeOfType<TaskListComponent>();
        state.Components.InferStepType().Should().Be("task-list");
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
    public void AddState_WithMultipleFieldsets_SetsComponentsCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("collect", s => s
                .DisplayName("Collect Data")
                .Fieldset(f => f.Legend("Personal info"))
                .Fieldset(f => f.Legend("Contact details"))
                .Fieldset(f => f.Legend("Preferences")))
            .Build();

        var state = result.States.Single();
        state.Components.Should().HaveCount(3);
        state.Components.All(c => c is FieldsetComponent).Should().BeTrue();
        state.Components.OfType<FieldsetComponent>().Select(f => f.Legend)
            .Should().ContainInOrder("Personal info", "Contact details", "Preferences");
    }

    [Fact]
    public void AddState_WithInlineFieldset_SetsInlineFields()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("collect", s => s
                .DisplayName("Collect Data")
                .Fieldset(f => f
                    .Legend("About you")
                    .TextInput("full-name", "Full name", required: true)
                    .Email("email-address", "Email address", required: true)))
            .Build();

        var component = result.States.Single().Components.Single();
        component.Should().BeOfType<FieldsetComponent>();
        var fieldset = (FieldsetComponent)component;
        fieldset.Legend.Should().Be("About you");
        fieldset.Children.Should().HaveCount(2);
        fieldset.Children.OfType<InputComponent>().Select(c => c.FieldKey)
            .Should().ContainInOrder("full-name", "email-address");
    }

    [Fact]
    public void AddTransition_WithAllParameters_SetsAllPropertiesCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddTransition("from-state", "to-state", "approve", "reviewer")
            .Build();

        var transition = result.Transitions!.Single();
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

        var transition = result.Transitions!.Single();
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
        result.Transitions![0].Action.Should().Be("continue");
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
                .Fieldset(f => f.Legend("Form 1").TextInput("a", "A"))
                .Fieldset(f => f.Legend("Form 2").TextInput("b", "B")))
            .AddState("review", s => s
                .DisplayName("Review Step")
                .SummaryList(sl => sl.Children(c => c
                    .TextInput("a", "A")
                    .TextInput("b", "B"))))
            .AddState("complete", s => s
                .DisplayName("Complete")
                .Panel("Your application is complete."))
            .AddTransition("initial", "review", "submit")
            .AddTransition("review", "complete", "confirm", "admin")
            .Build();

        result.DefinitionKey.Should().Be("complex-workflow");
        result.States.Should().HaveCount(3);
        result.Transitions.Should().HaveCount(2);
        result.States[0].Components.Should().HaveCount(2);
        result.Transitions![1].RequiresRole.Should().Be("admin");
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
    public void AddState_WithBodyAndHeading_SetsComponentsCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("state1", s => s
                .Body("Some paragraph text")
                .Heading(2, "Title text"))
            .Build();

        var state = result.States.Single();
        state.Components.Should().HaveCount(2);
        state.Components[0].Should().BeOfType<BodyComponent>();
        ((BodyComponent)state.Components[0]).Content.Should().Be("Some paragraph text");
        state.Components[1].Should().BeOfType<HeadingComponent>();
        var heading = (HeadingComponent)state.Components[1];
        heading.Level.Should().Be(2);
        heading.Content.Should().Be("Title text");
    }
}
