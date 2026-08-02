using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UmbracoPrism.MockBusinessApp.Services;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.ServiceDesign.ProcessManager;

public class BusinessAppWorkflowEngineStepTypeInferenceTests
{
    [Fact]
    public void WorkflowStatesWithoutAuthoredStepType_AreInferredAcrossQuestionReviewAndConfirmation()
    {
        using var harness = WorkflowHarness.Create(new ServiceBlueprint
        {
            DefinitionKey = "inferred-authoring",
            DisplayName = "Inferred Authoring",
            Version = 1,
            InitialStage = "enter-details",
            RequestPolicy = "single",
            Stages = [
                new StageDefinition
                {
                    StageKey = "enter-details",
                    DisplayName = "Enter details",
                    Components =
                    [
                        new FieldsetComponent
                        {
                            Legend = "About you",
                            Children =
                            [
                                new TextInputComponent
                                {
                                    FieldKey = "full-name",
                                    Label = "Full name",
                                    Required = true
                                }
                            ]
                        }
                    ],
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "enter-details--submit--to-check-details", Target = "to-check-details", Trigger = "submit" }]
                },
                new StageDefinition
                {
                    StageKey = "check-details",
                    DisplayName = "Check details",
                    Components =
                    [
                        new SummaryListComponent
                        {
                            Title = "Check your answers",
                            Children = [new TextInputComponent { FieldKey = "full-name", Label = "Full name" }],
                            ChangeStateKey = "enter-details"
                        }
                    ],
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "check-details--submit--to-done", Target = "to-done", Trigger = "submit" }]
                },
                new StageDefinition
                {
                    StageKey = "done",
                    DisplayName = "Done",
                    Components =
                    [
                        new PanelComponent { Heading = "Submission complete" }
                    ]
                }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "to-check-details",
                    DisplayName = "To check details",
                    GatewayType = "Split",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "to-check-details--continue--check-details", Target = "check-details", Trigger = "continue" }]
                },
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "to-done",
                    DisplayName = "To done",
                    GatewayType = "Split",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "to-done--continue--done", Target = "done", Trigger = "continue" }]
                }
            ]
        });

        var first = harness.Engine.GetCurrent("inferred-authoring", "tenant1", "user1");

        first.Render!.StepType.Should().Be("question");
        first.ResponseState.Should().Be("render");

        harness.Engine.Advance(
            first.InstanceId,
            "tenant1",
            "user1",
            "submit",
            expectedStateVersion: 0,
            fieldValues: new Dictionary<string, object?> { ["full-name"] = "Demo User" });

        var review = harness.Engine.GetCurrent("inferred-authoring", "tenant1", "user1");

        review.Render!.StepType.Should().Be("check-answers");
        review.ResponseState.Should().Be("render");
        review.Render.Components.Should().ContainSingle(component => component.Type == "summary-list");
        review.Render.Components
            .Single(component => component.Type == "summary-list")
            .Fields!
            .Single(field => field.FieldKey == "full-name")
            .Value.Should().Be("Demo User");

        harness.Engine.Advance(
            review.InstanceId,
            "tenant1",
            "user1",
            "submit",
            expectedStateVersion: 1,
            fieldValues: new Dictionary<string, object?>());

        var complete = harness.Engine.GetCurrent("inferred-authoring", "tenant1", "user1");

        complete.Render!.StepType.Should().Be("confirmation");
        complete.ResponseState.Should().Be("complete");
    }

    [Fact]
    public void WaitingComponentWithoutAuthoredStepType_PopulatesWaitingEnvelopeAndPollingMetadata()
    {
        using var harness = WorkflowHarness.Create(new ServiceBlueprint
        {
            DefinitionKey = "component-waiting",
            DisplayName = "Component Waiting",
            Version = 1,
            InitialStage = "processing",
            RequestPolicy = "single",
            Stages = [
                new StageDefinition
                {
                    StageKey = "processing",
                    DisplayName = "Processing payment",
                    Components =
                    [
                        new WaitingComponent
                        {
                            Content = "We are processing your payment.",
                            ExpectedWaitSeconds = 30,
                            PollIntervalMs = 1500,
                            AllowDefer = true,
                            DeferMessage = "Leave this page and check My Applications later."
                        },
                        new BodyComponent
                        {
                            Content = "You do not need to do anything else right now."
                        }
                    ],
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "processing--complete--to-done", Target = "to-done", Trigger = "complete" }]
                },
                new StageDefinition
                {
                    StageKey = "done",
                    DisplayName = "Payment complete",
                    Components =
                    [
                        new PanelComponent { Heading = "Payment received" }
                    ]
                }
            ],
            Gateways =
            [
                new ServiceBlueprintGatewayDefinition
                {
                    Key = "to-done",
                    DisplayName = "To done",
                    GatewayType = "Split",
                    Routes = [new ServiceBlueprintRouteDefinition { Id = "to-done--continue--done", Target = "done", Trigger = "continue" }]
                }
            ]
        });

        var waiting = harness.Engine.GetCurrent("component-waiting", "tenant1", "user1");

        waiting.Render!.StepType.Should().Be("status-timeline");
        waiting.ResponseState.Should().Be("defer");
        waiting.PollAfterMs.Should().Be(1500);
        var waitingComponent = waiting.Render.Components.FirstOrDefault(c =>
            string.Equals(c.Type, "waiting", StringComparison.OrdinalIgnoreCase));
        waitingComponent.Should().NotBeNull();
        waitingComponent!.Content.Should().Be("We are processing your payment.");
        waitingComponent.ExpectedWaitSeconds.Should().Be(30);
        waitingComponent.PollIntervalMs.Should().Be(1500);
        waitingComponent.AllowDefer.Should().Be(true);
        waitingComponent.DeferMessage.Should().Be("Leave this page and check My Applications later.");
        waiting.Render.Components.Should().ContainSingle(component => component.Type == "body");

        harness.Engine.Advance(
            waiting.InstanceId,
            "tenant1",
            "user1",
            "complete",
            expectedStateVersion: 0,
            fieldValues: new Dictionary<string, object?>());

        var complete = harness.Engine.GetCurrent("component-waiting", "tenant1", "user1");

        complete.Render!.StepType.Should().Be("confirmation");
        complete.ResponseState.Should().Be("complete");
    }

    private sealed class WorkflowHarness : IDisposable
    {
        private readonly string _contentRootPath;

        private WorkflowHarness(string contentRootPath, BusinessAppProcessManager engine)
        {
            _contentRootPath = contentRootPath;
            Engine = engine;
        }

        public BusinessAppProcessManager Engine { get; }

        public static WorkflowHarness Create(ServiceBlueprint workflow)
        {
            var contentRootPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                $"test-seeds-step-inference-{Guid.NewGuid():N}");

            Directory.CreateDirectory(Path.Combine(contentRootPath, "service-blueprints"));

            File.WriteAllText(
                Path.Combine(contentRootPath, "service-blueprints", $"{workflow.DefinitionKey}.json"),
                JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true }));

            var mockEnvironment = new Mock<IWebHostEnvironment>();
            mockEnvironment.Setup(environment => environment.ContentRootPath).Returns(contentRootPath);

            var logger = new Mock<ILogger<BusinessAppProcessManager>>();
            var sanitizer = new Mock<IServiceContentSanitizer>();
            sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(h => h ?? string.Empty);
            var engine = new BusinessAppProcessManager(logger.Object, mockEnvironment.Object, sanitizer.Object);
            engine.ResetAll();

            return new WorkflowHarness(contentRootPath, engine);
        }

        public void Dispose()
        {
            if (Directory.Exists(_contentRootPath))
            {
                Directory.Delete(_contentRootPath, recursive: true);
            }
        }
    }
}
