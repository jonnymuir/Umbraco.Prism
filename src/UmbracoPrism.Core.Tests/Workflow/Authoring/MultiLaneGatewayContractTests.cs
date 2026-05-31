using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Behavioural contracts for the corrected gateway-first workflow model.
///
/// Purpose: prove that the authored model and projector keep stages as work nodes,
/// gateways as routing nodes, and join-gateway waiting information on the gateway itself.
/// These tests act as guardrails while the editor and runtime finish converging.
/// </summary>
public class MultiLaneGatewayContractTests
{
    private readonly WorkflowProjector _projector = new();

    [Fact]
    public void Stages_AreActionBearing_GatewaysAreNot_InProjectedOutput()
    {
        var workflow = BuildTwoLaneWorkflow();

        var result = _projector.Project(workflow);

        result.File.States.Should().Contain(s => s.StateKey == "applicant-details");
        result.File.States.Should().Contain(s => s.StateKey == "caseworker-review");
        result.File.States.Should().NotContain(s => s.StateKey == "review-split");
        result.File.States.Should().NotContain(s => s.StateKey == "outcome-join");
        result.File.Metadata.Should().NotBeNull();
        result.File.Metadata!.Gateways.Should().NotBeNull();
        result.File.Metadata.Gateways!.Should().Contain(g => g.Key == "review-split");
        result.File.Metadata.Gateways!.Should().Contain(g => g.Key == "outcome-join");
    }

    [Fact]
    public void SplitGateway_ProjectsWithGatewayType_Split()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var split = result.File.Metadata!.Gateways!.Single(g => g.Key == "review-split");
        split.GatewayType.Should().Be("Split", because: "split gateways route work into more than one lane");
    }

    [Fact]
    public void JoinGateway_ProjectsWithGatewayType_Join()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var join = result.File.Metadata!.Gateways!.Single(g => g.Key == "outcome-join");
        join.GatewayType.Should().Be("Join", because: "join gateways wait for the required lanes and then release the next step");
    }

    [Fact]
    public void SplitGateway_LaneOwnership_ReflectedInProjectedMetadata()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var split = result.File.Metadata!.Gateways!.Single(g => g.Key == "review-split");
        split.LaneKey.Should().Be("applicant",
            because: "authors need to see which lane owns the branch point");
    }

    [Fact]
    public void JoinGateway_LaneOwnership_ReflectedInProjectedMetadata()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var join = result.File.Metadata!.Gateways!.Single(g => g.Key == "outcome-join");
        join.LaneKey.Should().Be("caseworker",
            because: "the waiting story belongs to the lane that owns the join gateway");
    }

    [Fact]
    public void MultiLane_Stages_ProjectToIndependentLaneKeys_NoContamination()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var applicantStage = result.File.States.Single(s => s.StateKey == "applicant-details");
        var caseworkerStage = result.File.States.Single(s => s.StateKey == "caseworker-review");

        applicantStage.Metadata!.LaneKey.Should().Be("applicant",
            because: "the applicant stage must always carry applicant lane attribution");
        caseworkerStage.Metadata!.LaneKey.Should().Be("caseworker",
            because: "the caseworker stage must always carry caseworker lane attribution");
        applicantStage.Metadata.LaneKey.Should().NotBe(caseworkerStage.Metadata!.LaneKey,
            because: "one lane must not overwrite another lane's work");
    }

    [Fact]
    public void MultiLane_Actions_StayLaneOwned_NoCrossContamination()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var applicantStage = result.File.States.Single(s => s.StateKey == "applicant-details");
        var caseworkerStage = result.File.States.Single(s => s.StateKey == "caseworker-review");

        applicantStage.Metadata!.Actions.Should().NotBeNull();
        applicantStage.Metadata.Actions!.Should().ContainSingle(a => a.Type == "forms.load");
        caseworkerStage.Metadata!.Actions.Should().BeNull(
            because: "the caseworker review stage has no actions and must not inherit another lane's actions");
    }

    [Fact]
    public void MultiLane_BothLanes_ProjectToMetadata_LanesArray()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        result.File.Metadata!.Lanes.Should().NotBeNull();
        result.File.Metadata.Lanes!.Should().Contain(l => l.Key == "applicant");
        result.File.Metadata.Lanes!.Should().Contain(l => l.Key == "caseworker");
    }

    [Fact]
    public void JoinGateway_WaitingInfo_ProjectsToGatewayMetadata()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var join = result.File.Metadata!.Gateways!.Single(g => g.Key == "outcome-join");
        join.WaitingContent.Should().Be("Waiting for the caseworker decision before the workflow can continue.",
            because: "waiting copy belongs on the join gateway");
        join.WaitingExpectedSeconds.Should().Be(300);
        join.WaitingPollIntervalMs.Should().Be(5000);
        join.RequiredIncomingLanes.Should().ContainInOrder(["applicant", "caseworker"]);
    }

    [Fact]
    public void JoinGateway_WaitingInfo_DoesNotCreateAFakeWaitingStage()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        result.File.States.Should().NotContain(s => s.StateKey == "caseworker-waiting");
        result.File.States
            .SelectMany(state => state.Components)
            .Should().NotContain(component => component.GetType().Name == "WaitingComponent",
                because: "waiting belongs on the join gateway, not on a placeholder stage");
    }

    [Fact]
    public void ProjectedStates_ContainNoGatewayBookkeepingFields()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        foreach (var state in result.File.States)
        {
            state.Metadata?.LaneKey.Should().NotContain("gateway",
                because: "projected states should stay clean and product-facing");
        }
    }

    [Fact]
    public void ProjectedMetadata_HasNoInternalCursorOrTokenFields()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var gateways = result.File.Metadata!.Gateways!;
        foreach (var gateway in gateways)
        {
            gateway.Key.Should().NotBeNullOrWhiteSpace();
            gateway.DisplayName.Should().NotBeNullOrWhiteSpace();
            gateway.LaneKey.Should().NotBeNullOrWhiteSpace();
            gateway.GatewayType.Should().BeOneOf("Split", "Join");
        }
    }

    [Fact]
    public void Project_MultiLaneWorkflow_IsIdempotent()
    {
        var workflow = BuildTwoLaneWorkflow();

        var result1 = _projector.Project(workflow);
        var result2 = _projector.Project(workflow);

        result1.Checksum.Should().Be(result2.Checksum,
            because: "projection must stay deterministic even when gateways introduce multiple lane paths");
        result1.File.States.Count.Should().Be(result2.File.States.Count);
        result1.File.Metadata!.Gateways!.Count.Should().Be(result2.File.Metadata!.Gateways!.Count);
    }

    [Fact]
    public void Project_SameWorkflow_WithDifferentLaneOrderInAuthored_ProducesSameChecksum()
    {
        var workflow = BuildTwoLaneWorkflow();
        var workflowWithReversedLanes = workflow with
        {
            Lanes = workflow.Lanes.Reverse().ToArray()
        };

        var result1 = _projector.Project(workflow);
        var result2 = _projector.Project(workflowWithReversedLanes);

        result1.Checksum.Should().Be(result2.Checksum,
            because: "lane ordering in the authored model must not change the projected contract");
    }

    private static AuthoredWorkflow BuildTwoLaneWorkflow() => new()
    {
        DefinitionKey = "multi-lane-test",
        DisplayName = "Multi-Lane Test Workflow",
        InitialStageKey = "applicant-details",
        Lanes =
        [
            new AuthoredLane
            {
                Key = "applicant",
                DisplayName = "Applicant",
                Actor = "applicant"
            },
            new AuthoredLane
            {
                Key = "caseworker",
                DisplayName = "Caseworker",
                Actor = "caseworker"
            }
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "review-split",
                DisplayName = "Review split",
                Kind = GatewayKind.Split,
                LaneKey = "applicant",
                Source = "applicant-details",
                Routes =
                [
                    new AuthoredRoute { Id = "to-join", Target = "outcome-join", Trigger = "submit" }
                ]
            },
            new AuthoredGateway
            {
                GatewayKey = "outcome-join",
                DisplayName = "Outcome join",
                Kind = GatewayKind.Join,
                LaneKey = "caseworker",
                WaitingInfo = new WaitingMetadata
                {
                    Content = "Waiting for the caseworker decision before the workflow can continue.",
                    ExpectedWaitSeconds = 300,
                    PollIntervalMs = 5000,
                    AllowDefer = false
                },
                RequiredIncomingLanes = ["applicant", "caseworker"],
                Routes =
                [
                    new AuthoredRoute { Id = "release", Target = "caseworker-review", Trigger = "release-review" }
                ]
            }
        ],
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "applicant-details",
                DisplayName = "Your details",
                Kind = StageKind.Question,
                LaneKey = "applicant",
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
            new AuthoredStage
            {
                StageKey = "caseworker-review",
                DisplayName = "Caseworker review",
                Kind = StageKind.Question,
                LaneKey = "caseworker"
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
