using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.MockBusinessApp.Services.Publishing;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Verifies that authored workflows are correctly published to the runtime store at startup,
/// establishing authored definitions as the single source of truth while preserving the
/// authored → projector → runtime boundary.
/// </summary>
public sealed class StartupWorkflowPublishingTests
{
    private static readonly string FixturesPath = Path.Combine(
        AppContext.BaseDirectory,
        "ServiceDesign",
        "Authoring",
        "Fixtures");

    [Fact]
    public async Task PublishAsync_ProjectsAuthoredWorkflowIntoRuntimeStore()
    {
        var engine = CreateTestEngine();
        var publishedStore = new InMemoryRuntimePublishedServiceBlueprintStore(engine);
        var publishService = new ServiceBlueprintPublishService(
            new ServiceBlueprintProjector(),
            publishedStore);

        foreach (var key in AuthoredServiceBlueprintFixtureLoader.ListKeys(FixturesPath))
        {
            var authored = await AuthoredServiceBlueprintFixtureLoader.LoadAsync(FixturesPath, key);
            if (authored is null) continue;

            var result = await publishService.PublishAsync(authored);
            result.HasErrors.Should().BeFalse(
                "startup publishing should succeed for valid authored workflows");
        }

        var planningWorkflow = await AuthoredServiceBlueprintFixtureLoader.LoadAsync(FixturesPath, "planning");
        planningWorkflow.Should().NotBeNull();

        var runtimeDefinition = await publishedStore.LoadAsync(planningWorkflow!.DefinitionKey);
        runtimeDefinition.Should().NotBeNull(
            "published store should have the workflow after startup publishing");
        runtimeDefinition!.DefinitionKey.Should().Be(planningWorkflow.DefinitionKey);
        runtimeDefinition.Metadata!.AuthoredServiceBlueprintId.Should().Be(planningWorkflow.Id,
            "runtime metadata should preserve authored workflow provenance");
    }

    [Fact]
    public async Task PublishedWorkflow_PreservesAuthoredMetadata()
    {
        var engine = CreateTestEngine();
        var publishedStore = new InMemoryRuntimePublishedServiceBlueprintStore(engine);
        var publishService = new ServiceBlueprintPublishService(
            new ServiceBlueprintProjector(),
            publishedStore);

        var authored = await AuthoredServiceBlueprintFixtureLoader.LoadAsync(FixturesPath, "planning");
        authored.Should().NotBeNull();

        var result = await publishService.PublishAsync(authored!);

        result.HasErrors.Should().BeFalse();
        result.File.Metadata!.AuthoredServiceBlueprintId.Should().Be(authored!.Id);
        result.File.Metadata.Description.Should().Be(authored.Description);
        result.File.Metadata.Tags.Should().NotBeNull();
        result.File.Metadata.Tags!["serviceArea"].Should().Be("Planning");
    }

    [Fact]
    public async Task RuntimeDefinition_ReflectsPublishedWorkflowStructure()
    {
        var engine = CreateTestEngine();
        var publishedStore = new InMemoryRuntimePublishedServiceBlueprintStore(engine);
        var publishService = new ServiceBlueprintPublishService(
            new ServiceBlueprintProjector(),
            publishedStore);

        var authored = await AuthoredServiceBlueprintFixtureLoader.LoadAsync(FixturesPath, "planning");
        var result = await publishService.PublishAsync(authored!);

        var runtimeDefinition = await publishedStore.LoadAsync(authored!.DefinitionKey);
        runtimeDefinition.Should().NotBeNull();
        runtimeDefinition!.Touchpoints.Select(s => s.TouchpointKey).Should()
            .BeEquivalentTo(authored.Touchpoints.Select(s => s.TouchpointKey));
        runtimeDefinition.Gateways.Should().NotBeNull();
        runtimeDefinition.Gateways!.Select(gateway => gateway.Key).Should()
            .BeEquivalentTo(authored.Gateways.Select(gateway => gateway.GatewayKey));
    }

    private static BusinessAppProcessManager CreateTestEngine()
    {
        var mockEnv = new Mock<IWebHostEnvironment>();
        return new BusinessAppProcessManager(
            new NullLogger<BusinessAppProcessManager>(),
            mockEnv.Object,
            new TestSanitizer(),
            new InMemoryWorkflowDefinitionStore(),
            actionRegistry: null);
    }

    /// <summary>
    /// Test-only sanitizer that passes through content unchanged.
    /// </summary>
    private sealed class TestSanitizer : IServiceContentSanitizer
    {
        public string Sanitize(string? html) => html ?? string.Empty;
    }

    /// <summary>
    /// In-memory workflow definition store for testing.
    /// Mimics FilesystemServiceBlueprintStore but uses in-memory storage.
    /// </summary>
    private sealed class InMemoryWorkflowDefinitionStore : UmbracoPrism.ProcessManager.Abstractions.IServiceBlueprintStore
    {
        private readonly Dictionary<string, UmbracoPrism.Shared.Models.ServiceDesign.ServiceBlueprint> _definitions = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, UmbracoPrism.Shared.Models.ServiceDesign.ServiceBlueprint> LoadDefinitions(
            Microsoft.Extensions.Logging.ILogger logger)
        {
            return _definitions;
        }

        public void AddOrUpdate(UmbracoPrism.Shared.Models.ServiceDesign.ServiceBlueprint definition)
        {
            _definitions[definition.DefinitionKey] = definition;
        }
    }
}
