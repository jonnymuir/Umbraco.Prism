using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.WorkflowEditor.Authoring;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class WorkflowAuthoringServiceQueueCapabilitiesTests
{
    [Fact]
    public void Validate_NoProviderRegistered_IsUnaffected()
    {
        var service = new WorkflowAuthoringService(new InMemoryWorkflowSourceStore());

        var outcome = service.Validate(ProjectWorkflowWithFieldsetOnBusinessUser());

        outcome.IsValid.Should().BeTrue();
        outcome.Diagnostics.Should().NotContain(d => d.Code == "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT");
    }

    [Fact]
    public void Validate_ProviderRegistered_QueueUndeclared_IsUnrestricted()
    {
        var provider = new StaticQueueCapabilitiesProvider(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
        var service = new WorkflowAuthoringService(new InMemoryWorkflowSourceStore(), queueCapabilities: provider);

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
        var service = new WorkflowAuthoringService(new InMemoryWorkflowSourceStore(), queueCapabilities: provider);

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
        var service = new WorkflowAuthoringService(new InMemoryWorkflowSourceStore(), queueCapabilities: provider);

        var outcome = service.Validate(ProjectWorkflowWithFieldsetOnBusinessUser());

        outcome.IsValid.Should().BeFalse();
        var matches = outcome.Diagnostics.Where(d => d.Code == "QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT").ToList();
        matches.Should().HaveCount(2, because: "both the fieldset wrapper and its text child are unsupported");
    }

    [Fact]
    public void GetQueueCapabilities_NoProviderRegistered_ReturnsEmpty()
    {
        var service = new WorkflowAuthoringService(new InMemoryWorkflowSourceStore());

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
        var service = new WorkflowAuthoringService(new InMemoryWorkflowSourceStore(), queueCapabilities: provider);

        var capabilities = service.GetQueueCapabilities();

        capabilities.Should().ContainKey("business-user");
        capabilities["business-user"].Should().BeEquivalentTo(new[] { "text", "decimal" });
    }

    private static WorkflowDefinitionFile ProjectWorkflowWithFieldsetOnBusinessUser()
    {
        var authored = new AuthoredWorkflow
        {
            Id = Guid.NewGuid(),
            DefinitionKey = "queue-capabilities-test",
            DisplayName = "Queue Capabilities Test",
            Version = 1,
            InitialStageKey = "review",
            InstancePolicy = "single",
            Queues = [new AuthoredQueue { Key = "business-user", DisplayName = "Business User", Actor = "reviewer" }],
            Stages =
            [
                new AuthoredStage
                {
                    StageKey = "review",
                    DisplayName = "Review",
                    Kind = StageKind.Question,
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

        return new WorkflowProjector().Project(authored).File;
    }

    private sealed class InMemoryWorkflowSourceStore : IWorkflowSourceStore
    {
        private readonly Dictionary<string, WorkflowDefinitionFile> _entries = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<WorkflowSourceSummary>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowSourceSummary>>(
                _entries.Values.Select(w => new WorkflowSourceSummary(w.DefinitionKey, w.DisplayName)).ToArray());

        public Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default) =>
            Task.FromResult(_entries.TryGetValue(definitionKey, out var workflow) ? workflow : null);

        public Task<WorkflowSaveResult> SaveAsync(WorkflowDefinitionFile workflow, int expectedVersion, CancellationToken ct = default)
        {
            var currentVersion = _entries.TryGetValue(workflow.DefinitionKey, out var existing) ? existing.Version : 0;
            if (currentVersion != expectedVersion)
            {
                return Task.FromResult(new WorkflowSaveResult(Saved: false, CurrentVersion: currentVersion, Location: $"memory://{workflow.DefinitionKey}"));
            }

            var newVersion = expectedVersion + 1;
            _entries[workflow.DefinitionKey] = workflow with { Version = newVersion };
            return Task.FromResult(new WorkflowSaveResult(Saved: true, CurrentVersion: newVersion, Location: $"memory://{workflow.DefinitionKey}"));
        }

        public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default) =>
            Task.FromResult(_entries.Remove(definitionKey));
    }
}
