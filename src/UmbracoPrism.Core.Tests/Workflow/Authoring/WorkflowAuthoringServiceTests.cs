using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Calculations;
using UmbracoPrism.WorkflowEditor.Authoring;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class WorkflowAuthoringServiceTests
{
    [Fact]
    public async Task SaveAsync_ValidNewWorkflow_SavesAndReturnsSavedOutcome()
    {
        var store = new InMemoryWorkflowSourceStore();
        var service = new WorkflowAuthoringService(store);
        var workflow = ProjectLinearWorkflow();

        var outcome = await service.SaveAsync(workflow, expectedVersion: 0);

        outcome.Status.Should().Be(WorkflowSaveStatus.Saved);
        outcome.IsSaved.Should().BeTrue();
        outcome.NewVersion.Should().Be(1);
        (await store.LoadAsync(workflow.DefinitionKey))!.Version.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_HostRegisteredStructuralValidatorRejectsIt_RejectsWithoutSaving()
    {
        var store = new InMemoryWorkflowSourceStore();
        var validator = new AlwaysRejectStructuralValidator();
        var service = new WorkflowAuthoringService(store, [validator]);
        var workflow = ProjectLinearWorkflow();

        var outcome = await service.SaveAsync(workflow, expectedVersion: 0);

        outcome.Status.Should().Be(WorkflowSaveStatus.Invalid);
        outcome.Diagnostics.Should().ContainSingle(d => d.Code == "TEST_HOST_RULE");
        (await store.LoadAsync(workflow.DefinitionKey)).Should().BeNull();
    }

    [Fact]
    public void Validate_NoStructuralValidatorsRegistered_DoesNotThrow()
    {
        var store = new InMemoryWorkflowSourceStore();
        var service = new WorkflowAuthoringService(store);

        var outcome = service.Validate(ProjectLinearWorkflow());

        outcome.IsValid.Should().BeTrue();
    }

    private sealed class AlwaysRejectStructuralValidator : IWorkflowStructuralValidator
    {
        public IEnumerable<WorkflowDiagnostic> Validate(WorkflowDefinitionFile workflow)
        {
            yield return new WorkflowDiagnostic("TEST_HOST_RULE", "$", "Rejected by a host-specific rule.");
        }
    }

    [Fact]
    public async Task SaveAsync_StateRoutedDirectlyToAnotherState_RejectsWithoutSaving()
    {
        var store = new InMemoryWorkflowSourceStore();
        var service = new WorkflowAuthoringService(store);
        var workflow = ProjectDirectStateToStateWorkflow();

        var outcome = await service.SaveAsync(workflow, expectedVersion: 0);

        outcome.Status.Should().Be(WorkflowSaveStatus.Invalid);
        outcome.Diagnostics.Should().ContainSingle(d => d.Message.Contains("must always target a gateway"));
        (await store.LoadAsync(workflow.DefinitionKey)).Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_StaleExpectedVersion_ReturnsConflictWithoutSaving()
    {
        var store = new InMemoryWorkflowSourceStore();
        var service = new WorkflowAuthoringService(store);
        var workflow = ProjectLinearWorkflow();

        var first = await service.SaveAsync(workflow, expectedVersion: 0);
        first.Status.Should().Be(WorkflowSaveStatus.Saved);

        // Someone else already saved (version is now 1) — this caller still thinks it's 0.
        var conflicted = await service.SaveAsync(workflow, expectedVersion: 0);

        conflicted.Status.Should().Be(WorkflowSaveStatus.Conflict);
        conflicted.CurrentVersion.Should().Be(1);
        (await store.LoadAsync(workflow.DefinitionKey))!.Version.Should().Be(1,
            because: "the conflicting save must not have overwritten the successful one");
    }

    [Fact]
    public void Validate_CalculationsBlockWithUnknownName_ReturnsError()
    {
        var service = new WorkflowAuthoringService(new InMemoryWorkflowSourceStore());
        var workflow = ProjectLinearWorkflow() with
        {
            Calculations = new WorkflowCalculationSet
            {
                Fields = new Dictionary<string, WorkflowCalculationField>
                {
                    ["broken"] = new WorkflowCalculationField { Expr = "nonExistentInput + 1" }
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
        var store = new InMemoryWorkflowSourceStore();
        var service = new WorkflowAuthoringService(store);
        var workflow = ProjectLinearWorkflow();
        workflow = workflow with
        {
            States = workflow.States
                .Select(s => s.StateKey == "done" ? s with { StageType = "Outcome" } : s)
                .ToList()
        };

        var outcome = await service.SaveAsync(workflow, expectedVersion: 0);

        outcome.Status.Should().Be(WorkflowSaveStatus.Invalid);
        outcome.Diagnostics.Should().ContainSingle(d =>
            d.Code == "STATE_UNKNOWN_STAGE_TYPE" && d.Message.Contains("'Outcome'"));
        (await store.LoadAsync(workflow.DefinitionKey)).Should().BeNull();
    }

    private static WorkflowDefinitionFile ProjectLinearWorkflow()
    {
        var authored = new AuthoredWorkflow
        {
            Id = Guid.NewGuid(),
            DefinitionKey = "authoring-service-valid",
            DisplayName = "Authoring Service Valid",
            Version = 1,
            InitialStageKey = "start",
            InstancePolicy = "single",
            Queues = [new AuthoredQueue { Key = "applicant", DisplayName = "Applicant", Actor = "applicant" }],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "to-done",
                    DisplayName = "Route to done",
                    Kind = GatewayKind.Split,
                    QueueKey = "applicant",
                    Routes = [new AuthoredRoute { Id = "release", Target = "done", Trigger = "submit" }]
                }
            ],
            Stages =
            [
                new AuthoredStage
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Kind = StageKind.Question,
                    QueueKey = "applicant",
                    Routes = [new AuthoredRoute { Id = "start-submit", Target = "to-done", Trigger = "submit" }]
                },
                new AuthoredStage
                {
                    StageKey = "done",
                    DisplayName = "Done",
                    Kind = StageKind.Confirmation,
                    QueueKey = "applicant"
                }
            ]
        };

        return new WorkflowProjector().Project(authored).File;
    }

    private static WorkflowDefinitionFile ProjectDirectStateToStateWorkflow()
    {
        var authored = new AuthoredWorkflow
        {
            Id = Guid.NewGuid(),
            DefinitionKey = "authoring-service-invalid",
            DisplayName = "Authoring Service Invalid",
            Version = 1,
            InitialStageKey = "start",
            InstancePolicy = "single",
            Queues = [new AuthoredQueue { Key = "applicant", DisplayName = "Applicant", Actor = "applicant" }],
            Stages =
            [
                new AuthoredStage
                {
                    StageKey = "start",
                    DisplayName = "Start",
                    Kind = StageKind.Question,
                    QueueKey = "applicant",
                    Routes = [new AuthoredRoute { Id = "start-submit", Target = "done", Trigger = "submit" }]
                },
                new AuthoredStage
                {
                    StageKey = "done",
                    DisplayName = "Done",
                    Kind = StageKind.Confirmation,
                    QueueKey = "applicant"
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
