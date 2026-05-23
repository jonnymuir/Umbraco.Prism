using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Verifies that patch failures surface as diagnostics — never as exceptions.
/// In every error scenario the returned <see cref="PatchResult.Updated"/> must
/// be the unchanged original.
/// </summary>
public class WorkflowPatchServiceFailureTests
{
    private static readonly string FixturesPath = WorkflowAuthoringFixtureLocator.GetFixturesPath();

    private readonly WorkflowProjector _projector = new();
    private readonly WorkflowPatchService _sut;

    public WorkflowPatchServiceFailureTests()
    {
        _sut = new WorkflowPatchService(_projector);
    }

    [Fact]
    public async Task Apply_UnknownOp_ReturnsDiagnosticAndOriginal()
    {
        var original = await LoadPlanningFixture();
        var envelope = BuildEnvelope("delete-everything");

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PATCH001");
        result.Updated.Should().Be(original, because: "on failure the original must be returned");
    }

    [Fact]
    public async Task Apply_InsertStage_MissingValue_ReturnsDiagnostic()
    {
        var original = await LoadPlanningFixture();
        var envelope = BuildEnvelopeRaw("insert-stage"); // no value

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PATCH002");
        result.Updated.Should().Be(original);
    }

    [Fact]
    public async Task Apply_InsertStage_SchemaInvalidValue_ReturnsDiagnostic()
    {
        var original = await LoadPlanningFixture();
        // Value is valid JSON but cannot be parsed as AuthoredStage (missing required stageKey)
        var envelope = BuildEnvelopeWithValue("insert-stage", new { notAStageKey = "oops" });

        var result = _sut.Apply(envelope, original);

        // The deserialized stage will have a null/empty StageKey — triggers PATCH003
        result.HasErrors.Should().BeTrue();
        result.Updated.Should().Be(original);
    }

    [Fact]
    public async Task Apply_InsertStage_BeforeNonExistentStage_ReturnsDiagnostic()
    {
        var original = await LoadPlanningFixture();
        var envelope = BuildEnvelopeWithValue("insert-stage",
            new
            {
                stageKey    = "new-stage",
                displayName = "New Stage",
                kind        = "Question",
                actor       = "applicant",
                fields      = Array.Empty<object>(),
                roleGates   = Array.Empty<string>()
            },
            before: "nonexistent-stage");

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == "PATCH004" && d.Message.Contains("nonexistent-stage"));
        result.Updated.Should().Be(original);
    }

    [Fact]
    public async Task Apply_RemoveStage_MissingTarget_ReturnsDiagnostic()
    {
        var original = await LoadPlanningFixture();
        var envelope = BuildEnvelope("remove-stage", "/stages/does-not-exist");

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == "PATCH006");
        result.Updated.Should().Be(original);
    }

    [Fact]
    public async Task Apply_UpdateStage_MissingTarget_ReturnsDiagnostic()
    {
        var original = await LoadPlanningFixture();
        var envelope = BuildEnvelopeWithValue("update-stage",
            new
            {
                stageKey    = "does-not-exist",
                displayName = "Ghost Stage",
                kind        = "Question",
                actor       = "applicant",
                fields      = Array.Empty<object>(),
                roleGates   = Array.Empty<string>()
            },
            path: "/stages/does-not-exist");

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == "PATCH006" && d.Message.Contains("does-not-exist"));
        result.Updated.Should().Be(original);
    }

    [Fact]
    public async Task Apply_NeverThrows_OnMalformedValue()
    {
        var original = await LoadPlanningFixture();

        // Build a raw op with a value that is not parseable as any authored type
        var malformedOps = new[]
        {
            new PatchOp
            {
                Op    = "insert-stage",
                Value = JsonSerializer.SerializeToElement(42) // integer, not an object
            }
        };

        var envelope = new ProposalEnvelope
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetWorkflowId = "planning-application",
            Rationale        = "Malformed value test",
            Ops              = malformedOps
        };

        var act = () => _sut.Apply(envelope, original);
        act.Should().NotThrow(because: "all errors must be returned as diagnostics, never thrown");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<AuthoredWorkflow> LoadPlanningFixture()
    {
        var store = new FilesystemAuthoredWorkflowStore(FixturesPath);
        var wf    = await store.LoadAsync("planning");
        return wf ?? throw new InvalidOperationException("planning fixture not found");
    }

    private static ProposalEnvelope BuildEnvelope(string op, string? path = null) =>
        new()
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetWorkflowId = "planning-application",
            Rationale        = "Failure test",
            Ops              = [new PatchOp { Op = op, Path = path }]
        };

    private static ProposalEnvelope BuildEnvelopeRaw(string op) =>
        new()
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetWorkflowId = "planning-application",
            Rationale        = "Failure test",
            Ops              = [new PatchOp { Op = op }]
        };

    private static ProposalEnvelope BuildEnvelopeWithValue(
        string op,
        object value,
        string? path   = null,
        string? before = null)
    {
        var valueElement = JsonSerializer.SerializeToElement(value, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return new()
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetWorkflowId = "planning-application",
            Rationale        = "Failure test",
            Ops              = [new PatchOp { Op = op, Path = path, Before = before, Value = valueElement }]
        };
    }

}
