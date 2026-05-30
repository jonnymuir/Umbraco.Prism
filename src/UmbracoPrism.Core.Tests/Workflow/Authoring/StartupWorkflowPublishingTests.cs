using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Verifies that authored workflows are correctly published to the runtime store at startup,
/// establishing authored definitions as the single source of truth while preserving the
/// authored → projector → runtime boundary.
/// </summary>
public sealed class StartupWorkflowPublishingTests
{
    private static readonly string FixturesPath = Path.Combine(
        AppContext.BaseDirectory,
        "Workflow",
        "Authoring",
        "Fixtures");

    [Fact]
    public async Task PublishAsync_ProjectsAuthoredWorkflowIntoRuntimeStore()
    {
        // Arrange: Set up the same infrastructure as MockBusinessApp startup
        var authoredStore = new FilesystemAuthoredWorkflowStore(FixturesPath);
        var engine = CreateTestEngine();
        var publishedStore = new InMemoryRuntimePublishedWorkflowStore(engine);
        var publishService = new WorkflowPublishService(
            new WorkflowProjector(),
            publishedStore);

        // Act: Simulate the startup publishing loop
        var authoredEntries = await authoredStore.ListAsync();
        var loadableEntries = authoredEntries.Where(entry => entry.IsLoadable).ToList();

        foreach (var entry in loadableEntries)
        {
            var authored = await authoredStore.LoadAsync(entry.WorkflowKey);
            if (authored is null) continue;

            var result = await publishService.PublishAsync(authored);
            result.HasErrors.Should().BeFalse(
                "startup publishing should succeed for valid authored workflows");
        }

        // Assert: The published store should now have the published workflow
        var planningWorkflow = await authoredStore.LoadAsync("planning");
        planningWorkflow.Should().NotBeNull();

        var runtimeDefinition = await publishedStore.LoadAsync(planningWorkflow!.DefinitionKey);
        runtimeDefinition.Should().NotBeNull(
            "published store should have the workflow after startup publishing");
        runtimeDefinition!.DefinitionKey.Should().Be(planningWorkflow.DefinitionKey);
        runtimeDefinition.Metadata!.AuthoredWorkflowId.Should().Be(planningWorkflow.Id,
            "runtime metadata should preserve authored workflow provenance");
    }

    [Fact]
    public async Task PublishedWorkflow_PreservesAuthoredMetadata()
    {
        // Arrange
        var authoredStore = new FilesystemAuthoredWorkflowStore(FixturesPath);
        var engine = CreateTestEngine();
        var publishedStore = new InMemoryRuntimePublishedWorkflowStore(engine);
        var publishService = new WorkflowPublishService(
            new WorkflowProjector(),
            publishedStore);

        // Act
        var authored = await authoredStore.LoadAsync("planning");
        authored.Should().NotBeNull();

        var result = await publishService.PublishAsync(authored!);

        // Assert
        result.HasErrors.Should().BeFalse();
        result.File.Metadata!.AuthoredWorkflowId.Should().Be(authored!.Id);
        result.File.Metadata.Description.Should().Be(authored.Description);
        result.File.Metadata.Tags.Should().NotBeNull();
        result.File.Metadata.Tags!["serviceArea"].Should().Be("Planning");
    }

    [Fact]
    public async Task RuntimeDefinition_ReflectsPublishedWorkflowStructure()
    {
        // Arrange
        var authoredStore = new FilesystemAuthoredWorkflowStore(FixturesPath);
        var engine = CreateTestEngine();
        var publishedStore = new InMemoryRuntimePublishedWorkflowStore(engine);
        var publishService = new WorkflowPublishService(
            new WorkflowProjector(),
            publishedStore);

        // Act
        var authored = await authoredStore.LoadAsync("planning");
        var result = await publishService.PublishAsync(authored!);

        // Assert: Published store should reflect authored structure
        var runtimeDefinition = await publishedStore.LoadAsync(authored!.DefinitionKey);
        runtimeDefinition.Should().NotBeNull();
        runtimeDefinition!.States.Select(s => s.StateKey).Should()
            .BeEquivalentTo(authored.Stages.Select(s => s.StageKey));
        runtimeDefinition.Transitions.Select(t => t.Action).Should()
            .BeEquivalentTo(authored.Transitions.Select(t => t.Trigger));
    }

    private static BusinessAppWorkflowEngine CreateTestEngine()
    {
        var mockEnv = new Mock<IWebHostEnvironment>();
        return new BusinessAppWorkflowEngine(
            new NullLogger<BusinessAppWorkflowEngine>(),
            mockEnv.Object,
            new TestSanitizer(),
            new InMemoryWorkflowDefinitionStore(),
            actionRegistry: null);
    }

    /// <summary>
    /// Test-only sanitizer that passes through content unchanged.
    /// </summary>
    private sealed class TestSanitizer : IWorkflowContentSanitizer
    {
        public string Sanitize(string? html) => html ?? string.Empty;
    }

    /// <summary>
    /// In-memory workflow definition store for testing.
    /// Mimics FilesystemWorkflowDefinitionStore but uses in-memory storage.
    /// </summary>
    private sealed class InMemoryWorkflowDefinitionStore : UmbracoPrism.WorkflowRuntime.Abstractions.IWorkflowDefinitionStore
    {
        private readonly Dictionary<string, UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile> _definitions = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile> LoadDefinitions(
            Microsoft.Extensions.Logging.ILogger logger)
        {
            return _definitions;
        }

        public void AddOrUpdate(UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile definition)
        {
            _definitions[definition.DefinitionKey] = definition;
        }
    }
}
