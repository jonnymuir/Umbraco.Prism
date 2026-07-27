using FluentAssertions;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public class ServiceBlueprintAuthoringServiceQueueCapabilitiesTests
{
    [Fact]
    public void Validate_NoProviderRegistered_IsUnaffected()
    {
        var service = new ServiceBlueprintAuthoringService(new InMemoryServiceBlueprintSourceStore());

        var outcome = service.Validate(ProjectWorkflowWithFieldsetOnBusinessUser());

        outcome.IsValid.Should().BeTrue();
        outcome.Diagnostics.Should().NotContain(d => d.Code == "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT");
    }

    [Fact]
    public void Validate_ProviderRegistered_QueueUndeclared_IsUnrestricted()
    {
        var provider = new StaticQueueCapabilitiesProvider(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
        var service = new ServiceBlueprintAuthoringService(new InMemoryServiceBlueprintSourceStore(), queueCapabilities: provider);

        var outcome = service.Validate(ProjectWorkflowWithFieldsetOnBusinessUser());

        outcome.IsValid.Should().BeTrue();
        outcome.Diagnostics.Should().NotContain(d => d.Code == "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT");
    }

    [Fact]
    public void Validate_QueueDeclaredMissingFieldsetType_FlagsOnlyTheFieldset()
    {
        var provider = new StaticQueueCapabilitiesProvider(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["business-user"] = new[] { "text" }
            });
        var service = new ServiceBlueprintAuthoringService(new InMemoryServiceBlueprintSourceStore(), queueCapabilities: provider);

        var outcome = service.Validate(ProjectWorkflowWithFieldsetOnBusinessUser());

        outcome.IsValid.Should().BeFalse();
        var matches = outcome.Diagnostics.Where(d => d.Code == "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT").ToList();
        matches.Should().ContainSingle();
        matches[0].Message.Should().Contain("fieldset");
        matches[0].Message.Should().NotContain("'text'");
    }

    [Fact]
    public void Validate_QueueDeclaredEmpty_FlagsEveryComponentInTheTree()
    {
        var provider = new StaticQueueCapabilitiesProvider(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["business-user"] = Array.Empty<string>()
            });
        var service = new ServiceBlueprintAuthoringService(new InMemoryServiceBlueprintSourceStore(), queueCapabilities: provider);

        var outcome = service.Validate(ProjectWorkflowWithFieldsetOnBusinessUser());

        outcome.IsValid.Should().BeFalse();
        var matches = outcome.Diagnostics.Where(d => d.Code == "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT").ToList();
        matches.Should().HaveCount(2, because: "both the fieldset wrapper and its text child are unsupported");
    }

    [Fact]
    public void GetQueueCapabilities_NoProviderRegistered_ReturnsEmpty()
    {
        var service = new ServiceBlueprintAuthoringService(new InMemoryServiceBlueprintSourceStore());

        service.GetQueueCapabilities().Should().BeEmpty();
    }

    [Fact]
    public void GetQueueCapabilities_ProviderRegistered_ReturnsItsDeclaredCapabilities()
    {
        var provider = new StaticQueueCapabilitiesProvider(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["business-user"] = new[] { "text", "decimal" }
            });
        var service = new ServiceBlueprintAuthoringService(new InMemoryServiceBlueprintSourceStore(), queueCapabilities: provider);

        var capabilities = service.GetQueueCapabilities();

        capabilities.Should().ContainKey("business-user");
        capabilities["business-user"].Should().BeEquivalentTo(new[] { "text", "decimal" });
    }

    private static ServiceBlueprint ProjectWorkflowWithFieldsetOnBusinessUser()
    {
        var authored = new AuthoredServiceBlueprint
        {
            Id = Guid.NewGuid(),
            DefinitionKey = "queue-capabilities-test",
            DisplayName = "Queue Capabilities Test",
            Version = 1,
            InitialTouchpointKey = "review",
            RequestPolicy = "single",
            Queues = [new AuthoredQueue { Key = "business-user", DisplayName = "Business User", Actor = "reviewer" }],
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "review",
                    DisplayName = "Review",
                    Kind = TouchpointKind.Question,
                    QueueKey = "business-user",
                    Components =
                    [
                        new FieldsetComponent
                        {
                            Legend = "Details",
                            Children = [new TextInputComponent { FieldKey = "reference", Label = "Reference" }]
                        }
                    ]
                }
            ]
        };

        return new ServiceBlueprintProjector().Project(authored).File;
    }

    private sealed class InMemoryServiceBlueprintSourceStore : IServiceBlueprintSourceStore
    {
        private readonly Dictionary<string, ServiceBlueprint> _entries = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ServiceBlueprintSourceSummary>>(
                _entries.Values.Select(w => new ServiceBlueprintSourceSummary(w.DefinitionKey, w.DisplayName)).ToArray());

        public Task<ServiceBlueprint?> LoadAsync(string definitionKey, CancellationToken ct = default) =>
            Task.FromResult(_entries.TryGetValue(definitionKey, out var workflow) ? workflow : null);

        public Task<ServiceBlueprintSaveResult> SaveAsync(ServiceBlueprint workflow, int expectedVersion, CancellationToken ct = default)
        {
            var currentVersion = _entries.TryGetValue(workflow.DefinitionKey, out var existing) ? existing.Version : 0;
            if (currentVersion != expectedVersion)
            {
                return Task.FromResult(new ServiceBlueprintSaveResult(Saved: false, CurrentVersion: currentVersion, Location: $"memory://{workflow.DefinitionKey}"));
            }

            var newVersion = expectedVersion + 1;
            _entries[workflow.DefinitionKey] = workflow with { Version = newVersion };
            return Task.FromResult(new ServiceBlueprintSaveResult(Saved: true, CurrentVersion: newVersion, Location: $"memory://{workflow.DefinitionKey}"));
        }

        public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default) =>
            Task.FromResult(_entries.Remove(definitionKey));
    }
}
