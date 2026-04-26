using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Tests.WorkflowEngine;

public class WorkflowDefinitionInferenceTests
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
          "initialState": "processing",
          "states": [
            {
              "stateKey": "processing",
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

        var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(json, JsonOptions);

        var state = workflow!.States.Single();
        // StepType is no longer set explicitly; it's inferred via EffectiveStepType
        state.EffectiveStepType.Should().Be("waiting");
        state.EffectiveWaitingConfig.Should().BeEquivalentTo(new WaitingConfig
        {
            Message = "Please wait while we process your request.",
            ExpectedWaitSeconds = 45,
            PollIntervalMs = 2500,
            AllowDefer = false,
            DeferMessage = "Come back later."
        });
    }

    [Fact]
    public void MissingStepType_IsInferredFromComponentShape()
    {
        var workflow = new WorkflowDefinitionFile
        {
            DefinitionKey = "inference-test",
            DisplayName = "Inference Test",
            Version = 1,
            InitialState = "question",
            States =
            [
                new StepDefinition
                {
                    StateKey = "question",
                    DisplayName = "Question",
                    Components =
                    [
                        new PrismComponentDefinition
                        {
                            Type = "fieldset",
                            Fields = [new FieldFile { FieldKey = "name", Label = "Name", FieldType = "text", Required = true }]
                        }
                    ]
                },
                new StepDefinition
                {
                    StateKey = "review",
                    DisplayName = "Review",
                    Components =
                    [
                        new PrismComponentDefinition
                        {
                            Type = "summary-list",
                            Fields = [new FieldFile { FieldKey = "name", Label = "Name", FieldType = "text", Required = true }]
                        }
                    ]
                },
                new StepDefinition
                {
                    StateKey = "complete",
                    DisplayName = "Complete",
                    Components = [new PrismComponentDefinition { Type = "panel", Heading = "Done" }]
                },
                new StepDefinition
                {
                    StateKey = "status",
                    DisplayName = "Status",
                    Components = [new PrismComponentDefinition { Type = "body", Content = "No action needed." }]
                }
            ],
            Transitions = []
        };

        workflow.States.Select(s => s.EffectiveStepType).Should().ContainInOrder("question", "check-answers", "confirmation", "question");
    }

    [Theory]
    [InlineData("payment-demo-v1.json")]
    [InlineData("community-enquiry-v1.json")]
    [InlineData("information-request-v1.json")]
    [InlineData("planning-notification-v1.json")]
    public void DemoWorkflowSeeds_DoNotAuthorLegacyStepMetadata(string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GetSeedPath(fileName)));

        foreach (var state in document.RootElement.GetProperty("states").EnumerateArray())
        {
            state.TryGetProperty("stepType", out _).Should().BeFalse();
            state.TryGetProperty("waitingConfig", out _).Should().BeFalse();
        }
    }

    [Fact]
    public void CommunityEnquirySeed_ModelsExplanatoryCopyAsComponents()
    {
        var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(
            File.ReadAllText(GetSeedPath("community-enquiry-v1.json")),
            JsonOptions);

        var state = workflow!.States.Single(s => s.StateKey == "collecting-details");
        var contentFieldTypes = new[] { "inset-text", "details", "warning-text" };

        state.Components.Select(c => c.Type).Should().Contain(["inset-text", "details", "warning-text"]);
        state.Components
            .Where(c => string.Equals(c.Type, "fieldset", StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => c.Fields ?? Array.Empty<FieldFile>())
            .Select(f => f.FieldType)
            .Should()
            .NotIntersectWith(contentFieldTypes);
    }

    private static string GetSeedPath(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "UmbracoPrism.sln")))
            {
                return Path.Combine(current.FullName, "src", "UmbracoPrism.MockBusinessApp", "workflow-seeds", fileName);
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
