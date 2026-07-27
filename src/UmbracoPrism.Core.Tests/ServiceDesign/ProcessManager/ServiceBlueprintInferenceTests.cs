using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;

namespace UmbracoPrism.Core.Tests.ServiceDesign.ProcessManager;

public class ServiceBlueprintInferenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void WaitingComponentWithoutAuthoredStepType_InfersWaitingMetadata()
    {
        var json = """
        {
          "definitionKey": "test",
          "displayName": "Test",
          "version": 1,
          "initialTouchpoint": "processing",
          "touchpoints": [
            {
              "touchpointKey": "processing",
              "displayName": "Processing",
              "components": [
                {
                  "type": "waiting",
                  "content": "Please wait while we process your request.",
                  "expectedWaitSeconds": 45,
                  "pollIntervalMs": 2500,
                  "allowDefer": false,
                  "deferMessage": "Come back later."
                }
              ]
            }
          ],
          "transitions": []
        }
        """;

        var workflow = JsonSerializer.Deserialize<ServiceBlueprint>(json, JsonOptions);

        var state = workflow!.Touchpoints.Single();
        // StepType is inferred from components via the InferStepType extension.
        state.Components.InferStepType().Should().Be("status-timeline");

        var waiting = state.Components.OfType<WaitingComponent>().Single();
        waiting.Content.Should().Be("Please wait while we process your request.");
        waiting.ExpectedWaitSeconds.Should().Be(45);
        waiting.PollIntervalMs.Should().Be(2500);
        waiting.AllowDefer.Should().BeFalse();
        waiting.DeferMessage.Should().Be("Come back later.");
    }

    [Fact]
    public void MissingStepType_IsInferredFromComponentShape()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "inference-test",
            DisplayName = "Inference Test",
            Version = 1,
            InitialTouchpoint = "question",
            Touchpoints = [
                new StepDefinition
                {
                    TouchpointKey = "question",
                    DisplayName = "Question",
                    Components =
                    [
                        new FieldsetComponent
                        {
                            Children =
                            [
                                new TextInputComponent { FieldKey = "name", Label = "Name", Required = true }
                            ]
                        }
                    ]
                },
                new StepDefinition
                {
                    TouchpointKey = "review",
                    DisplayName = "Review",
                    Components =
                    [
                        new SummaryListComponent { Children = [new TextInputComponent { FieldKey = "name", Label = "Name" }] }
                    ]
                },
                new StepDefinition
                {
                    TouchpointKey = "complete",
                    DisplayName = "Complete",
                    Components = [new PanelComponent { Heading = "Done" }]
                },
                new StepDefinition
                {
                    TouchpointKey = "status",
                    DisplayName = "Status",
                    Components = [new BodyComponent { Content = "No action needed." }]
                }
            ],
            Transitions = []
        };

        workflow.Touchpoints.Select(s => s.Components.InferStepType())
            .Should().ContainInOrder("question", "check-answers", "confirmation", "question");
    }

    [Theory]
    [InlineData("payment-demo.json")]
    [InlineData("community-enquiry.json")]
    [InlineData("information-request.json")]
    [InlineData("planning-notification.json")]
    public void DemoWorkflowSeeds_DoNotAuthorLegacyStepMetadata(string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GetSeedPath(fileName)));

        foreach (var state in document.RootElement.GetProperty("touchpoints").EnumerateArray())
        {
            state.TryGetProperty("stepType", out _).Should().BeFalse();
            state.TryGetProperty("waitingConfig", out _).Should().BeFalse();
        }
    }

    [Fact]
    public void CommunityEnquirySeed_ModelsExplanatoryCopyAsComponents()
    {
        var workflow = JsonSerializer.Deserialize<ServiceBlueprint>(
            File.ReadAllText(GetSeedPath("community-enquiry.json")),
            JsonOptions);

        var state = workflow!.Touchpoints.Single(s => s.TouchpointKey == "collecting-details");

        // Explanatory copy must live as standalone content components (inset-text, details, warning-text)
        // rather than being embedded inside fieldsets as fields.
        state.Components.OfType<InsetTextComponent>().Should().NotBeEmpty();
        state.Components.OfType<DetailsComponent>().Should().NotBeEmpty();
        state.Components.OfType<WarningTextComponent>().Should().NotBeEmpty();

        // Fieldsets should only contain InputComponents - never raw content like inset-text or details.
        foreach (var fieldset in state.Components.OfType<FieldsetComponent>())
        {
            fieldset.Children.Should().AllBeAssignableTo<InputComponent>();
        }
    }

    private static string GetSeedPath(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "UmbracoPrism.sln")))
            {
                return Path.Combine(current.FullName, "src", "UmbracoPrism.MockBusinessApp", "service-blueprints", fileName);
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
