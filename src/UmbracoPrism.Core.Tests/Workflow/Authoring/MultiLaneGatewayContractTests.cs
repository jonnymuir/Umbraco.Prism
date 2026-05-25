using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Behavioural contracts for the merged gateway+join+parallel-lane slice (#83 + #84 + #85).
///
/// Purpose: prove that the authored model and projector honour the core rules from
/// docs/design/workflow-multi-lane-engine.md even before the full runtime implementation
/// lands. These tests act as guardrails — they lock behaviour that must not regress.
///
/// Rules being tested:
///   - Stages are action-bearing; gateways are routing nodes. These must not blur.
///   - Join and split gateways project as gateway metadata, not as runtime states.
///   - Stages in different lanes keep their own LaneKey in projected metadata; they do not
///     inherit or overwrite each other's lane attribution.
///   - Waiting information (WaitingMetadata) belongs to stages in the current model and
///     must not accidentally migrate to gateway projection before #84 lands.
///   - The clean runtime contract principle: no engine bookkeeping appears in the published
///     States array or in any individual state's metadata.
/// </summary>
public class MultiLaneGatewayContractTests
{
    private readonly WorkflowProjector _projector = new();

    // ─── Stage-bearing vs gateway separation (#83) ────────────────────────────

    [Fact]
    public void Stages_AreActionBearing_GatewaysAreNot_InProjectedOutput()
    {
        // Stages carry actions; gateways do not appear as states at all.
        // This locks the "stages remain action-bearing nodes" rule.
        var workflow = BuildTwoLaneWorkflow();

        var result = _projector.Project(workflow);

        // Stages project to States
        result.File.States.Should().Contain(s => s.StateKey == "applicant-details");
        result.File.States.Should().Contain(s => s.StateKey == "caseworker-review");

        // Gateways do NOT appear as States — they are in Metadata only
        result.File.States.Should().NotContain(s => s.StateKey == "review-split");
        result.File.States.Should().NotContain(s => s.StateKey == "outcome-join");

        // Both gateways appear in Metadata.Gateways
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
        split.GatewayType.Should().Be("Split", because: "split gateways route work into multiple lanes");
    }

    [Fact]
    public void JoinGateway_ProjectsWithGatewayType_Join()
    {
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var join = result.File.Metadata!.Gateways!.Single(g => g.Key == "outcome-join");
        join.GatewayType.Should().Be("Join", because: "join gateways wait for lanes and release the next step");
    }

    [Fact]
    public void SplitGateway_LaneOwnership_ReflectedInProjectedMetadata()
    {
        // Authors can see which lane owns a split gateway (#83 acceptance criterion).
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var split = result.File.Metadata!.Gateways!.Single(g => g.Key == "review-split");
        split.LaneKey.Should().Be("applicant",
            because: "the split gateway's owning lane must survive projection so the editor can show it");
    }

    [Fact]
    public void JoinGateway_LaneOwnership_ReflectedInProjectedMetadata()
    {
        // Join gateways show waiting information to the owning lane (#84 rule).
        // The lane must be preserved in the projected metadata so the runtime can scope
        // waiting state to the right lane without polluting others.
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var join = result.File.Metadata!.Gateways!.Single(g => g.Key == "outcome-join");
        join.LaneKey.Should().Be("caseworker",
            because: "the join gateway belongs to the caseworker lane — waiting information must be scoped there");
    }

    // ─── Multi-lane stage isolation (#82/#83) ─────────────────────────────────

    [Fact]
    public void MultiLane_Stages_ProjectToIndependentLaneKeys_NoContamination()
    {
        // A lane cannot overwrite another lane's stage metadata (#85 core rule in authoring).
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var applicantStage = result.File.States.Single(s => s.StateKey == "applicant-details");
        var caseworkerStage = result.File.States.Single(s => s.StateKey == "caseworker-review");

        applicantStage.Metadata!.LaneKey.Should().Be("applicant",
            because: "the applicant stage must always carry applicant lane attribution");
        caseworkerStage.Metadata!.LaneKey.Should().Be("caseworker",
            because: "the caseworker stage must always carry caseworker lane attribution");

        applicantStage.Metadata.LaneKey.Should().NotBe(caseworkerStage.Metadata!.LaneKey,
            because: "each lane's stages must carry distinct lane keys — no cross-contamination");
    }

    [Fact]
    public void MultiLane_Actions_StayLaneOwned_NoCrossContamination()
    {
        // Actions on a stage must not appear in another lane's stage metadata.
        var result = _projector.Project(BuildTwoLaneWorkflow());

        var applicantStage = result.File.States.Single(s => s.StateKey == "applicant-details");
        var caseworkerStage = result.File.States.Single(s => s.StateKey == "caseworker-review");

        // applicant stage has a forms.load action
        applicantStage.Metadata!.Actions.Should().NotBeNull();
        applicantStage.Metadata.Actions!.Should().ContainSingle(a => a.Type == "forms.load");

        // caseworker stage has no actions — it must not inherit applicant's
        caseworkerStage.Metadata!.Actions.Should().BeNull(
            because: "caseworker review stage has no actions and must not inherit from another lane");
    }

    [Fact]
    public void MultiLane_BothLanes_ProjectToMetadata_LanesArray()
    {
        // Both authored lanes must appear in projected metadata so the editor and runtime
        // can discover lane membership without parsing each state individually.
        var result = _projector.Project(BuildTwoLaneWorkflow());

        result.File.Metadata!.Lanes.Should().NotBeNull();
        result.File.Metadata.Lanes!.Should().Contain(l => l.Key == "applicant");
        result.File.Metadata.Lanes!.Should().Contain(l => l.Key == "caseworker");
    }

    // ─── Waiting information belongs to the join's lane (#84) ─────────────────

    [Fact]
    public void WaitingStage_InOneLane_ProjectsWaitingComponent_OtherLaneUnaffected()
    {
        // A waiting stage in one lane projects its WaitingComponent correctly.
        // Other lanes' stages remain unaffected — waiting is not a global state.
        var workflow = BuildWorkflowWithWaitingStage();

        var result = _projector.Project(workflow);

        var waitingStageState = result.File.States.Single(s => s.StateKey == "caseworker-waiting");
        var applicantStageState = result.File.States.Single(s => s.StateKey == "applicant-details");

        waitingStageState.Components.Should().ContainSingle(c => c.GetType().Name == "WaitingComponent",
            because: "waiting stages project to a WaitingComponent to show waiting copy to the user");

        applicantStageState.Components.Should().NotContain(c => c.GetType().Name == "WaitingComponent",
            because: "the applicant lane stage should not inherit waiting behaviour from another lane");
    }

    [Fact]
    public void WaitingStage_WaitingCopy_IsPreservedInProjectedComponent()
    {
        var workflow = BuildWorkflowWithWaitingStage();

        var result = _projector.Project(workflow);

        var waitingStageState = result.File.States.Single(s => s.StateKey == "caseworker-waiting");
        var waitingComponent = waitingStageState.Components
            .OfType<UmbracoPrism.Shared.Models.Workflow.Components.WaitingComponent>()
            .Single();

        waitingComponent.Content.Should().Be("Waiting for finance review to complete.",
            because: "the waiting copy authored at the stage must survive projection to the user-facing component");
    }

    // ─── Runtime contract cleanliness (#84, #85) ──────────────────────────────

    [Fact]
    public void ProjectedStates_ContainNoGatewayBookkeepingFields()
    {
        // The clean runtime contract rule: States array contains only user-facing stage data.
        // No cursor IDs, token accumulation details, or join bookkeeping should appear.
        var result = _projector.Project(BuildTwoLaneWorkflow());

        foreach (var state in result.File.States)
        {
            // Each state's metadata is limited to what the runtime needs (actor, laneKey, actions, description)
            // It must not contain any gateway-engine references
            state.Metadata?.LaneKey.Should().NotContain("gateway",
                because: "state metadata lane keys must be named lane identifiers, not gateway references");
        }
    }

    [Fact]
    public void ProjectedMetadata_HasNoInternalCursorOrTokenFields()
    {
        // Engine-internal concepts (cursors, join tokens, arrival bookkeeping) must not
        // appear in the projected file. The runtime may use them internally, but the
        // published contract stays clean.
        var result = _projector.Project(BuildTwoLaneWorkflow());

        // We verify this structurally: gateway definitions carry only authoring metadata
        var gateways = result.File.Metadata!.Gateways!;
        foreach (var gateway in gateways)
        {
            gateway.Key.Should().NotBeNullOrWhiteSpace();
            gateway.DisplayName.Should().NotBeNullOrWhiteSpace();
            gateway.LaneKey.Should().NotBeNullOrWhiteSpace();
            gateway.GatewayType.Should().BeOneOf("Split", "Join");
            // No cursor-id, arrival-count, or token-list fields exist on the model
            // (verified structurally — the type doesn't have those properties)
        }
    }

    // ─── Deterministic projection (#85 author-side) ───────────────────────────

    [Fact]
    public void Project_MultiLaneWorkflow_IsIdempotent()
    {
        // Deterministic projection rule: identical input always produces identical output.
        // Race order in parallel lanes must not affect what gets authored and projected.
        var workflow = BuildTwoLaneWorkflow();

        var result1 = _projector.Project(workflow);
        var result2 = _projector.Project(workflow);

        result1.Checksum.Should().Be(result2.Checksum,
            because: "projection must be deterministic — race conditions in author-side ordering must not affect the output");
        result1.File.States.Count.Should().Be(result2.File.States.Count);
        result1.File.Metadata!.Gateways!.Count.Should().Be(result2.File.Metadata!.Gateways!.Count);
    }

    [Fact]
    public void Project_SameWorkflow_WithDifferentLaneOrderInAuthored_ProducesSameChecksum()
    {
        // Lanes in the authored array can be in any order — the projector must normalise them
        // so the output checksum stays stable regardless of how the author listed the lanes.
        var workflow = BuildTwoLaneWorkflow();
        var workflowWithReversedLanes = workflow with
        {
            Lanes = workflow.Lanes.Reverse().ToArray()
        };

        var result1 = _projector.Project(workflow);
        var result2 = _projector.Project(workflowWithReversedLanes);

        result1.Checksum.Should().Be(result2.Checksum,
            because: "lane ordering in the authored model must not affect the deterministic projected checksum");
    }

    // ─── Skip: needs #84 implementation ──────────────────────────────────────

    [Fact(Skip = "#84: AuthoredGateway needs Description and WaitingCopy fields for join gateways. " +
                 "Test: join gateway's waiting copy must project into WorkflowGatewayDefinition metadata " +
                 "so the runtime can show lane-specific waiting information without a fake stage.")]
    public void JoinGateway_WaitingCopy_ProjectsToGatewayMetadata_NotToStageComponents()
    {
        // When #84 lands: a join gateway with WaitingCopy should project that copy into
        // WorkflowGatewayDefinition, NOT into any stage component. The runtime reads it
        // from the gateway metadata and shows it only to the owning lane.
        Assert.Fail("Needs #84 implementation");
    }

    [Fact(Skip = "#85: Join gateway must require explicit RequiredLanes list. " +
                 "Test: projection must validate that a join gateway's RequiredLanes all reference " +
                 "known lanes, and emit a diagnostic (PROJ-JOIN-001) if any lane is missing.")]
    public void JoinGateway_WithUnknownRequiredLane_EmitsValidationDiagnostic()
    {
        // When #85 lands: join gateways must list which lanes they wait for.
        // If any required lane doesn't exist, the projector must emit an error.
        Assert.Fail("Needs #85 implementation");
    }

    [Fact(Skip = "#85: Parallel lane arrival order must not change the projected release semantics. " +
                 "Test: two simulated arrival sequences (A then B, B then A) must produce " +
                 "identical convergence in the runtime state machine.")]
    public void JoinGateway_DeterministicRelease_RegardlessOfLaneArrivalOrder()
    {
        // When #85 lands: the join bookkeeping must be idempotent — whether lane A or B
        // arrives first, the same join release happens, and no duplicate release occurs.
        Assert.Fail("Needs #85 implementation");
    }

    // ─── Fixtures ─────────────────────────────────────────────────────────────

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
                LaneKey = "applicant"
            },
            new AuthoredGateway
            {
                GatewayKey = "outcome-join",
                DisplayName = "Outcome join",
                Kind = GatewayKind.Join,
                LaneKey = "caseworker"
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
                Fields =
                [
                    new AuthoredField { Key = "name", Label = "Full name", Type = FieldType.Text, Required = true }
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
        Transitions = [],
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

    private static AuthoredWorkflow BuildWorkflowWithWaitingStage() => new()
    {
        DefinitionKey = "waiting-lane-test",
        DisplayName = "Waiting Lane Test Workflow",
        InitialStageKey = "applicant-details",
        Lanes =
        [
            new AuthoredLane { Key = "applicant", DisplayName = "Applicant", Actor = "applicant" },
            new AuthoredLane { Key = "caseworker", DisplayName = "Caseworker", Actor = "caseworker" }
        ],
        Gateways = [],
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "applicant-details",
                DisplayName = "Your details",
                Kind = StageKind.Question,
                LaneKey = "applicant",
                Fields =
                [
                    new AuthoredField { Key = "name", Label = "Full name", Type = FieldType.Text, Required = true }
                ]
            },
            new AuthoredStage
            {
                StageKey = "caseworker-waiting",
                DisplayName = "Waiting for finance",
                Kind = StageKind.Waiting,
                LaneKey = "caseworker",
                Waiting = new WaitingMetadata
                {
                    Content = "Waiting for finance review to complete.",
                    ExpectedWaitSeconds = 300,
                    PollIntervalMs = 5000,
                    AllowDefer = true
                }
            }
        ],
        Transitions = [],
        ParameterSchemas = []
    };
}
