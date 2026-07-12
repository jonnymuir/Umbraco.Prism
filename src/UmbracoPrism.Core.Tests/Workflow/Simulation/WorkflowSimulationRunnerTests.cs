using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.Workflow;
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

        var result = new WorkflowSimulationRunner().Run(
            projection.File,
            [new WorkflowRuntimeSimulationStep("submit")]);

        result.Trace.Should().HaveCount(2, "the initial GetCurrent plus one Advance step should each produce an envelope");
        result.Trace[0].ResponseState.Should().Be("render", "the workflow starts on the question stage");
        result.Trace[1].ResponseState.Should().Be("complete", "submitting routes through the gateway to the confirmation stage");
        result.Calculations.Should().HaveCount(2);
        result.Calculations.Should().OnlyContain(c => c == null, "this definition has no calculations block");
    }

    [Fact]
    public void Run_MoneyModeller_WithMockServiceInputs_ReturnsRawCalculatedFieldValues()
    {
        var definition = LoadMoneyModeller();
        var mockServiceInputs = new Dictionary<string, object?>
        {
            ["member"] = new Dictionary<string, object?>
            {
                ["name"] = "Dr Sarah Mitchell",
                ["active"] = true,
                ["age"] = 47m,
                ["salary"] = 82_000m,
                ["accruedPension"] = 16_400m,
                ["accruedLump"] = 49_200m,
                ["dcPot"] = 48_300m
            }
        };

        var result = new WorkflowSimulationRunner().Run(
            definition,
            [new WorkflowRuntimeSimulationStep("start-modelling")],
            mockServiceInputs);

        result.Trace.Should().HaveCount(2);
        result.Calculations.Should().HaveCount(2);
        result.Calculations.Should().OnlyContain(c => c != null,
            "with the service field resolved, the whole workflow's calculations block should evaluate cleanly at every step");
        result.Calculations[1]!.Fields["resultPension"].Should().NotBeNull(
            "raw calculated field values should be readable directly, not just baked into rendered UI text");
        result.Calculations[1]!.Series.Should().ContainKey("incomeByAge");
    }

    [Fact]
    public void Run_MoneyModeller_WithNoMockServiceInputs_DoesNotThrow_CalculationsAreNullForEveryStep()
    {
        var definition = LoadMoneyModeller();

        var act = () => new WorkflowSimulationRunner().Run(
            definition,
            [new WorkflowRuntimeSimulationStep("start-modelling")]);

        act.Should().NotThrow(
            "an unresolved service-sourced field should fail calculations cleanly, not blow up the whole simulation");

        var result = act();
        result.Calculations.Should().OnlyContain(c => c == null,
            "the 'member' field is service-sourced and unresolved without mockServiceInputs");
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

    private static WorkflowDefinitionFile LoadMoneyModeller()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src", "UmbracoPrism.MockBusinessApp", "workflow-seeds", "money-modeller.json");
            if (File.Exists(candidate))
            {
                return JsonSerializer.Deserialize<WorkflowDefinitionFile>(
                    File.ReadAllText(candidate),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true,
                        AllowOutOfOrderMetadataProperties = true
                    })!;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("money-modeller.json not found walking up from test bin.");
    }
}
