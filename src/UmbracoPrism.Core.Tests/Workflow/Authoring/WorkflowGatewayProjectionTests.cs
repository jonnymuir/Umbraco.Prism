using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Validates that the projector correctly emits gateway metadata including description,
/// waiting info for join gateways, required incoming lanes, and that gateway routes resolve
/// cleanly into runtime transitions.
/// </summary>
public class WorkflowGatewayProjectionTests
{
    private readonly WorkflowProjector _projector = new();

    // ─── Route graph: stage owns gateway, gateway routes to stage/gateway ──────

    [Fact]
    public void Project_GatewayRoutes_AreEmittedAsRuntimeTransitions()
    {
        var workflow = BuildTwoLaneWorkflow();

        var result = _projector.Project(workflow);

        result.HasErrors.Should().BeFalse();

        // Parallel-fork Split: entry edge into the gateway, then one auto-fan-out edge per branch.
        // The engine reads `ToState == splitKey` to fire HandleSplitGatewayAdvance, which fans
        // every outgoing edge of the gateway into one cursor per branch.
        result.File.Transitions.Should().Contain(t =>
            t.FromState == "submit" && t.ToState == "split-review" && t.Action == "submit",
            because: "the user's submit trigger must land on the split gateway so the engine can fan out");
        result.File.Transitions.Should().Contain(t =>
            t.FromState == "split-review" && t.ToState == "finance-review" && t.Action == "split-auto",
            because: "the split gateway must own the auto-fan-out edge into each branch stage");
        result.File.Transitions.Should().Contain(t =>
            t.FromState == "split-review" && t.ToState == "planning-review" && t.Action == "split-auto");

        // Join routes that target the join gateway from a wrapper single-route Split stay flat
        // (the engine reads `ToState == joinKey` to fire HandleJoinGatewayAdvance directly).
        result.File.Transitions.Should().Contain(t =>
            t.FromState == "finance-review" && t.ToState == "join-reviews" && t.Action == "approve");
        result.File.Transitions.Should().Contain(t =>
            t.FromState == "planning-review" && t.ToState == "join-reviews" && t.Action == "approve");

        // Join outgoing edge: the release path out of the join into the next stage.
        result.File.Transitions.Should().Contain(t =>
            t.FromState == "join-reviews" && t.ToState == "decision" && t.Action == "release",
            because: "the join gateway must own its release edge so the engine can advance after all required lanes arrive");
    }

    // ─── Split gateway emission ───────────────────────────────────────────────

    [Fact]
    public void Project_SplitGateway_EmittedIntoMetadataGateways()
    {
        var workflow = BuildTwoLaneWorkflow();

        var result = _projector.Project(workflow);

        var gateways = result.File.Metadata?.Gateways;
        gateways.Should().NotBeNull();
        gateways!.Should().Contain(g => g.Key == "split-review" && g.GatewayType == "Split");
    }

    [Fact]
    public void Project_SplitGateway_PreservesDescription()
    {
        var workflow = BuildTwoLaneWorkflow();

        var result = _projector.Project(workflow);

        var gw = result.File.Metadata!.Gateways!.First(g => g.Key == "split-review");
        gw.Description.Should().Be("Branch into finance and planning lanes.");
    }

    // ─── Join gateway emission ────────────────────────────────────────────────

    [Fact]
    public void Project_JoinGateway_EmittedWithWaitingContent()
    {
        var workflow = BuildTwoLaneWorkflow();

        var result = _projector.Project(workflow);

        var gw = result.File.Metadata!.Gateways!.First(g => g.Key == "join-reviews");
        gw.GatewayType.Should().Be("Join");
        gw.WaitingContent.Should().Be("Waiting for all reviews to complete.");
        gw.WaitingPollIntervalMs.Should().Be(5000);
        gw.WaitingExpectedSeconds.Should().Be(60);
    }

    [Fact]
    public void Project_JoinGateway_EmitsRequiredIncomingLanesInSortedOrder()
    {
        var workflow = BuildTwoLaneWorkflow();

        var result = _projector.Project(workflow);

        var gw = result.File.Metadata!.Gateways!.First(g => g.Key == "join-reviews");
        gw.RequiredIncomingLanes.Should().NotBeNull();
        gw.RequiredIncomingLanes!.Should().ContainInOrder(
            new[] { "finance", "planning" },
            "required incoming lanes must be emitted in sorted order");
    }

    // ─── Schema validation: join gateway rules ────────────────────────────────

    [Fact]
    public void Project_JoinGatewayWithoutWaitingInfo_ReportsProj137()
    {
        var workflow = BuildTwoLaneWorkflow();
        var altered = workflow with
        {
            Gateways =
            [
                workflow.Gateways[0],
                workflow.Gateways[1],
                workflow.Gateways[2],
                new AuthoredGateway
                {
                    GatewayKey = "join-reviews",
                    DisplayName = "All reviews done",
                    Kind = GatewayKind.Join,
                    LaneKey = "applicant",
                    RequiredIncomingLanes = ["finance", "planning"],
                    Routes = [new AuthoredRoute { Id = "release", Target = "decision", Trigger = "release" }]
                    // WaitingInfo intentionally missing
                }
            ]
        };

        var result = _projector.Project(altered);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ137");
    }

    [Fact]
    public void Project_JoinGatewayWithoutRequiredIncomingLanes_ReportsProj138()
    {
        var workflow = BuildTwoLaneWorkflow();
        var altered = workflow with
        {
            Gateways =
            [
                workflow.Gateways[0],
                workflow.Gateways[1],
                workflow.Gateways[2],
                new AuthoredGateway
                {
                    GatewayKey = "join-reviews",
                    DisplayName = "All reviews done",
                    Kind = GatewayKind.Join,
                    LaneKey = "applicant",
                    WaitingInfo = new WaitingMetadata { Content = "Waiting.", ExpectedWaitSeconds = 30, PollIntervalMs = 3000 },
                    Routes = [new AuthoredRoute { Id = "release", Target = "decision", Trigger = "release" }]
                    // RequiredIncomingLanes intentionally empty
                }
            ]
        };

        var result = _projector.Project(altered);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ138");
    }

    [Fact]
    public void Project_JoinGatewayWithUnknownRequiredLane_ReportsProj139()
    {
        var workflow = BuildTwoLaneWorkflow();
        var altered = workflow with
        {
            Gateways =
            [
                workflow.Gateways[0],
                workflow.Gateways[1],
                workflow.Gateways[2],
                new AuthoredGateway
                {
                    GatewayKey = "join-reviews",
                    DisplayName = "All reviews done",
                    Kind = GatewayKind.Join,
                    LaneKey = "applicant",
                    WaitingInfo = new WaitingMetadata { Content = "Waiting.", ExpectedWaitSeconds = 30, PollIntervalMs = 3000 },
                    RequiredIncomingLanes = ["finance", "does-not-exist"],
                    Routes = [new AuthoredRoute { Id = "release", Target = "decision", Trigger = "release" }]
                }
            ]
        };

        var result = _projector.Project(altered);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d =>
            d.Code == "PROJ139" && d.Message.Contains("does-not-exist"));
    }

    // ─── Workflow fixture helper ──────────────────────────────────────────────

    private static AuthoredWorkflow BuildTwoLaneWorkflow() => new()
    {
        Id = new Guid("aaaabbbb-cccc-dddd-eeee-000000000083"),
        DefinitionKey = "gateway-test",
        DisplayName = "Gateway Test Workflow",
        Version = 1,
        InitialStageKey = "submit",
        InstancePolicy = "single",
        Lanes =
        [
            new AuthoredLane { Key = "applicant", DisplayName = "Applicant", Actor = "applicant" },
            new AuthoredLane { Key = "finance", DisplayName = "Finance", Actor = "finance-officer" },
            new AuthoredLane { Key = "planning", DisplayName = "Planning", Actor = "planning-officer" }
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "split-review",
                DisplayName = "Start parallel reviews",
                Description = "Branch into finance and planning lanes.",
                Kind = GatewayKind.Split,
                LaneKey = "applicant",
                Source = "submit",
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
                LaneKey = "finance",
                Source = "finance-review",
                Routes = [new AuthoredRoute { Id = "approve", Target = "join-reviews", Trigger = "approve" }]
            },
            new AuthoredGateway
            {
                GatewayKey = "planning-out",
                DisplayName = "Planning routing",
                Kind = GatewayKind.Split,
                LaneKey = "planning",
                Source = "planning-review",
                Routes = [new AuthoredRoute { Id = "approve", Target = "join-reviews", Trigger = "approve" }]
            },
            new AuthoredGateway
            {
                GatewayKey = "join-reviews",
                DisplayName = "All reviews done",
                Kind = GatewayKind.Join,
                LaneKey = "applicant",
                RequiredIncomingLanes = ["planning", "finance"],
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
            new AuthoredStage
            {
                StageKey = "submit",
                DisplayName = "Submit application",
                Kind = StageKind.Question,
                LaneKey = "applicant"
            },
            new AuthoredStage
            {
                StageKey = "finance-review",
                DisplayName = "Finance review",
                Kind = StageKind.Question,
                LaneKey = "finance"
            },
            new AuthoredStage
            {
                StageKey = "planning-review",
                DisplayName = "Planning review",
                Kind = StageKind.Question,
                LaneKey = "planning"
            },
            new AuthoredStage
            {
                StageKey = "decision",
                DisplayName = "Final decision",
                Kind = StageKind.Confirmation,
                LaneKey = "applicant"
            }
        ]
    };
}
