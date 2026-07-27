using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.MockBusinessApp.Services.Publishing;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;
using UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public sealed class WorkflowPublishServiceTests : IDisposable
{
    private static readonly string FixturesPath = Path.Combine(
        AppContext.BaseDirectory,
        "ServiceDesign",
        "Authoring",
        "Fixtures");

    private readonly string _publishedPath = Path.Combine(
        AppContext.BaseDirectory,
        "ServiceDesign",
        "Authoring",
        "Published",
        Guid.NewGuid().ToString("N"));

    private readonly ServiceBlueprintPublishService _sut;

    public WorkflowPublishServiceTests()
    {
        Directory.CreateDirectory(_publishedPath);
        _sut = new ServiceBlueprintPublishService(
            new ServiceBlueprintProjector(),
            new FilesystemPublishedServiceBlueprintStore(_publishedPath));
    }

    [Fact]
    public async Task PublishAsync_PlanningFixture_ProjectsStagesTransitionsAndActions()
    {
        var workflow = await LoadPlanningFixture();

        var result = await _sut.PublishAsync(workflow);

        result.HasErrors.Should().BeFalse();
        result.RoundTripVerified.Should().BeTrue();
        result.File.Touchpoints.Select(state => state.TouchpointKey).Should().Equal(
            workflow.Touchpoints.OrderBy(stage => stage.TouchpointKey, StringComparer.Ordinal).Select(stage => stage.TouchpointKey));
        result.File.Touchpoints.Should().ContainSingle(state => state.TouchpointKey == "declaration")
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
        var reloaded = JsonSerializer.Deserialize<ServiceBlueprint>(publishedJson, ServiceBlueprintProjector.CanonicalOptions);

        result.RoundTripVerified.Should().BeTrue();
        result.VerifiedChecksum.Should().Be(result.Checksum);
        reloaded.Should().NotBeNull();
        reloaded!.Metadata!.AuthoredServiceBlueprintId.Should().Be(workflow.Id);
        reloaded.Metadata.Handoffs.Should().ContainSingle(handoff => handoff.Id == "applicant-to-caseworker");
        reloaded.Touchpoints.Should().ContainSingle(state => state.TouchpointKey == "application-form")
            .Which.Metadata!.Actions.Should().ContainSingle(action => action.Type == "forms.save");
    }

    [Fact]
    public async Task PublishAsync_WithNamedQueuesAndGateways_PreservesQueueOwnershipMetadata()
    {
        var workflow = new AuthoredServiceBlueprint
        {
            DefinitionKey = "queue-owned-workflow",
            DisplayName = "Queue Owned Workflow",
            InitialTouchpointKey = "draft",
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
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "draft",
                    DisplayName = "Draft",
                    Kind = TouchpointKind.Question,
                    QueueKey = "web-user",
                    Routes = [new AuthoredRoute { Id = "draft-submit", Target = "fan-out", Trigger = "submit" }]
                },
                new AuthoredTouchpoint
                {
                    TouchpointKey = "done",
                    DisplayName = "Done",
                    Kind = TouchpointKind.Confirmation,
                    QueueKey = "web-user"
                }
            ]
        };

        var result = await _sut.PublishAsync(workflow);

        result.HasErrors.Should().BeFalse();
        result.File.Queues.Should().ContainSingle(queue =>
            queue.Key == "web-user" && queue.Actor == "applicant");
        result.File.Metadata.Should().NotBeNull();
        result.File.Metadata!.Gateways.Should().HaveCount(2);
        result.File.Metadata.Gateways.Should().ContainSingle(gateway => gateway.Key == "fan-out" && gateway.GatewayType == "Split");
        result.File.Metadata.Gateways.Should().ContainSingle(gateway => gateway.Key == "fan-in" && gateway.GatewayType == "Join");

        var stateMetadata = result.File.Touchpoints.Single(state => state.TouchpointKey == "draft").Metadata;
        stateMetadata.Should().NotBeNull();
        stateMetadata!.TouchpointType.Should().Be("Question");
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
        result.File.Touchpoints.Single(state => state.TouchpointKey == "confirm-payment-received")
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

    private static async Task<AuthoredServiceBlueprint> LoadPlanningFixture()
    {
        return await AuthoredServiceBlueprintFixtureLoader.LoadAsync(FixturesPath, "planning")
            ?? throw new InvalidOperationException("planning fixture not found");
    }

    private static async Task<AuthoredServiceBlueprint> LoadFixture(string workflowKey)
    {
        return await AuthoredServiceBlueprintFixtureLoader.LoadAsync(FixturesPath, workflowKey)
            ?? throw new InvalidOperationException($"{workflowKey} fixture not found");
    }
}
