using FluentAssertions;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Calculations;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public class ServiceBlueprintAuthoringServiceTests
{
    [Fact]
    public async Task SaveAsync_ValidNewWorkflow_SavesAndReturnsSavedOutcome()
    {
        var store = new InMemoryServiceBlueprintSourceStore();
        var service = new ServiceBlueprintAuthoringService(store);
        var workflow = ProjectLinearWorkflow();

        var outcome = await service.SaveAsync(workflow, expectedVersion: 0);

        outcome.Status.Should().Be(ServiceBlueprintSaveStatus.Saved);
        outcome.IsSaved.Should().BeTrue();
        outcome.NewVersion.Should().Be(1);
        (await store.LoadAsync(workflow.DefinitionKey))!.Version.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_HostRegisteredStructuralValidatorRejectsIt_RejectsWithoutSaving()
    {
        var store = new InMemoryServiceBlueprintSourceStore();
        var validator = new AlwaysRejectStructuralValidator();
        var service = new ServiceBlueprintAuthoringService(store, [validator]);
        var workflow = ProjectLinearWorkflow();

        var outcome = await service.SaveAsync(workflow, expectedVersion: 0);

        outcome.Status.Should().Be(ServiceBlueprintSaveStatus.Invalid);
        outcome.Diagnostics.Should().ContainSingle(d => d.Code == "TEST_HOST_RULE");
        (await store.LoadAsync(workflow.DefinitionKey)).Should().BeNull();
    }

    [Fact]
    public void Validate_NoStructuralValidatorsRegistered_DoesNotThrow()
    {
        var store = new InMemoryServiceBlueprintSourceStore();
        var service = new ServiceBlueprintAuthoringService(store);

        var outcome = service.Validate(ProjectLinearWorkflow());

        outcome.IsValid.Should().BeTrue();
    }

    private sealed class AlwaysRejectStructuralValidator : IServiceBlueprintStructuralValidator
    {
        public IEnumerable<ServiceBlueprintDiagnostic> Validate(ServiceBlueprint workflow)
        {
            yield return new ServiceBlueprintDiagnostic("TEST_HOST_RULE", "$", "Rejected by a host-specific rule.");
        }
    }

    [Fact]
    public async Task SaveAsync_StateRoutedDirectlyToAnotherState_RejectsWithoutSaving()
    {
        var store = new InMemoryServiceBlueprintSourceStore();
        var service = new ServiceBlueprintAuthoringService(store);
        var workflow = ProjectDirectStateToStateWorkflow();

        var outcome = await service.SaveAsync(workflow, expectedVersion: 0);

        outcome.Status.Should().Be(ServiceBlueprintSaveStatus.Invalid);
        outcome.Diagnostics.Should().ContainSingle(d => d.Message.Contains("must always target a gateway"));
        (await store.LoadAsync(workflow.DefinitionKey)).Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_StaleExpectedVersion_ReturnsConflictWithoutSaving()
    {
        var store = new InMemoryServiceBlueprintSourceStore();
        var service = new ServiceBlueprintAuthoringService(store);
        var workflow = ProjectLinearWorkflow();

        var first = await service.SaveAsync(workflow, expectedVersion: 0);
        first.Status.Should().Be(ServiceBlueprintSaveStatus.Saved);

        // Someone else already saved (version is now 1) — this caller still thinks it's 0.
        var conflicted = await service.SaveAsync(workflow, expectedVersion: 0);

        conflicted.Status.Should().Be(ServiceBlueprintSaveStatus.Conflict);
        conflicted.CurrentVersion.Should().Be(1);
        (await store.LoadAsync(workflow.DefinitionKey))!.Version.Should().Be(1,
            because: "the conflicting save must not have overwritten the successful one");
    }

    [Fact]
    public void Validate_CalculationsBlockWithUnknownName_ReturnsError()
    {
        var service = new ServiceBlueprintAuthoringService(new InMemoryServiceBlueprintSourceStore());
        var workflow = ProjectLinearWorkflow() with
        {
            Calculations = new ServiceBlueprintCalculationSet
            {
                Fields = new Dictionary<string, ServiceBlueprintCalculationField>
                {
                    ["broken"] = new ServiceBlueprintCalculationField { Expr = "nonExistentInput + 1" }
                }
            }
        };

        var outcome = service.Validate(workflow);

        outcome.IsValid.Should().BeFalse();
        outcome.Diagnostics.Should().ContainSingle(d => d.Code == "CALC_FIELD_ERROR" && d.Message.Contains("Unknown name"));
    }

    [Fact]
    public async Task SaveAsync_StateWithUnrecognisedStageType_RejectsWithoutSaving()
    {
        // Regression coverage: an MCP-authored workflow once saved successfully with
        // stageType "Outcome" — a value no authoring surface actually recognises — because
        // nothing in this pipeline checked it. The backoffice editor's own client-side lint
        // was the only thing that ever caught it, so the invalid save reached persistence
        // first and only surfaced as a confusing error much later when someone opened the
        // workflow in the editor.
        var store = new InMemoryServiceBlueprintSourceStore();
        var service = new ServiceBlueprintAuthoringService(store);
        var workflow = ProjectLinearWorkflow();
        workflow = workflow with
        {
            Stages = workflow.Stages
                .Select(s => s.StageKey == "done" ? s with { StageType = "Outcome" } : s)
                .ToList()
        };

        var outcome = await service.SaveAsync(workflow, expectedVersion: 0);

        outcome.Status.Should().Be(ServiceBlueprintSaveStatus.Invalid);
        outcome.Diagnostics.Should().ContainSingle(d =>
            d.Code == "STAGE_UNKNOWN_TYPE" && d.Message.Contains("'Outcome'"));
        (await store.LoadAsync(workflow.DefinitionKey)).Should().BeNull();
    }

    private static ServiceBlueprint ProjectLinearWorkflow() => new()
    {
        DefinitionKey = "authoring-service-valid",
        DisplayName = "Authoring Service Valid",
        Version = 1,
        InitialStage = "start",
        RequestPolicy = "single",
        Queues = [new QueueDefinition { Key = "applicant", DisplayName = "Applicant", Actor = "applicant" }],
        Gateways =
        [
            new ServiceBlueprintGatewayDefinition
            {
                Key = "to-done",
                DisplayName = "Route to done",
                GatewayType = "Split",
                QueueKey = "applicant",
                Routes = [new ServiceBlueprintRouteDefinition { Id = "release", Target = "done", Trigger = "submit" }]
            }
        ],
        Stages =
        [
            new StageDefinition
            {
                StageKey = "start",
                DisplayName = "Start",
                StageType = "Question",
                QueueKey = "applicant",
                Routes = [new ServiceBlueprintRouteDefinition { Id = "start-submit", Target = "to-done", Trigger = "submit" }]
            },
            new StageDefinition
            {
                StageKey = "done",
                DisplayName = "Done",
                StageType = "Confirmation",
                QueueKey = "applicant"
            }
        ]
    };

    private static ServiceBlueprint ProjectDirectStateToStateWorkflow() => new()
    {
        DefinitionKey = "authoring-service-invalid",
        DisplayName = "Authoring Service Invalid",
        Version = 1,
        InitialStage = "start",
        RequestPolicy = "single",
        Queues = [new QueueDefinition { Key = "applicant", DisplayName = "Applicant", Actor = "applicant" }],
        Stages =
        [
            new StageDefinition
            {
                StageKey = "start",
                DisplayName = "Start",
                StageType = "Question",
                QueueKey = "applicant",
                Routes = [new ServiceBlueprintRouteDefinition { Id = "start-submit", Target = "done", Trigger = "submit" }]
            },
            new StageDefinition
            {
                StageKey = "done",
                DisplayName = "Done",
                StageType = "Confirmation",
                QueueKey = "applicant"
            }
        ]
    };

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
