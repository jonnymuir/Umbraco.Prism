using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public sealed class InMemoryRuntimePublishedWorkflowStoreTests : IDisposable
{
    private readonly string _contentRootPath = Path.Combine(Path.GetTempPath(), $"workflow-runtime-{Guid.NewGuid():N}");

    public InMemoryRuntimePublishedWorkflowStoreTests()
    {
        Directory.CreateDirectory(Path.Combine(_contentRootPath, "workflow-seeds"));
    }

    [Fact]
    public async Task SaveAsync_UpdatesRuntimeEngineWithoutMutatingSeedFile()
    {
        var original = BuildDefinition("planning", "Planning");
        await SeedDefinitionAsync(original);

        var engine = CreateEngine();
        var store = new InMemoryRuntimePublishedWorkflowStore(engine);
        var updated = BuildDefinition("planning", "Planning updated");

        var location = await store.SaveAsync(updated);

        location.Should().Be("memory://published-workflows/planning");
        engine.GetDefinition("planning")!.DisplayName.Should().Be("Planning updated");

        var diskDefinition = JsonSerializer.Deserialize<WorkflowDefinitionFile>(
            await File.ReadAllTextAsync(GetSeedPath("planning")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        diskDefinition.Should().NotBeNull();
        diskDefinition!.DisplayName.Should().Be("Planning");
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRootPath))
            Directory.Delete(_contentRootPath, recursive: true);
    }

    private BusinessAppWorkflowEngine CreateEngine()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(_contentRootPath);

        var logger = new Mock<ILogger<BusinessAppWorkflowEngine>>();
        var sanitizer = new Mock<IWorkflowContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(value => value ?? string.Empty);

        return new BusinessAppWorkflowEngine(logger.Object, environment.Object, sanitizer.Object);
    }

    private Task SeedDefinitionAsync(WorkflowDefinitionFile definition) =>
        File.WriteAllTextAsync(GetSeedPath(definition.DefinitionKey), JsonSerializer.Serialize(definition));

    private string GetSeedPath(string key) =>
        Path.Combine(_contentRootPath, "workflow-seeds", $"{key}.json");

    private static WorkflowDefinitionFile BuildDefinition(string definitionKey, string displayName) => new()
    {
        DefinitionKey = definitionKey,
        DisplayName = displayName,
        Version = 1,
        InitialState = "start",
        States = [],
        Transitions = []
    };
}
