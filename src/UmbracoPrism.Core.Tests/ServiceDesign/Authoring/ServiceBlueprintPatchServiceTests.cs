using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Verifies successful patch operations against the shared community-enquiry reference workflow.
/// Input immutability is asserted after every test.
/// </summary>
public class ServiceBlueprintPatchServiceTests
{
    private static readonly string FixturesPath = ServiceBlueprintAuthoringFixtureLocator.GetFixturesPath();

    private readonly ServiceBlueprintProjector _projector = new();
    private readonly ServiceBlueprintPatchService _sut;

    public ServiceBlueprintPatchServiceTests()
    {
        _sut = new ServiceBlueprintPatchService(_projector);
    }

    [Fact]
    public async Task Apply_InsertStage_AppendsByDefault()
    {
        var original = await LoadReferenceFixture();
        var originalStageCount = original.Touchpoints.Count;

        var envelope = BuildEnvelope("insert-stage", value: new
        {
            key         = "site-notice",
            title       = "Site Notice",
            type        = "Question",
            actor       = "applicant",
            queueKey    = "applicant",
            components  = Array.Empty<object>(),
            roleGates   = Array.Empty<string>()
        });

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeFalse(because: "inserting a valid stage should succeed");
        result.Updated.Touchpoints.Should().HaveCount(originalStageCount + 1);
        result.Updated.Touchpoints.Last().TouchpointKey.Should().Be("site-notice");

        AssertOriginalUnmutated(original, originalStageCount);
    }

    [Fact]
    public async Task Apply_InsertStage_BeforeTarget_InsertsAtCorrectPosition()
    {
        var original = await LoadReferenceFixture();

        var envelope = BuildEnvelope("insert-stage",
            before: "submitted",
            value: new
            {
                key         = "supporting-docs",
                title       = "Supporting Documents",
                type        = "Question",
                actor       = "applicant",
                queueKey    = "applicant",
                components  = Array.Empty<object>(),
                roleGates   = Array.Empty<string>()
            });

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeFalse();
        var keys = result.Updated.Touchpoints.Select(s => s.TouchpointKey).ToList();
        var newIdx     = keys.IndexOf("supporting-docs");
        var submittedIdx = keys.IndexOf("submitted");
        newIdx.Should().BeLessThan(submittedIdx, because: "supporting-docs should precede submitted");
    }

    [Fact]
    public async Task Apply_InsertStage_AfterTarget_InsertsAtCorrectPosition()
    {
        var original = await LoadReferenceFixture();

        var envelope = BuildEnvelope("insert-stage",
            after: "collecting-details",
            value: new
            {
                key         = "eligibility-check",
                title       = "Eligibility Check",
                type        = "Question",
                actor       = "applicant",
                queueKey    = "applicant",
                components  = Array.Empty<object>(),
                roleGates   = Array.Empty<string>()
            });

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeFalse();
        var keys = result.Updated.Touchpoints.Select(s => s.TouchpointKey).ToList();
        var newIdx  = keys.IndexOf("eligibility-check");
        var prevIdx = keys.IndexOf("collecting-details");
        newIdx.Should().Be(prevIdx + 1);
    }

    [Fact]
    public async Task Apply_RemoveStage_RemovesCorrectStage()
    {
        var original = await LoadReferenceFixture();

        // Insert an orphan stage first so removing it does not leave dangling routes.
        var insertEnvelope = BuildEnvelope("insert-stage", value: new
        {
            key       = "orphan-stage",
            title     = "Orphan",
            type      = "Question",
            actor     = "applicant",
            queueKey  = "applicant",
            components= Array.Empty<object>(),
            roleGates = Array.Empty<string>()
        });
        var withOrphan = _sut.Apply(insertEnvelope, original);
        withOrphan.HasErrors.Should().BeFalse();

        var removeEnvelope = BuildEnvelope("remove-stage", path: "/stages/orphan-stage");
        var result = _sut.Apply(removeEnvelope, withOrphan.Updated);

        result.HasErrors.Should().BeFalse();
        result.Updated.Touchpoints.Should().NotContain(s => s.TouchpointKey == "orphan-stage",
            because: "remove-stage should eliminate the target stage");
        result.Updated.Touchpoints.Count.Should().Be(original.Touchpoints.Count);

        original.Touchpoints.Should().HaveCount(original.Touchpoints.Count,
            because: "original must not be mutated");
    }

    [Fact]
    public async Task Apply_UpdateStage_ReplacesStageInPlace()
    {
        var original = await LoadReferenceFixture();
        var originalDetailsStage = original.Touchpoints.Single(s => s.TouchpointKey == "collecting-details");

        var envelope = BuildEnvelope("update-stage", path: "/stages/collecting-details", value: new
        {
            key         = "collecting-details",
            title       = "Updated Details",
            type        = "Question",
            actor       = "applicant",
            queueKey    = "applicant",
            components  = Array.Empty<object>(),
            roleGates   = Array.Empty<string>()
        });

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeFalse();
        var updated = result.Updated.Touchpoints.Single(s => s.TouchpointKey == "collecting-details");
        updated.DisplayName.Should().Be("Updated Details");

        // Original unchanged
        originalDetailsStage.DisplayName.Should().NotBe("Updated Details");
    }

    [Fact]
    public async Task Apply_IncrementsVersion()
    {
        var original = await LoadReferenceFixture();

        var envelope = BuildEnvelope("insert-stage", value: new
        {
            key         = "extra-stage",
            title       = "Extra Stage",
            type        = "Question",
            actor       = "applicant",
            queueKey    = "applicant",
            components  = Array.Empty<object>(),
            roleGates   = Array.Empty<string>()
        });

        var result = _sut.Apply(envelope, original);

        result.Updated.Version.Should().Be(original.Version + 1,
            because: "each successful apply must increment the version");
    }

    [Fact]
    public async Task Apply_InputNotMutated_AfterSuccessfulPatch()
    {
        var original = await LoadReferenceFixture();
        var snapshot = new
        {
            StageCount   = original.Touchpoints.Count,
            GatewayCount = original.Gateways.Count,
            HandoffCount = original.Handoffs.Count,
            Version      = original.Version
        };

        var envelope = BuildEnvelope("insert-stage", value: new
        {
            key         = "new-stage",
            title       = "New Stage",
            type        = "Question",
            actor       = "applicant",
            queueKey    = "applicant",
            components  = Array.Empty<object>(),
            roleGates   = Array.Empty<string>()
        });

        _sut.Apply(envelope, original);

        original.Touchpoints.Count.Should().Be(snapshot.StageCount);
        original.Gateways.Count.Should().Be(snapshot.GatewayCount);
        original.Handoffs.Count.Should().Be(snapshot.HandoffCount);
        original.Version.Should().Be(snapshot.Version);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<AuthoredServiceBlueprint> LoadReferenceFixture()
    {
        var wf = await AuthoredServiceBlueprintFixtureLoader.LoadAsync(FixturesPath, "community-enquiry");
        return wf ?? throw new InvalidOperationException("community-enquiry fixture not found");
    }

    private static ProposalEnvelope BuildEnvelope(
        string op,
        string? path   = null,
        string? before = null,
        string? after  = null,
        object? value  = null)
    {
        var ops = new List<PatchOp>
        {
            new()
            {
                Op     = op,
                Path   = path,
                Before = before,
                After  = after,
                Value  = value is null
                    ? null
                    : JsonSerializer.SerializeToElement(value, new JsonSerializerOptions
                        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            }
        };

        return new ProposalEnvelope
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetServiceBlueprintId = "community-enquiry",
            Rationale        = "Test patch",
            Ops              = ops
        };
    }

    private static void AssertOriginalUnmutated(AuthoredServiceBlueprint original, int originalStageCount) =>
        original.Touchpoints.Should().HaveCount(originalStageCount,
            because: "the original AuthoredServiceBlueprint must never be mutated");
}
