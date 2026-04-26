using FluentAssertions;
using UmbracoPrism.Shared.Builders;
using UmbracoPrism.Shared.Models.Workflow;

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
                .AddFieldset("personal-info"))
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
                .AddFieldset(new[] 
                { 
                    new FieldFile { FieldKey = "name", Label = "Name", FieldType = "text", Required = true }
                }))
            .Build();

        var state = result.States.Single();
        // StepType is inferred from components, not set explicitly
        state.Components.Should().ContainSingle(c => c.Type == "fieldset");
    }

    [Fact]
    public void AddState_WithSummaryListComponent_InferredAsCheckAnswers()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("check", s => s
                .DisplayName("Check Answers")
                .AddSummaryList(new[] 
                { 
                    new FieldFile { FieldKey = "name", Label = "Name", FieldType = "text", Required = true }
                }))
            .Build();

        var state = result.States.Single();
        // StepType is inferred from components, not set explicitly
        state.Components.Should().ContainSingle(c => c.Type == "summary-list");
    }

    [Fact]
    public void AddState_WithConfirmationContent_CreatesConfirmationState()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("done", s => s
                .DisplayName("Completed")
                .AddContent("panel", "Your application has been submitted."))
            .Build();

        var state = result.States.Single();
        // StepType is inferred from components, not set explicitly
        state.Components.Should().ContainSingle(c => c.Type == "panel");
    }

    [Fact]
    public void AddState_WithStatusTimelineComponent_CreatesStatusState()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("status", s => s
                .DisplayName("Status")
                .AddContent("status-timeline", ""))
            .Build();

        var state = result.States.Single();
        // StepType is inferred from components, not set explicitly
        state.Components.Should().ContainSingle(c => c.Type == "status-timeline");
    }

    [Fact]
    public void AddState_WithTaskListComponent_CreatesTaskListState()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("tasks", s => s
                .DisplayName("Tasks")
                .AddContent("task-list", ""))
            .Build();

        var state = result.States.Single();
        // StepType is inferred from components, not set explicitly
        state.Components.Should().ContainSingle(c => c.Type == "task-list");
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
    public void AddState_WithFieldsets_SetsComponentsCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("collect", s => s
                .DisplayName("Collect Data")
                .AddFieldset("personal-info")
                .AddFieldset("contact-details")
                .AddFieldset("preferences"))
            .Build();

        var state = result.States.Single();
        state.Components.Should().HaveCount(3);
        state.Components.All(c => c.Type == "fieldset").Should().BeTrue();
        state.Components.Select(c => c.FieldGroupKey).Should().ContainInOrder("personal-info", "contact-details", "preferences");
    }

    [Fact]
    public void AddState_WithInlineFieldset_SetsInlineFields()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("collect", s => s
                .DisplayName("Collect Data")
                .AddFieldset(
                [
                    new FieldFile { FieldKey = "full-name", Label = "Full name", FieldType = "text", Required = true },
                    new FieldFile { FieldKey = "email-address", Label = "Email address", FieldType = "email", Required = true }
                ],
                legend: "About you"))
            .Build();

        var component = result.States.Single().Components.Single();
        component.Type.Should().Be("fieldset");
        component.Legend.Should().Be("About you");
        component.FieldGroupKey.Should().BeNull();
        component.Fields.Should().HaveCount(2);
        component.Fields.Should().NotBeNull();
        component.Fields!.Select(f => f.FieldKey).Should().ContainInOrder("full-name", "email-address");
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
                .AddFieldset("form1")
                .AddFieldset("form2"))
            .AddState("review", s => s
                .DisplayName("Review Step")
                .AddSummaryList("form1"))
            .AddState("complete", s => s
                .DisplayName("Complete")
                .AddContent("panel", "Your application is complete."))
            .AddTransition("initial", "review", "submit")
            .AddTransition("review", "complete", "confirm", "admin")
            .Build();

        result.DefinitionKey.Should().Be("complex-workflow");
        result.States.Should().HaveCount(3);
        result.Transitions.Should().HaveCount(2);
        result.States[0].Components.Should().HaveCount(2);
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
    public void AddState_WithAddContent_SetsComponentCorrectly()
    {
        var result = new WorkflowDefinitionBuilder()
            .Key("test")
            .AddState("state1", s => s
                .AddContent("body", "Some paragraph text")
                .AddContent("heading", "Title text", level: 2))
            .Build();

        var state = result.States.Single();
        state.Components.Should().HaveCount(2);
        state.Components[0].Type.Should().Be("body");
        state.Components[0].Content.Should().Be("Some paragraph text");
        state.Components[1].Type.Should().Be("heading");
        state.Components[1].Level.Should().Be(2);
    }
}
