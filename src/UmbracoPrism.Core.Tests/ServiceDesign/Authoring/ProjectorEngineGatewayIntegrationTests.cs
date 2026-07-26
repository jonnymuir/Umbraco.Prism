using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

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

        // Check join-wait state before the second approve mutates the instance.
        var instanceAfterFirst = engine.GetAllInstances().Single(i => i.InstanceId == afterFirstApprove.InstanceId);
        instanceAfterFirst.Cursors.Should().Contain(c => c.IsAtGateway && c.CurrentNodeKey == "join-reviews",
            "the first approved cursor should be held at the join gateway");
        instanceAfterFirst.JoinArrivals.Should().ContainKey("join-reviews",
            "the join gateway should have recorded the first arrival");

        // After the second approval both required queues have arrived and the join releases.
        var afterSecondApprove = engine.Advance(afterFirstApprove.InstanceId, Tenant, User, "approve", afterFirstApprove.StateVersion, null);
        afterSecondApprove.ResponseState.Should().Be("complete");
    }

    private static ProcessManagerEngine ProjectAndWireEngine(AuthoredServiceBlueprint authored)
    {
        var projection = new ServiceBlueprintProjector().Project(authored);
        projection.HasErrors.Should().BeFalse();

        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(service => service.Sanitize(It.IsAny<string>())).Returns<string>(value => value);

        return new TestableWorkflowRuntimeEngine(
            NullLogger<TestableWorkflowRuntimeEngine>.Instance,
            sanitizer.Object,
            projection.File);
    }

    private static AuthoredServiceBlueprint BuildSplitJoinWorkflow() => new()
    {
        Id = new Guid("aaaabbbb-cccc-dddd-eeee-000000000099"),
        DefinitionKey = "gateway-integration",
        DisplayName = "Gateway Integration Workflow",
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
            new AuthoredTouchpoint
            {
                StageKey = "submit",
                DisplayName = "Submit application",
                Kind = TouchpointKind.Question,
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "submit-gateway", Target = "split-review", Trigger = "submit" }]
            },
            new AuthoredTouchpoint
            {
                StageKey = "finance-review",
                DisplayName = "Finance review",
                Kind = TouchpointKind.Question,
                QueueKey = "finance",
                Routes = [new AuthoredRoute { Id = "finance-route", Target = "finance-out", Trigger = "approve" }]
            },
            new AuthoredTouchpoint
            {
                StageKey = "planning-review",
                DisplayName = "Planning review",
                Kind = TouchpointKind.Question,
                QueueKey = "planning",
                Routes = [new AuthoredRoute { Id = "planning-route", Target = "planning-out", Trigger = "approve" }]
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

    private sealed class TestableWorkflowRuntimeEngine : ProcessManagerEngine
    {
        public TestableWorkflowRuntimeEngine(
            ILogger<TestableWorkflowRuntimeEngine> logger,
            IServiceContentSanitizer sanitizer,
            ServiceBlueprint definition)
            : base(logger, new SingleDefinitionStore(definition), sanitizer)
        {
        }
    }

    private sealed class SingleDefinitionStore : IServiceBlueprintStore
    {
        private readonly ServiceBlueprint _definition;

        public SingleDefinitionStore(ServiceBlueprint definition)
        {
            _definition = definition;
        }

        public IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger) =>
            new Dictionary<string, ServiceBlueprint>
            {
                [_definition.DefinitionKey] = _definition
            };
    }
}
