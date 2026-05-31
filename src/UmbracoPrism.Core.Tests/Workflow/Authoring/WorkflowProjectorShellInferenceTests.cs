using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Verifies that the projector emits component trees that satisfy the existing shell-inference
/// rules in <see cref="PrismComponentExtensions.InferStepType"/>.
///
/// The projector does NOT hard-code step types; it emits components whose presence causes
/// the runtime's own inference to produce the correct shell string. This test validates that
/// coupling without duplicating the inference logic.
/// </summary>
public class WorkflowProjectorShellInferenceTests
{
    private readonly WorkflowProjector _projector = new();

    [Fact]
    public void QuestionStage_EmitsFieldset_InfersQuestion()
    {
        var authored = SingleStageWorkflow("details", StageKind.Question, fields:
        [
            new AuthoredField { Key = "name", Label = "Full name", Type = FieldType.Text, Required = true }
        ]);

        var result = _projector.Project(authored);

        var state = result.File.States.Single(s => s.StateKey == "details");
        state.Components.InferStepType().Should().Be("question",
            "a stage with only a FieldsetComponent should infer as 'question'");

        state.Components.Should().ContainSingle().Which.Should().BeOfType<FieldsetComponent>();
    }

    [Fact]
    public void CheckAnswersStage_EmitsSummaryList_InfersCheckAnswers()
    {
        var authored = new AuthoredWorkflow
        {
            Id = new Guid("aaaabbbb-0000-0000-0000-000000000010"),
            DefinitionKey = "check-answers-test",
            DisplayName = "Check Answers Test",
            Version = 1,
            InitialStageKey = "collect",
            Stages =
            [
                new AuthoredStage
                {
                    StageKey = "collect",
                    DisplayName = "Collect",
                    Kind = StageKind.Question,
                    Fields =
                    [
                        new AuthoredField { Key = "email", Label = "Email", Type = FieldType.Email, Required = true }
                    ]
                },
                new AuthoredStage
                {
                    StageKey = "review",
                    DisplayName = "Review",
                    Kind = StageKind.CheckAnswers
                }
            ],
        };

        var result = _projector.Project(authored);

        var reviewState = result.File.States.Single(s => s.StateKey == "review");
        reviewState.Components.InferStepType().Should().Be("check-answers",
            "a stage with a SummaryListComponent should infer as 'check-answers'");

        reviewState.Components.Should().ContainSingle().Which.Should().BeOfType<SummaryListComponent>();
    }

    [Fact]
    public void ConfirmationStage_EmitsPanel_InfersConfirmation()
    {
        var authored = SingleStageWorkflow("done", StageKind.Confirmation);

        var result = _projector.Project(authored);

        var state = result.File.States.Single(s => s.StateKey == "done");
        state.Components.InferStepType().Should().Be("confirmation",
            "a stage with a PanelComponent should infer as 'confirmation'");

        state.Components.Should().ContainSingle().Which.Should().BeOfType<PanelComponent>();
    }

    [Fact]
    public void TaskListStage_EmitsTaskList_InfersTaskList()
    {
        var authored = SingleStageWorkflow("tasks", StageKind.TaskList);

        var result = _projector.Project(authored);

        var state = result.File.States.Single(s => s.StateKey == "tasks");
        state.Components.InferStepType().Should().Be("task-list",
            "a stage with a TaskListComponent should infer as 'task-list'");

        state.Components.Should().ContainSingle().Which.Should().BeOfType<TaskListComponent>();
    }

    [Fact]
    public void CheckAnswersStage_SummaryListContains_QuestionStageFields()
    {
        var authored = new AuthoredWorkflow
        {
            Id = new Guid("aaaabbbb-0000-0000-0000-000000000011"),
            DefinitionKey = "summary-fields-test",
            DisplayName = "Summary Fields Test",
            Version = 1,
            InitialStageKey = "step1",
            Stages =
            [
                new AuthoredStage
                {
                    StageKey = "step1",
                    DisplayName = "Step 1",
                    Kind = StageKind.Question,
                    Fields =
                    [
                        new AuthoredField { Key = "first-name", Label = "First name", Type = FieldType.Text, Required = true }
                    ]
                },
                new AuthoredStage
                {
                    StageKey = "step2",
                    DisplayName = "Step 2",
                    Kind = StageKind.Question,
                    Fields =
                    [
                        new AuthoredField { Key = "age", Label = "Age", Type = FieldType.Number, Required = true }
                    ]
                },
                new AuthoredStage
                {
                    StageKey = "review",
                    DisplayName = "Check answers",
                    Kind = StageKind.CheckAnswers
                }
            ],
        };

        var result = _projector.Project(authored);

        var reviewState = result.File.States.Single(s => s.StateKey == "review");
        var summaryList = reviewState.Components.Should().ContainSingle()
            .Which.Should().BeOfType<SummaryListComponent>().Subject;

        var fieldKeys = summaryList.Children.OfType<InputComponent>().Select(c => c.FieldKey).ToList();
        fieldKeys.Should().Contain("first-name");
        fieldKeys.Should().Contain("age");
    }

    [Fact]
    public async Task PlanningFixture_AllStages_HaveCorrectInferredShells()
    {
        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Workflow", "Authoring", "Fixtures");
        var authored = await AuthoredWorkflowFixtureLoader.LoadAsync(fixturesPath, "planning");

        var result = _projector.Project(authored!);

        result.HasErrors.Should().BeFalse("planning fixture must project without errors");

        var shells = result.File.States.ToDictionary(s => s.StateKey, s => s.Components.InferStepType());
        shells["declaration"].Should().Be("question");
        shells["application-form"].Should().Be("question");
        shells["check-answers"].Should().Be("check-answers");
        shells["submitted"].Should().Be("confirmation");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AuthoredWorkflow SingleStageWorkflow(
        string stageKey,
        StageKind kind,
        IReadOnlyList<AuthoredField>? fields = null)
    {
        return new AuthoredWorkflow
        {
            Id = Guid.NewGuid(),
            DefinitionKey = $"{stageKey}-shell-test",
            DisplayName = "Shell Test",
            Version = 1,
            InitialStageKey = stageKey,
            Stages =
            [
                new AuthoredStage
                {
                    StageKey = stageKey,
                    DisplayName = stageKey,
                    Kind = kind,
                    Fields = fields ?? []
                }
            ],
        };
    }
}
