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
        result.File.Transitions.Should().ContainSingle(transition => transition.Action == "submit")
            .Which.Metadata!.Actions.Should().ContainSingle(action => action.Type == "forms.submit" && action.Timing == "OnTransition");
        result.File.Transitions.Should().ContainSingle(transition => transition.Action == "submit")
            .Which.Metadata!.Conditions.Should().ContainSingle(condition => condition.Expression == "application.isComplete == true");
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
    public async Task PublishAsync_WithNamedLanesAndGateways_PreservesLaneOwnershipMetadata()
    {
        var workflow = new AuthoredWorkflow
        {
            DefinitionKey = "lane-owned-workflow",
            DisplayName = "Lane Owned Workflow",
            InitialStageKey = "draft",
            Lanes =
            [
                new AuthoredLane
                {
                    Key = "applicant",
                    DisplayName = "Applicant lane",
                    Actor = "applicant",
                    QueueName = "web-user",
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
                    LaneKey = "applicant",
                    Source = "draft",
                    Routes = [new AuthoredRoute { Id = "to-join", Target = "fan-in", Trigger = "submit" }]
                },
                new AuthoredGateway
                {
                    GatewayKey = "fan-in",
                    DisplayName = "Fan in",
                    Kind = GatewayKind.Join,
                    LaneKey = "applicant",
                    WaitingInfo = new WaitingMetadata
                    {
                        Content = "Waiting for all lanes to complete.",
                        ExpectedWaitSeconds = 60
                    },
                    RequiredIncomingLanes = ["applicant"],
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
                    LaneKey = "applicant"
                },
                new AuthoredStage
                {
                    StageKey = "done",
                    DisplayName = "Done",
                    Kind = StageKind.Confirmation,
                    LaneKey = "applicant"
                }
            ]
        };

        var result = await _sut.PublishAsync(workflow);

        result.HasErrors.Should().BeFalse();
        result.File.Metadata!.Lanes.Should().ContainSingle(lane =>
            lane.Key == "applicant" && lane.Actor == "applicant" && lane.QueueName == "web-user");
        result.File.Metadata.Gateways.Should().HaveCount(2);
        result.File.Metadata.Gateways.Should().ContainSingle(gateway => gateway.Key == "fan-out" && gateway.GatewayType == "Split");
        result.File.Metadata.Gateways.Should().ContainSingle(gateway => gateway.Key == "fan-in" && gateway.GatewayType == "Join");

        var stateMetadata = result.File.States.Single(state => state.StateKey == "draft").Metadata;
        stateMetadata.Should().NotBeNull();
        stateMetadata!.StageType.Should().Be("Question");
        stateMetadata.Actor.Should().Be("applicant");
        stateMetadata.LaneKey.Should().Be("applicant");
        stateMetadata.RoleGates.Should().Equal("submitter");
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
}
