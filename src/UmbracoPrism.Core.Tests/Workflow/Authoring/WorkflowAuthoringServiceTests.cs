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
    public async Task SaveAsync_ValidWorkflow_SavesAndReturnsValidOutcome()
    {
        var store = new InMemoryWorkflowSourceStore();
        var service = new WorkflowAuthoringService(store);
        var workflow = ProjectLinearWorkflow();

        var outcome = await service.SaveAsync(workflow);

        outcome.IsValid.Should().BeTrue();
        outcome.Errors.Should().BeEmpty();
        (await store.LoadAsync(workflow.DefinitionKey)).Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAsync_StateRoutedDirectlyToAnotherState_RejectsWithoutSaving()
    {
        var store = new InMemoryWorkflowSourceStore();
        var service = new WorkflowAuthoringService(store);
        var workflow = ProjectDirectStateToStateWorkflow();

        var outcome = await service.SaveAsync(workflow);

        outcome.IsValid.Should().BeFalse();
        outcome.Errors.Should().ContainSingle(e => e.Contains("must always target a gateway"));
        (await store.LoadAsync(workflow.DefinitionKey)).Should().BeNull();
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
        outcome.Errors.Should().ContainSingle(e => e.Contains("Calculations block failed to evaluate"));
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

        public Task<string> SaveAsync(WorkflowDefinitionFile workflow, CancellationToken ct = default)
        {
            _entries[workflow.DefinitionKey] = workflow;
            return Task.FromResult($"memory://{workflow.DefinitionKey}");
        }
    }
}
