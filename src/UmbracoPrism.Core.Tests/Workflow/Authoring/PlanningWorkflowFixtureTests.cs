using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class PlanningWorkflowFixtureTests
{
    private static readonly JsonSerializerOptions RoundTripOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory,
        "Workflow", "Authoring", "Fixtures", "planning.workflow.json");

    [Fact]
    public void Fixture_ExistsOnDisk()
    {
        File.Exists(FixturePath).Should().BeTrue();
    }

    [Fact]
    public void Fixture_ParsesWithoutError()
    {
        var json = File.ReadAllText(FixturePath);

        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();
    }

    [Fact]
    public void Fixture_ContainsExpectedStagesTransitionsAndActionSchemas()
    {
        var workflow = JsonSerializer.Deserialize<AuthoredWorkflow>(File.ReadAllText(FixturePath), RoundTripOptions);

        workflow.Should().NotBeNull();
        workflow!.Stages.Select(stage => stage.StageKey).Should().Contain([
            "declaration",
            "application-form",
            "check-answers",
            "submitted"
        ]);
        workflow.Gateways.SelectMany(g => g.Routes)
            .Should().ContainSingle(r => r.Trigger == "submit" && r.Condition != null && r.Actions.Count == 1);
        workflow.ParameterSchemas.Should().ContainSingle(s => s.Key == "forms-form-definition");
    }

    [Fact]
    public void Fixture_RoundTrips_PreservingAuthoringContracts()
    {
        var original = JsonSerializer.Deserialize<AuthoredWorkflow>(File.ReadAllText(FixturePath), RoundTripOptions)!;
        var json = JsonSerializer.Serialize(original, RoundTripOptions);
        var restored = JsonSerializer.Deserialize<AuthoredWorkflow>(json, RoundTripOptions)!;

        restored.ParameterSchemas.Should().BeEquivalentTo(original.ParameterSchemas);
        restored.Stages.Select(stage => stage.StageKey).Should().Equal(original.Stages.Select(stage => stage.StageKey));
        restored.Stages.SelectMany(stage => stage.Actions).Select(action => action.Type)
            .Should().Equal(original.Stages.SelectMany(stage => stage.Actions).Select(action => action.Type));
        restored.Gateways.SelectMany(g => g.Routes).Select(r => r.Trigger)
            .Should().Equal(original.Gateways.SelectMany(g => g.Routes).Select(r => r.Trigger));
        restored.Gateways.SelectMany(g => g.Routes)
            .Single(r => r.Trigger == "submit")
            .Actions[0].Parameters["formDefinitionId"]!.GetValue<string>()
            .Should().Be("planning-application");
    }
}
