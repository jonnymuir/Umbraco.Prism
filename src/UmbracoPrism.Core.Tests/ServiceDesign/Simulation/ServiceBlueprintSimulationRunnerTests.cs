using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;
using UmbracoPrism.ProcessManager.Services;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Simulation;

public class ServiceBlueprintSimulationRunnerTests
{
    [Fact]
    public void Run_LinearWorkflow_WalksThroughGatewayToCompletion()
    {
        var projection = new ServiceBlueprintProjector().Project(BuildLinearWorkflow());
        projection.HasErrors.Should().BeFalse();

        var result = new ServiceBlueprintSimulationRunner().Run(
            projection.File,
            [new ProcessManagerSimulationStep("submit")]);

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

        var result = new ServiceBlueprintSimulationRunner().Run(
            definition,
            [new ProcessManagerSimulationStep("start-modelling")],
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

        var act = () => new ServiceBlueprintSimulationRunner().Run(
            definition,
            [new ProcessManagerSimulationStep("start-modelling")]);

        act.Should().NotThrow(
            "an unresolved service-sourced field should fail calculations cleanly, not blow up the whole simulation");

        var result = act();
        result.Calculations.Should().OnlyContain(c => c == null,
            "the 'member' field is service-sourced and unresolved without mockServiceInputs");
    }

    private static AuthoredServiceBlueprint BuildLinearWorkflow() => new()
    {
        Id = Guid.NewGuid(),
        DefinitionKey = "simulation-smoke-test",
        DisplayName = "Simulation Smoke Test",
        Version = 1,
        InitialTouchpointKey = "start",
        RequestPolicy = "single",
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
        Touchpoints =
        [
            new AuthoredTouchpoint
            {
                TouchpointKey = "start",
                DisplayName = "Start",
                Kind = TouchpointKind.Question,
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "start-submit", Target = "to-done", Trigger = "submit" }]
            },
            new AuthoredTouchpoint
            {
                TouchpointKey = "done",
                DisplayName = "Done",
                Kind = TouchpointKind.Confirmation,
                QueueKey = "applicant"
            }
        ]
    };

    private static ServiceBlueprint LoadMoneyModeller()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src", "UmbracoPrism.MockBusinessApp", "service-blueprints", "money-modeller.json");
            if (File.Exists(candidate))
            {
                return JsonSerializer.Deserialize<ServiceBlueprint>(
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
