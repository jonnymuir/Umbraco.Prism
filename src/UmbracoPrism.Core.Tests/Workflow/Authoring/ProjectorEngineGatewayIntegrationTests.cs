using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowEditor.Authoring;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class ProjectorEngineGatewayIntegrationTests
{
    private const string Tenant = "tenant-1";
    private const string User = "user-1";

    [Fact]
    public void Split_AuthoredWorkflow_FansOutToOneCursorPerBranch_WhenProjectedAndRun()
    {
        var engine = ProjectAndWireEngine(BuildSplitJoinWorkflow());

        var initial = engine.GetCurrent("gateway-integration", Tenant, User, action: "start-new");
        var afterSubmit = engine.Advance(initial.InstanceId, Tenant, User, "submit", initial.StateVersion, null);

        var instance = engine.GetAllInstances().Single(i => i.InstanceId == afterSubmit.InstanceId);
        instance.Cursors.Should().HaveCount(2);
        instance.Cursors.Should().Contain(cursor => cursor.CurrentNodeKey == "finance-review");
        instance.Cursors.Should().Contain(cursor => cursor.CurrentNodeKey == "planning-review");
    }

    [Fact]
    public void Join_AuthoredWorkflow_WaitsUntilAllRequiredQueuesArrive_WhenProjectedAndRun()
    {
        var engine = ProjectAndWireEngine(BuildSplitJoinWorkflow());

        var initial = engine.GetCurrent("gateway-integration", Tenant, User, action: "start-new");
        var afterSubmit = engine.Advance(initial.InstanceId, Tenant, User, "submit", initial.StateVersion, null);
        var afterFirstApprove = engine.Advance(afterSubmit.InstanceId, Tenant, User, "approve", afterSubmit.StateVersion, null);
        var afterSecondApprove = engine.Advance(afterFirstApprove.InstanceId, Tenant, User, "approve", afterFirstApprove.StateVersion, null);

        afterFirstApprove.ResponseState.Should().Be("defer");
        afterSecondApprove.ResponseState.Should().Be("complete");
    }

    private static WorkflowRuntimeEngine ProjectAndWireEngine(AuthoredWorkflow authored)
    {
        var projection = new WorkflowProjector().Project(authored);
        projection.HasErrors.Should().BeFalse();

        var sanitizer = new Mock<IWorkflowContentSanitizer>();
        sanitizer.Setup(service => service.Sanitize(It.IsAny<string>())).Returns<string>(value => value);

        return new TestableWorkflowRuntimeEngine(
            NullLogger<TestableWorkflowRuntimeEngine>.Instance,
            sanitizer.Object,
            projection.File);
    }

    private static AuthoredWorkflow BuildSplitJoinWorkflow() => new()
    {
        Id = new Guid("aaaabbbb-cccc-dddd-eeee-000000000099"),
        DefinitionKey = "gateway-integration",
        DisplayName = "Gateway Integration Workflow",
        Version = 1,
        InitialStageKey = "submit",
        InstancePolicy = "single",
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
                RequiredIncomingQueues = ["finance", "planning"],
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
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "submit-gateway", Target = "split-review", Trigger = "submit" }]
            },
            new AuthoredStage
            {
                StageKey = "finance-review",
                DisplayName = "Finance review",
                Kind = StageKind.Question,
                QueueKey = "finance",
                Routes = [new AuthoredRoute { Id = "finance-route", Target = "finance-out", Trigger = "approve" }]
            },
            new AuthoredStage
            {
                StageKey = "planning-review",
                DisplayName = "Planning review",
                Kind = StageKind.Question,
                QueueKey = "planning",
                Routes = [new AuthoredRoute { Id = "planning-route", Target = "planning-out", Trigger = "approve" }]
            },
            new AuthoredStage
            {
                StageKey = "decision",
                DisplayName = "Final decision",
                Kind = StageKind.Confirmation,
                QueueKey = "applicant"
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
