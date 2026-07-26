using FluentAssertions;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public class GatewayProjectionTests
{
    private readonly ServiceBlueprintProjector _projector = new();

    [Fact]
    public void Project_QueueOnlyRoutes_AreEmittedForStatesAndGateways()
    {
        var result = _projector.Project(BuildTwoQueueWorkflow());

        result.HasErrors.Should().BeFalse();
        result.File.Transitions.Should().Contain(transition =>
            transition.FromState == "submit"
            && transition.ToState == "split-review"
            && transition.Action == "submit");
        result.File.Transitions.Should().Contain(transition =>
            transition.FromState == "split-review"
            && transition.ToState == "finance-review"
            && transition.Action == "submit");
        result.File.Transitions.Should().Contain(transition =>
            transition.FromState == "split-review"
            && transition.ToState == "planning-review"
            && transition.Action == "submit");
    }

    [Fact]
    public void Project_JoinGateway_EmitsRequiredIncomingQueuesInSortedOrder()
    {
        var result = _projector.Project(BuildTwoQueueWorkflow());

        var joinGateway = result.File.Gateways!.Single(gateway => gateway.Key == "join-reviews");
        joinGateway.RequiredIncomingQueues.Should().Equal("finance", "planning");
    }

    [Fact]
    public void Project_JoinGatewayWithoutWaitingInfo_ReportsProj137()
    {
        var workflow = BuildTwoQueueWorkflow() with
        {
            Gateways =
            [
                BuildTwoQueueWorkflow().Gateways[0],
                BuildTwoQueueWorkflow().Gateways[1],
                BuildTwoQueueWorkflow().Gateways[2],
                new AuthoredGateway
                {
                    GatewayKey = "join-reviews",
                    DisplayName = "All reviews done",
                    Kind = GatewayKind.Join,
                    QueueKey = "applicant",
                    RequiredIncomingQueues = ["finance", "planning"],
                    Routes = [new AuthoredRoute { Id = "release", Target = "decision", Trigger = "release" }]
                }
            ]
        };

        _projector.Project(workflow).Diagnostics.Should().Contain(d => d.Code == "PROJ137");
    }

    private static AuthoredServiceBlueprint BuildTwoQueueWorkflow() => new()
    {
        DefinitionKey = "gateway-test",
        DisplayName = "Gateway Test Workflow",
        Version = 1,
        InitialStageKey = "submit",
        RequestPolicy = "single",
        Queues =
        [
            new AuthoredQueue { Key = "applicant", DisplayName = "Applicant", Actor = "applicant" },
            new AuthoredQueue { Key = "finance", DisplayName = "Finance", Actor = "finance-officer" },
            new AuthoredQueue { Key = "planning", DisplayName = "Planning", Actor = "planning-officer" }
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "split-review",
                DisplayName = "Start parallel reviews",
                Description = "Branch into finance and planning queues.",
                Kind = GatewayKind.Split,
                QueueKey = "applicant",
                Routes =
                [
                    new AuthoredRoute { Id = "to-finance", Target = "finance-review", Trigger = "submit" },
                    new AuthoredRoute { Id = "to-planning", Target = "planning-review", Trigger = "submit" }
                ]
            },
            new AuthoredGateway
            {
                GatewayKey = "finance-out",
                DisplayName = "Finance routing",
                Kind = GatewayKind.Split,
                QueueKey = "finance",
                Routes = [new AuthoredRoute { Id = "approve", Target = "join-reviews", Trigger = "approve" }]
            },
            new AuthoredGateway
            {
                GatewayKey = "planning-out",
                DisplayName = "Planning routing",
                Kind = GatewayKind.Split,
                QueueKey = "planning",
                Routes = [new AuthoredRoute { Id = "approve", Target = "join-reviews", Trigger = "approve" }]
            },
            new AuthoredGateway
            {
                GatewayKey = "join-reviews",
                DisplayName = "All reviews done",
                Kind = GatewayKind.Join,
                QueueKey = "applicant",
                RequiredIncomingQueues = ["planning", "finance"],
                WaitingInfo = new WaitingMetadata
                {
                    Content = "Waiting for all reviews to complete.",
                    ExpectedWaitSeconds = 60,
                    PollIntervalMs = 5000
                },
                Routes = [new AuthoredRoute { Id = "release", Target = "decision", Trigger = "release" }]
            }
        ],
        Stages =
        [
            new AuthoredTouchpoint
            {
                StageKey = "submit",
                DisplayName = "Submit application",
                Kind = TouchpointKind.Question,
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "submit-route", Target = "split-review", Trigger = "submit" }]
            },
            new AuthoredTouchpoint
            {
                StageKey = "finance-review",
                DisplayName = "Finance review",
                Kind = TouchpointKind.Question,
                QueueKey = "finance",
                Routes = [new AuthoredRoute { Id = "finance-approve-route", Target = "finance-out", Trigger = "approve" }]
            },
            new AuthoredTouchpoint
            {
                StageKey = "planning-review",
                DisplayName = "Planning review",
                Kind = TouchpointKind.Question,
                QueueKey = "planning",
                Routes = [new AuthoredRoute { Id = "planning-approve-route", Target = "planning-out", Trigger = "approve" }]
            },
            new AuthoredTouchpoint
            {
                StageKey = "decision",
                DisplayName = "Final decision",
                Kind = TouchpointKind.Confirmation,
                QueueKey = "applicant"
            }
        ]
    };
}
