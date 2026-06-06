using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.MockBusinessApp.Services.Publishing;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowEditor.Authoring;
using UmbracoPrism.Core.Tests.Workflow.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Publishing;

public sealed class WorkflowPublishServiceTests : IDisposable
{
    private static readonly string FixturesPath = Path.Combine(
        AppContext.BaseDirectory,
        "Workflow",
        "Authoring",
        "Fixtures");

    private readonly string _publishedPath = Path.Combine(
        AppContext.BaseDirectory,
        "Workflow",
        "Authoring",
        "Published",
        Guid.NewGuid().ToString("N"));

    private readonly WorkflowPublishService _sut;

    public WorkflowPublishServiceTests()
    {
        Directory.CreateDirectory(_publishedPath);
        _sut = new WorkflowPublishService(
            new WorkflowProjector(),
            new FilesystemPublishedWorkflowStore(_publishedPath));
    }

    [Fact]
    public async Task PublishAsync_PlanningFixture_ProjectsStagesTransitionsAndActions()
    {
        var workflow = await LoadPlanningFixture();

        var result = await _sut.PublishAsync(workflow);

        result.HasErrors.Should().BeFalse();
        result.RoundTripVerified.Should().BeTrue();
        result.File.States.Select(state => state.StateKey).Should().Equal(
            workflow.Stages.OrderBy(stage => stage.StageKey, StringComparer.Ordinal).Select(stage => stage.StageKey));
        result.File.States.Should().ContainSingle(state => state.StateKey == "declaration")
            .Which.Metadata!.Actions.Should().ContainSingle(action => action.Type == "forms.load" && action.Timing == "OnEntry");
        result.File.Gateways.Should().NotBeNull();
        result.File.Gateways!
            .SelectMany(gateway => gateway.Routes ?? [])
            .Should()
            .Contain(route =>
                route.Trigger == "submit"
                && route.Actions!.Any(action => action.Type == "forms.submit" && action.Timing == "OnTransition"));
        result.File.Gateways!
            .SelectMany(gateway => gateway.Routes ?? [])
            .Should()
            .Contain(route =>
                route.Trigger == "submit"
                && route.Conditions!.Any(condition => condition.Expression == "application.isComplete == true"));
    }

    [Fact]
    public async Task PreviewAsync_UsesCurrentPublishedChecksumWhenDefinitionAlreadyExists()
    {
        var workflow = await LoadPlanningFixture();
        var firstPublish = await _sut.PublishAsync(workflow);

        var preview = await _sut.PreviewAsync(workflow);

        preview.HasErrors.Should().BeFalse();
        preview.CurrentPublishedChecksum.Should().Be(firstPublish.VerifiedChecksum);
        preview.WouldChange.Should().BeFalse();
    }

    [Fact]
    public async Task PublishAsync_RoundTripsThroughRuntimeStore()
    {
        var workflow = await LoadPlanningFixture();

        var result = await _sut.PublishAsync(workflow);
        var publishedJson = await File.ReadAllTextAsync(result.PublishedPath!);
        var reloaded = JsonSerializer.Deserialize<WorkflowDefinitionFile>(publishedJson, WorkflowProjector.CanonicalOptions);

        result.RoundTripVerified.Should().BeTrue();
        result.VerifiedChecksum.Should().Be(result.Checksum);
        reloaded.Should().NotBeNull();
        reloaded!.Metadata!.AuthoredWorkflowId.Should().Be(workflow.Id);
        reloaded.Metadata.Handoffs.Should().ContainSingle(handoff => handoff.Id == "applicant-to-caseworker");
        reloaded.States.Should().ContainSingle(state => state.StateKey == "application-form")
            .Which.Metadata!.Actions.Should().ContainSingle(action => action.Type == "forms.save");
    }

    [Fact]
    public async Task PublishAsync_WithNamedQueuesAndGateways_PreservesQueueOwnershipMetadata()
    {
        var workflow = new AuthoredWorkflow
        {
            DefinitionKey = "queue-owned-workflow",
            DisplayName = "Queue Owned Workflow",
            InitialStageKey = "draft",
            Queues =
            [
                new AuthoredQueue
                {
                    Key = "web-user",
                    DisplayName = "Applicant queue",
                    Actor = "applicant",
                    RoleGates = ["submitter"]
                }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "fan-out",
                    DisplayName = "Fan out",
                    Kind = GatewayKind.Split,
                    QueueKey = "web-user",
                    Routes = [new AuthoredRoute { Id = "to-join", Target = "fan-in", Trigger = "submit" }]
                },
                new AuthoredGateway
                {
                    GatewayKey = "fan-in",
                    DisplayName = "Fan in",
                    Kind = GatewayKind.Join,
                    QueueKey = "web-user",
                    WaitingInfo = new WaitingMetadata
                    {
                        Content = "Waiting for all queues to complete.",
                        ExpectedWaitSeconds = 60
                    },
                    RequiredIncomingQueues = ["web-user"],
                    Routes = [new AuthoredRoute { Id = "release", Target = "done", Trigger = "release" }]
                }
            ],
            Stages =
            [
                new AuthoredStage
                {
                    StageKey = "draft",
                    DisplayName = "Draft",
                    Kind = StageKind.Question,
                    QueueKey = "web-user",
                    Routes = [new AuthoredRoute { Id = "draft-submit", Target = "fan-out", Trigger = "submit" }]
                },
                new AuthoredStage
                {
                    StageKey = "done",
                    DisplayName = "Done",
                    Kind = StageKind.Confirmation,
                    QueueKey = "web-user"
                }
            ]
        };

        var result = await _sut.PublishAsync(workflow);

        result.HasErrors.Should().BeFalse();
        result.File.Queues.Should().ContainSingle(queue =>
            queue.Key == "web-user" && queue.Actor == "applicant");
        result.File.Metadata.Gateways.Should().HaveCount(2);
        result.File.Metadata.Gateways.Should().ContainSingle(gateway => gateway.Key == "fan-out" && gateway.GatewayType == "Split");
        result.File.Metadata.Gateways.Should().ContainSingle(gateway => gateway.Key == "fan-in" && gateway.GatewayType == "Join");

        var stateMetadata = result.File.States.Single(state => state.StateKey == "draft").Metadata;
        stateMetadata.Should().NotBeNull();
        stateMetadata!.StageType.Should().Be("Question");
        stateMetadata.Actor.Should().Be("applicant");
        stateMetadata.QueueKey.Should().Be("web-user");
        stateMetadata.RoleGates.Should().Equal("submitter");
    }

    [Fact]
    public async Task PublishAsync_PaymentFixtureWithDirectStageToJoinRoute_PublishesWithoutIntermediateGateway()
    {
        var workflow = await LoadFixture("payment-demo");

        var result = await _sut.PublishAsync(workflow);

        result.HasErrors.Should().BeFalse();
        result.File.Gateways.Should().NotContain(gateway => gateway.Key == "confirm-payment-route");
        result.File.States.Single(state => state.StateKey == "confirm-payment-received")
            .Routes.Should().ContainSingle(route =>
                route.Target == "await-payment-confirmation"
                && route.Trigger == "confirm");
        result.File.Gateways!.Single(gateway => gateway.Key == "await-payment-confirmation")
            .RequiredIncomingQueues.Should().Equal("business-user", "web-user");
    }

    public void Dispose()
    {
        if (Directory.Exists(_publishedPath))
            Directory.Delete(_publishedPath, recursive: true);
    }

    private static async Task<AuthoredWorkflow> LoadPlanningFixture()
    {
        return await AuthoredWorkflowFixtureLoader.LoadAsync(FixturesPath, "planning")
            ?? throw new InvalidOperationException("planning fixture not found");
    }

    private static async Task<AuthoredWorkflow> LoadFixture(string workflowKey)
    {
        return await AuthoredWorkflowFixtureLoader.LoadAsync(FixturesPath, workflowKey)
            ?? throw new InvalidOperationException($"{workflowKey} fixture not found");
    }
}
