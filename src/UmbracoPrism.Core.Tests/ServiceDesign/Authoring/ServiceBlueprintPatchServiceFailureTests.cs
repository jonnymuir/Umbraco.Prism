using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Verifies that patch failures surface as diagnostics — never as exceptions.
/// In every error scenario the returned <see cref="PatchResult.Updated"/> must
/// be the unchanged original.
/// </summary>
public class ServiceBlueprintPatchServiceFailureTests
{
    private static readonly string FixturesPath = ServiceBlueprintAuthoringFixtureLocator.GetFixturesPath();

    private readonly ServiceBlueprintProjector _projector = new();
    private readonly ServiceBlueprintPatchService _sut;

    public ServiceBlueprintPatchServiceFailureTests()
    {
        _sut = new ServiceBlueprintPatchService(_projector);
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
        // Value is valid JSON but cannot be parsed as AuthoredTouchpoint (missing required stageKey)
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
                key         = "new-stage",
                title       = "New Stage",
                type        = "Question",
                actor       = "applicant",
                components  = Array.Empty<object>(),
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
                key         = "does-not-exist",
                title       = "Ghost Stage",
                type        = "Question",
                actor       = "applicant",
                components  = Array.Empty<object>(),
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
            TargetServiceBlueprintId = "planning-application",
            Rationale        = "Malformed value test",
            Ops              = malformedOps
        };

        var act = () => _sut.Apply(envelope, original);
        act.Should().NotThrow(because: "all errors must be returned as diagnostics, never thrown");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<AuthoredServiceBlueprint> LoadPlanningFixture()
    {
        var wf = await AuthoredServiceBlueprintFixtureLoader.LoadAsync(FixturesPath, "planning");
        return wf ?? throw new InvalidOperationException("planning fixture not found");
    }

    private static ProposalEnvelope BuildEnvelope(string op, string? path = null) =>
        new()
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetServiceBlueprintId = "planning-application",
            Rationale        = "Failure test",
            Ops              = [new PatchOp { Op = op, Path = path }]
        };

    private static ProposalEnvelope BuildEnvelopeRaw(string op) =>
        new()
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetServiceBlueprintId = "planning-application",
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
            TargetServiceBlueprintId = "planning-application",
            Rationale        = "Failure test",
            Ops              = [new PatchOp { Op = op, Path = path, Before = before, Value = valueElement }]
        };
    }

}
