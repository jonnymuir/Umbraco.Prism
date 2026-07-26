using FluentAssertions;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public class ServiceBlueprintSimulationServiceTests
{
    private readonly ServiceBlueprintSimulationService _service = new();

    [Fact]
    public void Simulate_WalksThroughSplitGateway_AndArrivesAtNextStage()
    {
        // Authoring contract: a stage's outgoing routing is owned by a single split gateway.
        // The simulator walks through the gateway and lands on the route target.
        var workflow = new AuthoredServiceBlueprint
        {
            DefinitionKey = "split-walk",
            DisplayName = "Split walk",
            InitialStageKey = "start",
            Queues = [new AuthoredQueue { Key = "applicant", DisplayName = "Applicant" }],
            Stages =
            [
                new AuthoredTouchpoint { StageKey = "start", DisplayName = "Start", QueueKey = "applicant" },
                new AuthoredTouchpoint { StageKey = "end", DisplayName = "End", Kind = TouchpointKind.Confirmation, QueueKey = "applicant" }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "fan-out",
                    DisplayName = "Fan out",
                    Kind = GatewayKind.Split,
                    QueueKey = "applicant",
                    Source = "start",
                    Routes = [new AuthoredRoute { Id = "r1", Target = "end", Trigger = "continue" }]
                }
            ]
        };

        var result = _service.Simulate(workflow, actions: new[] { "continue" });

        result.CurrentStageKey.Should().Be("end",
            "simulating from the first stage should walk through the gateway and arrive at the route target");
        result.Steps.Should().ContainSingle()
            .Which.Should().Match<WorkflowSimulationStep>(step =>
                step.FromStageKey == "start" &&
                step.ToStageKey == "end" &&
                step.Action == "continue");
        result.StopReason.Should().Be("terminal-stage");
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public void Simulate_StopsAtJoinGateway_WithWaitingGatewayReason()
    {
        // A join gateway is a synchronisation point; the simulator pauses with a dedicated
        // stop reason so the UI can surface the waiting copy.
        var workflow = new AuthoredServiceBlueprint
        {
            DefinitionKey = "join-pause",
            DisplayName = "Join pause",
            InitialStageKey = "start",
            Queues = [new AuthoredQueue { Key = "applicant", DisplayName = "Applicant" }],
            Stages =
            [
                new AuthoredTouchpoint { StageKey = "start", DisplayName = "Start", QueueKey = "applicant" },
                new AuthoredTouchpoint { StageKey = "end", DisplayName = "End", Kind = TouchpointKind.Confirmation, QueueKey = "applicant" }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "route-to-join",
                    DisplayName = "Route to join",
                    Kind = GatewayKind.Split,
                    QueueKey = "applicant",
                    Source = "start",
                    Routes = [new AuthoredRoute { Id = "r1", Target = "join-here", Trigger = "continue" }]
                },
                new AuthoredGateway
                {
                    GatewayKey = "join-here",
                    DisplayName = "Join here",
                    Kind = GatewayKind.Join,
                    QueueKey = "applicant",
                    WaitingInfo = new WaitingMetadata { Content = "Waiting for parallel branches." },
                    RequiredIncomingQueues = ["applicant"],
                    Routes = [new AuthoredRoute { Id = "release", Target = "end", Trigger = "release" }]
                }
            ]
        };

        var result = _service.Simulate(workflow, actions: new[] { "continue" });

        result.StopReason.Should().Be("waiting-gateway");
        result.CurrentStageKey.Should().Be("join-here");
        result.Completed.Should().BeFalse();
    }
}
