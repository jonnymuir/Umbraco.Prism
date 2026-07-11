using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Tests.Workflow.Simulation;

public class WorkflowSimulationRunnerTests
{
    [Fact]
    public void Run_LinearWorkflow_WalksThroughGatewayToCompletion()
    {
        var projection = new WorkflowProjector().Project(BuildLinearWorkflow());
        projection.HasErrors.Should().BeFalse();

        var trace = new WorkflowSimulationRunner().Run(
            projection.File,
            [new WorkflowRuntimeSimulationStep("submit")]);

        trace.Should().HaveCount(2, "the initial GetCurrent plus one Advance step should each produce an envelope");
        trace[0].ResponseState.Should().Be("render", "the workflow starts on the question stage");
        trace[1].ResponseState.Should().Be("complete", "submitting routes through the gateway to the confirmation stage");
    }

    private static AuthoredWorkflow BuildLinearWorkflow() => new()
    {
        Id = Guid.NewGuid(),
        DefinitionKey = "simulation-smoke-test",
        DisplayName = "Simulation Smoke Test",
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
}
