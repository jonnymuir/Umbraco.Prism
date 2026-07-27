using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public sealed class InMemoryRuntimePublishedWorkflowStoreTests : IDisposable
{
    private readonly string _contentRootPath = Path.Combine(Path.GetTempPath(), $"workflow-runtime-{Guid.NewGuid():N}");

    public InMemoryRuntimePublishedWorkflowStoreTests()
    {
        Directory.CreateDirectory(Path.Combine(_contentRootPath, "service-blueprints"));
    }

    [Fact]
    public async Task SaveAsync_UpdatesRuntimeEngineWithoutMutatingSeedFile()
    {
        var original = BuildDefinition("planning", "Planning");
        await SeedDefinitionAsync(original);

        var engine = CreateEngine();
        var store = new InMemoryRuntimePublishedServiceBlueprintStore(engine);
        var updated = BuildDefinition("planning", "Planning updated");

        var result = await store.SaveAsync(updated, expectedVersion: 1);

        result.Saved.Should().BeTrue();
        result.CurrentVersion.Should().Be(2);
        result.Location.Should().Be("memory://published-workflows/planning");
        engine.GetDefinition("planning")!.DisplayName.Should().Be("Planning updated");
        engine.GetDefinition("planning")!.Version.Should().Be(2);

        var diskDefinition = JsonSerializer.Deserialize<ServiceBlueprint>(
            await File.ReadAllTextAsync(GetSeedPath("planning")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        diskDefinition.Should().NotBeNull();
        diskDefinition!.DisplayName.Should().Be("Planning");
    }

    /// <summary>
    /// This is the store both /mockapp/service-blueprints/* (the editor) and /prism/workflow-authoring/*
    /// (the AI toolkit) share. A stale expectedVersion — e.g. a human's editor session that loaded
    /// the workflow before an AI's save landed — must be rejected, not silently overwrite.
    /// </summary>
    [Fact]
    public async Task SaveAsync_StaleExpectedVersion_RejectsWithoutMutatingEngine()
    {
        var original = BuildDefinition("planning", "Planning");
        await SeedDefinitionAsync(original);

        var engine = CreateEngine();
        var store = new InMemoryRuntimePublishedServiceBlueprintStore(engine);

        var aiSave = await store.SaveAsync(BuildDefinition("planning", "Saved by AI"), expectedVersion: 1);
        aiSave.Saved.Should().BeTrue();

        // The human's editor loaded "planning" before the AI's save (still thinks version is 1).
        var humanSave = await store.SaveAsync(BuildDefinition("planning", "Saved by human"), expectedVersion: 1);

        humanSave.Saved.Should().BeFalse();
        humanSave.CurrentVersion.Should().Be(2);
        engine.GetDefinition("planning")!.DisplayName.Should().Be("Saved by AI",
            because: "the stale human save must not have clobbered the AI's live change");
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRootPath))
            Directory.Delete(_contentRootPath, recursive: true);
    }

    private BusinessAppProcessManager CreateEngine()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(_contentRootPath);

        var logger = new Mock<ILogger<BusinessAppProcessManager>>();
        var sanitizer = new Mock<IServiceContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(value => value ?? string.Empty);

        return new BusinessAppProcessManager(logger.Object, environment.Object, sanitizer.Object);
    }

    private Task SeedDefinitionAsync(ServiceBlueprint definition) =>
        File.WriteAllTextAsync(GetSeedPath(definition.DefinitionKey), JsonSerializer.Serialize(definition));

    private string GetSeedPath(string key) =>
        Path.Combine(_contentRootPath, "service-blueprints", $"{key}.json");

    private static ServiceBlueprint BuildDefinition(string definitionKey, string displayName) => new()
    {
        DefinitionKey = definitionKey,
        DisplayName = displayName,
        Version = 1,
        InitialTouchpoint = "start",
        Touchpoints = [],
        Transitions = []
    };
}
