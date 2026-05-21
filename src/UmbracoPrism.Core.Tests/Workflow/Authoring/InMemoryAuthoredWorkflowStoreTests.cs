using FluentAssertions;
using System.Text.Json;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public sealed class InMemoryAuthoredWorkflowStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"workflow-authored-{Guid.NewGuid():N}");

    public InMemoryAuthoredWorkflowStoreTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task FromFilesystemDirectory_LoadsValidSeedIntoMemory()
    {
        var seeded = BuildWorkflow("planning");
        await WriteWorkflowAsync("planning", seeded);

        var store = InMemoryAuthoredWorkflowStore.FromFilesystemDirectory(_tempDirectory);

        var loaded = await store.LoadAsync("planning");

        loaded.Should().NotBeNull();
        loaded!.DefinitionKey.Should().Be("planning");
        loaded.DisplayName.Should().Be("Planning");
    }

    [Fact]
    public async Task FromFilesystemDirectory_PreservesFilenameKey_WhenDefinitionKeyDiffers()
    {
        var seeded = BuildWorkflow("planning-application");
        await WriteWorkflowAsync("planning", seeded);

        var store = InMemoryAuthoredWorkflowStore.FromFilesystemDirectory(_tempDirectory);

        var loaded = await store.LoadAsync("planning");

        loaded.Should().NotBeNull();
        loaded!.DefinitionKey.Should().Be("planning-application");
        (await store.ListKeysAsync()).Should().Contain("planning");
        (await store.ListKeysAsync()).Should().NotContain("planning-application",
            because: "the editor route key should stay aligned with the authored document key that the host shell links to");
    }

    [Fact]
    public async Task SaveAsync_UpdatesMemoryWithoutWritingANewFile()
    {
        var store = InMemoryAuthoredWorkflowStore.FromFilesystemDirectory(_tempDirectory);
        var updated = BuildWorkflow("demo-save");

        var location = await store.SaveAsync(updated);
        var loaded = await store.LoadAsync("demo-save");

        location.Should().Be("memory://authored-workflows/demo-save");
        loaded.Should().NotBeNull();
        File.Exists(Path.Combine(_tempDirectory, "demo-save.workflow.json")).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WithWorkflowKey_PreservesLookupAlias()
    {
        var store = InMemoryAuthoredWorkflowStore.FromFilesystemDirectory(_tempDirectory);
        var updated = BuildWorkflow("planning-application");

        var location = await store.SaveAsync("planning", updated);
        var loaded = await store.LoadAsync("planning");

        location.Should().Be("memory://authored-workflows/planning");
        loaded.Should().NotBeNull();
        loaded!.DefinitionKey.Should().Be("planning-application");
        (await store.ListAsync()).Should().ContainSingle(entry => entry.WorkflowKey == "planning")
            .Which.DefinitionKey.Should().Be("planning-application");
    }

    [Fact]
    public async Task LoadAsync_RethrowsSeededJsonErrors()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "broken.workflow.json"), "{");
        var store = InMemoryAuthoredWorkflowStore.FromFilesystemDirectory(_tempDirectory);

        var act = async () => await store.LoadAsync("broken");

        await act.Should().ThrowAsync<JsonException>();
        (await store.ListKeysAsync()).Should().Contain("broken");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private async Task WriteWorkflowAsync(string key, AuthoredWorkflow workflow)
    {
        var path = Path.Combine(_tempDirectory, $"{key}.workflow.json");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(workflow, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
    }

    private static AuthoredWorkflow BuildWorkflow(string definitionKey) => new()
    {
        Id = Guid.NewGuid(),
        DefinitionKey = definitionKey,
        DisplayName = "Planning",
        InitialStageKey = "start",
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "start",
                DisplayName = "Start",
                Kind = StageKind.Confirmation
            }
        ]
    };
}
