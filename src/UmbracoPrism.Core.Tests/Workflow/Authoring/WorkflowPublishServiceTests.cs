using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

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

    public void Dispose()
    {
        if (Directory.Exists(_publishedPath))
            Directory.Delete(_publishedPath, recursive: true);
    }

    private static async Task<AuthoredWorkflow> LoadPlanningFixture()
    {
        var store = new FilesystemAuthoredWorkflowStore(FixturesPath);
        return await store.LoadAsync("planning")
            ?? throw new InvalidOperationException("planning fixture not found");
    }
}
