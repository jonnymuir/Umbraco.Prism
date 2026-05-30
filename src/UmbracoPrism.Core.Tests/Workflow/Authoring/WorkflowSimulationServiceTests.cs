using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class WorkflowSimulationServiceTests
{
    private readonly WorkflowSimulationService _service = new();

    [Fact]
    public void Simulate_WalksThroughSplitGateway_AndArrivesAtNextStage()
    {
        // Authoring contract: a stage hands off to a gateway, which routes to the next stage.
        // The simulator must transparently walk through the split gateway and land on the
        // downstream stage so authors see a single "step" per author-initiated action.
        var workflow = new AuthoredWorkflow
        {
            DefinitionKey = "split-walk",
            DisplayName = "Split walk",
            InitialStageKey = "start",
            Lanes = [new AuthoredLane { Key = "applicant", DisplayName = "Applicant" }],
            Stages =
            [
                new AuthoredStage { StageKey = "start", DisplayName = "Start", LaneKey = "applicant" },
                new AuthoredStage { StageKey = "end", DisplayName = "End", Kind = StageKind.Confirmation, LaneKey = "applicant" }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "fan-out",
                    DisplayName = "Fan out",
                    Kind = GatewayKind.Split,
                    LaneKey = "applicant"
                }
            ],
            Transitions =
            [
                new AuthoredTransition { Source = "start", Target = "fan-out", Trigger = "continue" },
                new AuthoredTransition { Source = "fan-out", Target = "end", Trigger = "route" }
            ]
        };

        var result = _service.Simulate(workflow, actions: new[] { "continue" });

        result.CurrentStageKey.Should().Be("end",
            "simulating from the first stage should walk through the split gateway and arrive at the downstream stage");
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
        // A join gateway is a synchronisation point; the simulator must pause there with a
        // dedicated stop reason so the UI can surface the waiting copy.
        var workflow = new AuthoredWorkflow
        {
            DefinitionKey = "join-pause",
            DisplayName = "Join pause",
            InitialStageKey = "start",
            Lanes = [new AuthoredLane { Key = "applicant", DisplayName = "Applicant" }],
            Stages =
            [
                new AuthoredStage { StageKey = "start", DisplayName = "Start", LaneKey = "applicant" }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "join-here",
                    DisplayName = "Join here",
                    Kind = GatewayKind.Join,
                    LaneKey = "applicant",
                    WaitingInfo = new WaitingMetadata { Content = "Waiting for parallel branches." },
                    RequiredIncomingLanes = ["applicant"]
                }
            ],
            Transitions =
            [
                new AuthoredTransition { Source = "start", Target = "join-here", Trigger = "continue" }
            ]
        };

        var result = _service.Simulate(workflow, actions: new[] { "continue" });

        result.StopReason.Should().Be("waiting-gateway");
        result.CurrentStageKey.Should().Be("join-here");
        result.Completed.Should().BeFalse();
    }
}
