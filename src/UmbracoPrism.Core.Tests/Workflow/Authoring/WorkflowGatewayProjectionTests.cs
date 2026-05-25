using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Validates that the projector correctly emits gateway metadata including description,
/// waiting info for join gateways, required incoming lanes, and that transitions can
/// target either stage keys or gateway keys without raising false-positive warnings.
/// </summary>
public class WorkflowGatewayProjectionTests
{
    private readonly WorkflowProjector _projector = new();

    // ─── Transition graph: stage → gateway, gateway → stage ──────────────────

    [Fact]
    public void Project_TransitionToGateway_DoesNotEmitProj004Warning()
    {
        var workflow = BuildTwoLaneWorkflow();

        var result = _projector.Project(workflow);

        result.HasErrors.Should().BeFalse("gateway-targeted transitions are valid graph edges");
        result.Diagnostics.Should().NotContain(d => d.Code == "PROJ004",
            "transitions whose source or target is a defined gateway key must not warn");
    }

    [Fact]
    public void Project_TransitionFromGateway_DoesNotEmitProj004Warning()
    {
        var workflow = BuildTwoLaneWorkflow();

        var result = _projector.Project(workflow);

        result.Diagnostics.Should().NotContain(d => d.Code == "PROJ004");
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
        gw.RequiredIncomingLanes!.Should().ContainInOrder("finance", "planning",
            "required incoming lanes must be emitted in sorted order");
    }

    // ─── Schema validation: join gateway rules ────────────────────────────────

    [Fact]
    public void Project_JoinGatewayWithoutWaitingInfo_ReportsProj137()
    {
        var workflow = BuildTwoLaneWorkflow() with
        {
            Gateways =
            [
                BuildTwoLaneWorkflow().Gateways[0],
                new AuthoredGateway
                {
                    GatewayKey = "join-reviews",
                    DisplayName = "All reviews done",
                    Kind = GatewayKind.Join,
                    LaneKey = "applicant",
                    RequiredIncomingLanes = ["finance", "planning"]
                    // WaitingInfo intentionally missing
                }
            ]
        };

        var result = _projector.Project(workflow);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ137");
    }

    [Fact]
    public void Project_JoinGatewayWithoutRequiredIncomingLanes_ReportsProj138()
    {
        var workflow = BuildTwoLaneWorkflow() with
        {
            Gateways =
            [
                BuildTwoLaneWorkflow().Gateways[0],
                new AuthoredGateway
                {
                    GatewayKey = "join-reviews",
                    DisplayName = "All reviews done",
                    Kind = GatewayKind.Join,
                    LaneKey = "applicant",
                    WaitingInfo = new WaitingMetadata { Content = "Waiting.", ExpectedWaitSeconds = 30, PollIntervalMs = 3000 }
                    // RequiredIncomingLanes intentionally empty
                }
            ]
        };

        var result = _projector.Project(workflow);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ138");
    }

    [Fact]
    public void Project_JoinGatewayWithUnknownRequiredLane_ReportsProj139()
    {
        var workflow = BuildTwoLaneWorkflow() with
        {
            Gateways =
            [
                BuildTwoLaneWorkflow().Gateways[0],
                new AuthoredGateway
                {
                    GatewayKey = "join-reviews",
                    DisplayName = "All reviews done",
                    Kind = GatewayKind.Join,
                    LaneKey = "applicant",
                    WaitingInfo = new WaitingMetadata { Content = "Waiting.", ExpectedWaitSeconds = 30, PollIntervalMs = 3000 },
                    RequiredIncomingLanes = ["finance", "does-not-exist"]
                }
            ]
        };

        var result = _projector.Project(workflow);

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
                LaneKey = "applicant"
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
                }
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
        ],
        Transitions =
        [
            new AuthoredTransition { FromStage = "submit", ToStage = "split-review", Action = "submit" },
            new AuthoredTransition { FromStage = "split-review", ToStage = "finance-review", Action = "split-auto" },
            new AuthoredTransition { FromStage = "split-review", ToStage = "planning-review", Action = "split-auto" },
            new AuthoredTransition { FromStage = "finance-review", ToStage = "join-reviews", Action = "approve" },
            new AuthoredTransition { FromStage = "planning-review", ToStage = "join-reviews", Action = "approve" },
            new AuthoredTransition { FromStage = "join-reviews", ToStage = "decision", Action = "release" }
        ]
    };
}
