using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowEditor.Authoring;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// End-to-end behavioural contract: an authored workflow that uses a Split gateway,
/// a Join gateway, and a wait MUST exercise the runtime engine's gateway code when
/// projected. These tests deliberately go through the real <see cref="WorkflowProjector"/>
/// and feed the projected definition to the real <see cref="WorkflowRuntimeEngine"/> —
/// no hand-rolled <c>WorkflowDefinitionFile</c> shortcut. If the projector ever stops
/// emitting gateway keys as graph endpoints, the engine quietly falls back to plain
/// stage→stage transitions and these tests will catch it.
/// </summary>
public class ProjectorEngineGatewayIntegrationTests
{
    private const string Tenant = "tenant-1";
    private const string User = "user-1";

    [Fact]
    public void Split_AuthoredWorkflow_FansOutToOneCursorPerBranch_WhenProjectedAndRun()
    {
        var engine = ProjectAndWireEngine(BuildSplitJoinWorkflow());

        var initial = engine.GetCurrent("gateway-integration", Tenant, User, action: "start-new");
        initial.ResponseState.Should().Be("render");

        var afterSubmit = engine.Advance(
            initial.InstanceId, Tenant, User, "submit", initial.StateVersion, null);

        afterSubmit.ResponseState.Should().BeOneOf("render", "defer",
            "after a split fan-out the engine renders the first active stage or defers");

        var instance = engine.GetAllInstances().Single(i => i.InstanceId == afterSubmit.InstanceId);
        instance.Cursors.Should().HaveCount(2,
            "an authored Split gateway must produce one cursor per outgoing branch at runtime");
        instance.Cursors.Should().Contain(c => c.CurrentNodeKey == "finance-review");
        instance.Cursors.Should().Contain(c => c.CurrentNodeKey == "planning-review");
    }

    [Fact]
    public void Join_AuthoredWorkflow_WaitsUntilAllRequiredLanesArrive_WhenProjectedAndRun()
    {
        var engine = ProjectAndWireEngine(BuildSplitJoinWorkflow());

        var initial = engine.GetCurrent("gateway-integration", Tenant, User, action: "start-new");
        var afterSubmit = engine.Advance(
            initial.InstanceId, Tenant, User, "submit", initial.StateVersion, null);

        // First lane arrives at the join — should wait, not fall through to the next stage.
        var afterFirstApprove = engine.Advance(
            afterSubmit.InstanceId, Tenant, User, "approve", afterSubmit.StateVersion, null);

        afterFirstApprove.ResponseState.Should().Be("defer",
            "a join gateway must wait until every required incoming lane has arrived");

        // Second lane arrives — join releases to the next stage.
        var afterSecondApprove = engine.Advance(
            afterFirstApprove.InstanceId, Tenant, User, "approve", afterFirstApprove.StateVersion, null);

        afterSecondApprove.ResponseState.Should().Be("complete",
            "once every required lane has arrived the join releases and the workflow reaches the confirmation stage");
    }

    [Fact]
    public void Join_AuthoredWorkflow_SurfacesWaitingCopyFromTheGateway_NotAFakeStage()
    {
        var engine = ProjectAndWireEngine(BuildSplitJoinWorkflow());

        var initial = engine.GetCurrent("gateway-integration", Tenant, User, action: "start-new");
        var afterSubmit = engine.Advance(
            initial.InstanceId, Tenant, User, "submit", initial.StateVersion, null);
        var afterFirstApprove = engine.Advance(
            afterSubmit.InstanceId, Tenant, User, "approve", afterSubmit.StateVersion, null);

        afterFirstApprove.ResponseState.Should().Be("defer");
        var waitingComponent = afterFirstApprove.Render!.Components.FirstOrDefault(c => c.Type == "waiting");
        waitingComponent.Should().NotBeNull(
            "the join gateway's waiting copy must be surfaced as a waiting component while siblings are outstanding");
        waitingComponent!.Content.Should().Contain("Waiting for all reviews to complete.",
            "waiting copy must come from the authored join gateway, not from a placeholder stage");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static WorkflowRuntimeEngine ProjectAndWireEngine(AuthoredWorkflow authored)
    {
        var projection = new WorkflowProjector().Project(authored);
        projection.HasErrors.Should().BeFalse(
            "the test fixture authored workflow must project cleanly before behavioural assertions run");

        var sanitizer = new Mock<IWorkflowContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(x => x);

        return new TestableWorkflowRuntimeEngine(
            NullLogger<TestableWorkflowRuntimeEngine>.Instance, sanitizer.Object, projection.File);
    }

    private static AuthoredWorkflow BuildSplitJoinWorkflow() => new()
    {
        Id = new Guid("aaaabbbb-cccc-dddd-eeee-000000000099"),
        DefinitionKey = "gateway-integration",
        DisplayName = "Gateway Integration Workflow",
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
                RequiredIncomingLanes = ["finance", "planning"],
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

    private sealed class TestableWorkflowRuntimeEngine : WorkflowRuntimeEngine
    {
        public TestableWorkflowRuntimeEngine(
            ILogger<TestableWorkflowRuntimeEngine> logger,
            IWorkflowContentSanitizer sanitizer,
            WorkflowDefinitionFile definition)
            : base(logger, new SingleDefinitionStore(definition), sanitizer)
        {
        }
    }

    private sealed class SingleDefinitionStore : IWorkflowDefinitionStore
    {
        private readonly WorkflowDefinitionFile _definition;

        public SingleDefinitionStore(WorkflowDefinitionFile definition)
        {
            _definition = definition;
        }

        public IReadOnlyDictionary<string, WorkflowDefinitionFile> LoadDefinitions(ILogger logger) =>
            new Dictionary<string, WorkflowDefinitionFile>
            {
                [_definition.DefinitionKey] = _definition
            };
    }
}
