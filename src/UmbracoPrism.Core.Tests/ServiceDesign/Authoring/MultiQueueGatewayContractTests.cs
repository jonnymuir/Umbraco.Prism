using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public class MultiQueueGatewayContractTests
{
    private readonly ServiceBlueprintProjector _projector = new();

    [Fact]
    public void ProjectedDefinition_UsesQueuesForOwnership()
    {
        var result = _projector.Project(BuildTwoQueueWorkflow());

        result.HasErrors.Should().BeFalse();
        result.File.Queues.Should().Contain(queue => queue.Key == "applicant");
        result.File.Queues.Should().Contain(queue => queue.Key == "caseworker");
        result.File.Touchpoints.Should().Contain(state => state.TouchpointKey == "applicant-details" && state.QueueKey == "applicant");
        result.File.Gateways.Should().Contain(gateway => gateway.Key == "review-split" && gateway.QueueKey == "applicant");
    }

    [Fact]
    public void JoinGateway_WaitingInfo_ProjectsToGateway()
    {
        var result = _projector.Project(BuildTwoQueueWorkflow());

        var join = result.File.Gateways!.Single(gateway => gateway.Key == "outcome-join");
        join.RequiredIncomingQueues.Should().Equal("applicant", "caseworker");
        join.WaitingContent.Should().Contain("caseworker decision");
    }

    [Fact]
    public void Project_IsDeterministic_WhenQueueOrderChanges()
    {
        var workflow = BuildTwoQueueWorkflow();
        var reversed = workflow with
        {
            Queues = workflow.Queues.Reverse().ToArray()
        };

        _projector.Project(workflow).Checksum.Should().Be(_projector.Project(reversed).Checksum);
    }

    private static AuthoredServiceBlueprint BuildTwoQueueWorkflow() => new()
    {
        DefinitionKey = "multi-queue-test",
        DisplayName = "Multi-Queue Test Workflow",
        InitialTouchpointKey = "applicant-details",
        Queues =
        [
            new AuthoredQueue { Key = "applicant", DisplayName = "Applicant", Actor = "applicant" },
            new AuthoredQueue { Key = "caseworker", DisplayName = "Caseworker", Actor = "caseworker" }
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "review-split",
                DisplayName = "Review split",
                Kind = GatewayKind.Split,
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "to-join", Target = "outcome-join", Trigger = "submit" }]
            },
            new AuthoredGateway
            {
                GatewayKey = "outcome-join",
                DisplayName = "Outcome join",
                Kind = GatewayKind.Join,
                QueueKey = "caseworker",
                WaitingInfo = new WaitingMetadata
                {
                    Content = "Waiting for the caseworker decision before the workflow can continue.",
                    ExpectedWaitSeconds = 300,
                    PollIntervalMs = 5000,
                    AllowDefer = false
                },
                RequiredIncomingQueues = ["applicant", "caseworker"],
                Routes = [new AuthoredRoute { Id = "release", Target = "caseworker-review", Trigger = "release-review" }]
            }
        ],
        Touchpoints =
        [
            new AuthoredTouchpoint
            {
                TouchpointKey = "applicant-details",
                DisplayName = "Your details",
                Kind = TouchpointKind.Question,
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "submit-route", Target = "review-split", Trigger = "submit" }],
                Actions =
                [
                    new AuthoredAction
                    {
                        Type = "forms.load",
                        Timing = ActionTiming.OnEntry,
                        ParameterSchemaKey = "forms-form-definition",
                        Parameters = new JsonObject { ["formDefinitionId"] = "details-form" }
                    }
                ],
                Components =
                [
                    new FieldsetComponent
                    {
                        Children =
                        [
                            new TextInputComponent { FieldKey = "name", Label = "Full name", Required = true }
                        ]
                    }
                ]
            },
            new AuthoredTouchpoint
            {
                TouchpointKey = "caseworker-review",
                DisplayName = "Caseworker review",
                Kind = TouchpointKind.Question,
                QueueKey = "caseworker"
            }
        ],
        ParameterSchemas =
        [
            new AuthoredParameterSchema
            {
                Key = "forms-form-definition",
                AppliesTo = ["forms.load"],
                AllowAdditionalProperties = false,
                Properties =
                [
                    new AuthoredParameterDefinition { Key = "formDefinitionId", ValueKind = ParameterValueKind.String }
                ],
                Required = ["formDefinitionId"]
            }
        ]
    };
}
