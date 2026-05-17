using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Locks the determinism guarantee: <see cref="WorkflowProjector.Project"/> must produce
/// byte-identical output and an identical SHA-256 checksum on every invocation for the same input.
/// </summary>
public class WorkflowProjectorDeterminismTests
{
    private readonly WorkflowProjector _projector = new();

    [Fact]
    public void Project_SameInput_ProducesByteIdenticalOutput()
    {
        var authored = BuildDeterministicWorkflow();

        var result1 = _projector.Project(authored);
        var result2 = _projector.Project(authored);

        // Checksums must be identical
        result1.Checksum.Should().Be(result2.Checksum,
            "identical inputs must produce identical SHA-256 checksums");

        // Independently serialize the WorkflowDefinitionFile and compare raw bytes
        var bytes1 = JsonSerializer.SerializeToUtf8Bytes(result1.File, WorkflowProjector.CanonicalOptions);
        var bytes2 = JsonSerializer.SerializeToUtf8Bytes(result2.File, WorkflowProjector.CanonicalOptions);

        bytes1.Should().Equal(bytes2,
            "identical inputs must produce byte-identical projected JSON");
    }

    [Fact]
    public void Project_ChecksumIsHexEncodedSha256()
    {
        var result = _projector.Project(BuildDeterministicWorkflow());

        // SHA-256 hex string is always 64 lowercase hex characters
        result.Checksum.Should().MatchRegex("^[0-9a-f]{64}$",
            "checksum must be a lowercase hex-encoded SHA-256 digest");
    }

    [Fact]
    public async Task Project_PlanningFixture_IsDeterministic()
    {
        var fixturesPath = GetFixturesPath();
        var store = new FilesystemAuthoredWorkflowStore(fixturesPath);

        var authored = await store.LoadAsync("planning");
        authored.Should().NotBeNull("planning fixture must exist");

        var result1 = _projector.Project(authored!);
        var result2 = _projector.Project(authored!);

        result1.Checksum.Should().Be(result2.Checksum);
    }

    [Fact]
    public void Project_NormalisesStageOrder_BeforeEmitting()
    {
        // Build workflow with stages in reverse alphabetical order to prove normalisation sorts them
        var authored = new AuthoredWorkflow
        {
            Id = new Guid("aaaabbbb-0000-0000-0000-000000000001"),
            DefinitionKey = "order-test",
            DisplayName = "Order Test",
            Version = 1,
            InitialStageKey = "alpha",
            Stages =
            [
                new AuthoredStage { StageKey = "zeta", DisplayName = "Zeta", Kind = StageKind.Confirmation },
                new AuthoredStage { StageKey = "alpha", DisplayName = "Alpha", Kind = StageKind.Question }
            ],
            Transitions = []
        };

        var result = _projector.Project(authored);

        result.File.States.Select(s => s.StateKey)
            .Should().ContainInOrder(new[] { "alpha", "zeta" },
                because: "stages must be emitted in ordinal StageKey order regardless of authored order");
    }

    [Fact]
    public void Project_NormalisesTransitionOrder_BeforeEmitting()
    {
        var authored = new AuthoredWorkflow
        {
            Id = new Guid("aaaabbbb-0000-0000-0000-000000000002"),
            DefinitionKey = "transition-order-test",
            DisplayName = "Transition Order Test",
            Version = 1,
            InitialStageKey = "a",
            Stages =
            [
                new AuthoredStage { StageKey = "a", DisplayName = "A", Kind = StageKind.Question },
                new AuthoredStage { StageKey = "b", DisplayName = "B", Kind = StageKind.Confirmation },
                new AuthoredStage { StageKey = "c", DisplayName = "C", Kind = StageKind.Confirmation }
            ],
            Transitions =
            [
                new AuthoredTransition { FromStage = "a", ToStage = "c", Action = "skip" },
                new AuthoredTransition { FromStage = "a", ToStage = "b", Action = "continue" }
            ]
        };

        var result = _projector.Project(authored);

        result.File.Transitions.Select(t => t.ToState)
            .Should().ContainInOrder(new[] { "b", "c" },
                because: "transitions must be emitted sorted by (FromStage, ToStage, Action)");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AuthoredWorkflow BuildDeterministicWorkflow() => new()
    {
        Id = new Guid("12345678-1234-1234-1234-123456789012"),
        DefinitionKey = "determinism-test",
        DisplayName = "Determinism Test",
        Version = 1,
        Description = "Stable workflow used by determinism tests.",
        SchemaVersion = "1.0",
        InitialStageKey = "collect",
        InstancePolicy = "single",
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "collect",
                DisplayName = "Collect details",
                Kind = StageKind.Question,
                Fields =
                [
                    new AuthoredField { Key = "email", Label = "Email address", Type = FieldType.Email, Required = true },
                    new AuthoredField { Key = "name", Label = "Full name", Type = FieldType.Text, Required = true }
                ]
            },
            new AuthoredStage
            {
                StageKey = "review",
                DisplayName = "Check your answers",
                Kind = StageKind.CheckAnswers
            },
            new AuthoredStage
            {
                StageKey = "done",
                DisplayName = "Application submitted",
                Kind = StageKind.Confirmation
            }
        ],
        Transitions =
        [
            new AuthoredTransition { FromStage = "collect", ToStage = "review", Action = "continue" },
            new AuthoredTransition { FromStage = "review", ToStage = "done", Action = "submit" }
        ],
        Handoffs = [],
        Metadata = new Dictionary<string, string> { ["env"] = "test" }
    };

    private static string GetFixturesPath() =>
        Path.Combine(AppContext.BaseDirectory, "Workflow", "Authoring", "Fixtures");
}
