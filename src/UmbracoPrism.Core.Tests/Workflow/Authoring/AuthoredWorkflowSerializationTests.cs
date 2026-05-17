using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Verifies that <see cref="AuthoredWorkflow"/> and its graph types round-trip
/// through System.Text.Json without data loss, and that the filesystem store
/// loads fixture documents correctly.
/// </summary>
public class AuthoredWorkflowSerializationTests
{
    private static readonly JsonSerializerOptions RoundTripOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void AuthoredWorkflow_RoundTripsWithoutDataLoss()
    {
        var original = BuildTestWorkflow();

        var json = JsonSerializer.Serialize(original, RoundTripOptions);
        var restored = JsonSerializer.Deserialize<AuthoredWorkflow>(json, RoundTripOptions)!;

        restored.DefinitionKey.Should().Be(original.DefinitionKey);
        restored.DisplayName.Should().Be(original.DisplayName);
        restored.Version.Should().Be(original.Version);
        restored.InitialStageKey.Should().Be(original.InitialStageKey);
        restored.Description.Should().Be(original.Description);
        restored.SchemaVersion.Should().Be(original.SchemaVersion);

        restored.Stages.Should().HaveCount(2);
        restored.Stages[0].StageKey.Should().Be("details");
        restored.Stages[0].Kind.Should().Be(StageKind.Question);
        restored.Stages[0].Fields.Should().HaveCount(1);
        restored.Stages[0].Fields[0].Key.Should().Be("full-name");
        restored.Stages[0].Fields[0].Type.Should().Be(FieldType.Text);
        restored.Stages[0].Fields[0].Required.Should().BeTrue();

        restored.Stages[1].Kind.Should().Be(StageKind.Confirmation);

        restored.Transitions.Should().HaveCount(1);
        restored.Transitions[0].FromStage.Should().Be("details");
        restored.Transitions[0].ToStage.Should().Be("done");
        restored.Transitions[0].Action.Should().Be("submit");

        restored.Handoffs.Should().HaveCount(1);
        restored.Handoffs[0].Id.Should().Be("h1");
        restored.Handoffs[0].Label.Should().Be("applicant-to-caseworker");

        restored.Metadata.Should().ContainKey("owner").WhoseValue.Should().Be("test-team");
    }

    [Fact]
    public void AuthoredField_AllTypesRoundTrip()
    {
        var allTypes = Enum.GetValues<FieldType>();

        foreach (var fieldType in allTypes)
        {
            var field = new AuthoredField { Key = "f", Label = "F", Type = fieldType };
            var json = JsonSerializer.Serialize(field, RoundTripOptions);
            var restored = JsonSerializer.Deserialize<AuthoredField>(json, RoundTripOptions)!;
            restored.Type.Should().Be(fieldType, $"FieldType.{fieldType} should round-trip");
        }
    }

    [Fact]
    public void AllStageKinds_RoundTrip()
    {
        var allKinds = Enum.GetValues<StageKind>();

        foreach (var kind in allKinds)
        {
            var stage = new AuthoredStage { StageKey = "s", DisplayName = "S", Kind = kind };
            var json = JsonSerializer.Serialize(stage, RoundTripOptions);
            var restored = JsonSerializer.Deserialize<AuthoredStage>(json, RoundTripOptions)!;
            restored.Kind.Should().Be(kind, $"StageKind.{kind} should round-trip");
        }
    }

    [Fact]
    public async Task FilesystemStore_LoadsFixtureDocument()
    {
        var fixturesPath = GetFixturesPath();
        var store = new FilesystemAuthoredWorkflowStore(fixturesPath);

        var workflow = await store.LoadAsync("planning");

        workflow.Should().NotBeNull();
        workflow!.DefinitionKey.Should().Be("planning-application");
        workflow.Stages.Should().HaveCount(4);
        workflow.Stages.Should().Contain(s => s.StageKey == "declaration");
        workflow.Stages.Should().Contain(s => s.StageKey == "check-answers");
    }

    [Fact]
    public async Task FilesystemStore_ListKeys_ReturnsFixtureKey()
    {
        var store = new FilesystemAuthoredWorkflowStore(GetFixturesPath());

        var keys = await store.ListKeysAsync();

        keys.Should().Contain("planning");
    }

    [Fact]
    public async Task FilesystemStore_ReturnsNull_ForMissingKey()
    {
        var store = new FilesystemAuthoredWorkflowStore(GetFixturesPath());

        var result = await store.LoadAsync("does-not-exist");

        result.Should().BeNull();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AuthoredWorkflow BuildTestWorkflow() => new()
    {
        Id = new Guid("aaaabbbb-cccc-dddd-eeee-ffffffffffff"),
        DefinitionKey = "test-workflow",
        DisplayName = "Test Workflow",
        Version = 1,
        Description = "A minimal workflow for serialization tests.",
        SchemaVersion = "1.0",
        InitialStageKey = "details",
        InstancePolicy = "single",
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "details",
                DisplayName = "Your details",
                Kind = StageKind.Question,
                Fields =
                [
                    new AuthoredField
                    {
                        Key = "full-name",
                        Label = "Full name",
                        Type = FieldType.Text,
                        Required = true
                    }
                ]
            },
            new AuthoredStage
            {
                StageKey = "done",
                DisplayName = "Complete",
                Kind = StageKind.Confirmation
            }
        ],
        Transitions =
        [
            new AuthoredTransition { FromStage = "details", ToStage = "done", Action = "submit" }
        ],
        Handoffs =
        [
            new AuthoredHandoff
            {
                Id = "h1",
                FromStage = "details",
                ToStage = "done",
                Label = "applicant-to-caseworker"
            }
        ],
        Metadata = new Dictionary<string, string> { ["owner"] = "test-team" }
    };

    private static string GetFixturesPath()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Workflow", "Authoring", "Fixtures");

        return path;
    }
}
