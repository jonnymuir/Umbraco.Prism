using FluentAssertions;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Verifies that the projector emits component trees that satisfy the existing shell-inference
/// rules in <see cref="PrismComponentExtensions.InferStepType"/>.
///
/// The projector does NOT hard-code step types; it passes authored components straight through
/// (or falls back to a kind-shaped default when a stage declares none). This test validates
/// that coupling without duplicating the inference logic.
/// </summary>
public class ServiceBlueprintProjectorShellInferenceTests
{
    private readonly ServiceBlueprintProjector _projector = new();

    [Fact]
    public void QuestionStage_EmitsFieldset_InfersQuestion()
    {
        var authored = SingleStageWorkflow("details", TouchpointKind.Question, components:
        [
            new FieldsetComponent
            {
                Legend = "Your details",
                Children =
                [
                    new TextInputComponent { FieldKey = "name", Label = "Full name", Required = true }
                ]
            }
        ]);

        var result = _projector.Project(authored);

        var state = result.File.Touchpoints.Single(s => s.TouchpointKey == "details");
        state.Components.InferStepType().Should().Be("question",
            "a stage with a FieldsetComponent should infer as 'question'");

        state.Components.Should().ContainSingle().Which.Should().BeOfType<FieldsetComponent>();
    }

    [Fact]
    public void CheckAnswersStage_AuthoredSummaryList_PassesThrough()
    {
        var authored = new AuthoredServiceBlueprint
        {
            Id = new Guid("aaaabbbb-0000-0000-0000-000000000010"),
            DefinitionKey = "check-answers-test",
            DisplayName = "Check Answers Test",
            Version = 1,
            InitialTouchpointKey = "collect",
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "collect",
                    DisplayName = "Collect",
                    Kind = TouchpointKind.Question,
                    Components =
                    [
                        new FieldsetComponent
                        {
                            Children =
                            [
                                new EmailComponent { FieldKey = "email", Label = "Email", Required = true }
                            ]
                        }
                    ]
                },
                new AuthoredTouchpoint
                {
                    TouchpointKey = "review",
                    DisplayName = "Review",
                    Kind = TouchpointKind.CheckAnswers,
                    Components =
                    [
                        new SummaryListComponent
                        {
                            Title = "Your answers",
                            Children =
                            [
                                new EmailComponent { FieldKey = "email", Label = "Email", Required = true }
                            ]
                        }
                    ]
                }
            ],
        };

        var result = _projector.Project(authored);

        var reviewState = result.File.Touchpoints.Single(s => s.TouchpointKey == "review");
        reviewState.Components.InferStepType().Should().Be("check-answers",
            "a stage with a SummaryListComponent should infer as 'check-answers'");

        var summary = reviewState.Components.Should().ContainSingle()
            .Which.Should().BeOfType<SummaryListComponent>().Subject;
        summary.Title.Should().Be("Your answers");
        summary.Children.OfType<InputComponent>().Select(c => c.FieldKey)
            .Should().BeEquivalentTo(["email"]);
    }

    [Fact]
    public void ConfirmationStage_EmitsPanel_InfersConfirmation()
    {
        var authored = SingleStageWorkflow("done", TouchpointKind.Confirmation);

        var result = _projector.Project(authored);

        var state = result.File.Touchpoints.Single(s => s.TouchpointKey == "done");
        state.Components.InferStepType().Should().Be("confirmation",
            "an empty Confirmation stage should fall back to a PanelComponent");

        state.Components.Should().ContainSingle().Which.Should().BeOfType<PanelComponent>();
    }

    [Fact]
    public void TaskListStage_EmitsTaskList_InfersTaskList()
    {
        var authored = SingleStageWorkflow("tasks", TouchpointKind.TaskList);

        var result = _projector.Project(authored);

        var state = result.File.Touchpoints.Single(s => s.TouchpointKey == "tasks");
        state.Components.InferStepType().Should().Be("task-list",
            "an empty TaskList stage should fall back to a TaskListComponent");

        state.Components.Should().ContainSingle().Which.Should().BeOfType<TaskListComponent>();
    }

    [Fact]
    public void CheckAnswersStage_EmptyComponents_FallsBackToHarvestedQuestionInputs()
    {
        var authored = new AuthoredServiceBlueprint
        {
            Id = new Guid("aaaabbbb-0000-0000-0000-000000000011"),
            DefinitionKey = "summary-fields-test",
            DisplayName = "Summary Fields Test",
            Version = 1,
            InitialTouchpointKey = "step1",
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "step1",
                    DisplayName = "Step 1",
                    Kind = TouchpointKind.Question,
                    Components =
                    [
                        new FieldsetComponent
                        {
                            Children =
                            [
                                new TextInputComponent { FieldKey = "first-name", Label = "First name", Required = true }
                            ]
                        }
                    ]
                },
                new AuthoredTouchpoint
                {
                    TouchpointKey = "step2",
                    DisplayName = "Step 2",
                    Kind = TouchpointKind.Question,
                    Components =
                    [
                        new FieldsetComponent
                        {
                            Children =
                            [
                                new NumberInputComponent { FieldKey = "age", Label = "Age", Required = true }
                            ]
                        }
                    ]
                },
                new AuthoredTouchpoint
                {
                    TouchpointKey = "review",
                    DisplayName = "Check answers",
                    Kind = TouchpointKind.CheckAnswers
                    // No Components: projector falls back to a harvested SummaryListComponent.
                }
            ],
        };

        var result = _projector.Project(authored);

        var reviewState = result.File.Touchpoints.Single(s => s.TouchpointKey == "review");
        var summaryList = reviewState.Components.Should().ContainSingle()
            .Which.Should().BeOfType<SummaryListComponent>().Subject;

        var fieldKeys = summaryList.Children.OfType<InputComponent>().Select(c => c.FieldKey).ToList();
        fieldKeys.Should().Contain("first-name");
        fieldKeys.Should().Contain("age");
    }

    [Fact]
    public async Task PlanningFixture_AllStages_HaveCorrectInferredShells()
    {
        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "ServiceDesign", "Authoring", "Fixtures");
        var authored = await AuthoredServiceBlueprintFixtureLoader.LoadAsync(fixturesPath, "planning");

        var result = _projector.Project(authored!);

        result.HasErrors.Should().BeFalse("planning fixture must project without errors");

        var shells = result.File.Touchpoints.ToDictionary(s => s.TouchpointKey, s => s.Components.InferStepType());
        shells["declaration"].Should().Be("question");
        shells["application-form"].Should().Be("question");
        shells["check-answers"].Should().Be("check-answers");
        shells["submitted"].Should().Be("confirmation");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AuthoredServiceBlueprint SingleStageWorkflow(
        string stageKey,
        TouchpointKind kind,
        IReadOnlyList<PrismComponent>? components = null)
    {
        return new AuthoredServiceBlueprint
        {
            Id = Guid.NewGuid(),
            DefinitionKey = $"{stageKey}-shell-test",
            DisplayName = "Shell Test",
            Version = 1,
            InitialTouchpointKey = stageKey,
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = stageKey,
                    DisplayName = stageKey,
                    Kind = kind,
                    Components = components ?? []
                }
            ],
        };
    }
}
