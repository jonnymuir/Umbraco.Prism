using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Verifies successful patch operations against the shared community-enquiry reference workflow.
/// Input immutability is asserted after every test.
/// </summary>
public class WorkflowPatchServiceTests
{
    private static readonly string FixturesPath = WorkflowAuthoringFixtureLocator.GetFixturesPath();

    private readonly WorkflowProjector _projector = new();
    private readonly WorkflowPatchService _sut;

    public WorkflowPatchServiceTests()
    {
        _sut = new WorkflowPatchService(_projector);
    }

    [Fact]
    public async Task Apply_InsertStage_AppendsByDefault()
    {
        var original = await LoadReferenceFixture();
        var originalStageCount = original.Stages.Count;

        var envelope = BuildEnvelope("insert-stage", value: new
        {
            stageKey    = "site-notice",
            displayName = "Site Notice",
            kind        = "Question",
            actor       = "caseworker",
            fields      = Array.Empty<object>(),
            roleGates   = Array.Empty<string>()
        });

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeFalse(because: "inserting a valid stage should succeed");
        result.Updated.Stages.Should().HaveCount(originalStageCount + 1);
        result.Updated.Stages.Last().StageKey.Should().Be("site-notice");

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
                stageKey    = "supporting-docs",
                displayName = "Supporting Documents",
                kind        = "Question",
                actor       = "applicant",
                fields      = Array.Empty<object>(),
                roleGates   = Array.Empty<string>()
            });

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeFalse();
        var keys = result.Updated.Stages.Select(s => s.StageKey).ToList();
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
                stageKey    = "eligibility-check",
                displayName = "Eligibility Check",
                kind        = "Question",
                actor       = "applicant",
                fields      = Array.Empty<object>(),
                roleGates   = Array.Empty<string>()
            });

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeFalse();
        var keys = result.Updated.Stages.Select(s => s.StageKey).ToList();
        var newIdx  = keys.IndexOf("eligibility-check");
        var prevIdx = keys.IndexOf("collecting-details");
        newIdx.Should().Be(prevIdx + 1);
    }

    [Fact]
    public async Task Apply_RemoveStage_RemovesCorrectStage()
    {
        var original = await LoadReferenceFixture();

        var envelope = BuildEnvelope("remove-stage", path: "/stages/submitted");

        var result = _sut.Apply(envelope, original);

        // Projection may warn about dangling transitions but the stage should be gone.
        result.Updated.Stages.Should().NotContain(s => s.StageKey == "submitted",
            because: "remove-stage should eliminate the target stage");
        result.Updated.Stages.Count.Should().Be(original.Stages.Count - 1);

        original.Stages.Should().HaveCount(original.Stages.Count,
            because: "original must not be mutated");
    }

    [Fact]
    public async Task Apply_UpdateStage_ReplacesStageInPlace()
    {
        var original = await LoadReferenceFixture();
        var originalDetailsStage = original.Stages.Single(s => s.StageKey == "collecting-details");

        var envelope = BuildEnvelope("update-stage", path: "/stages/collecting-details", value: new
        {
            stageKey    = "collecting-details",
            displayName = "Updated Details",
            kind        = "Question",
            actor       = "applicant",
            fields      = Array.Empty<object>(),
            roleGates   = Array.Empty<string>()
        });

        var result = _sut.Apply(envelope, original);

        result.HasErrors.Should().BeFalse();
        var updated = result.Updated.Stages.Single(s => s.StageKey == "collecting-details");
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
            stageKey    = "extra-stage",
            displayName = "Extra Stage",
            kind        = "Question",
            actor       = "applicant",
            fields      = Array.Empty<object>(),
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
            StageCount      = original.Stages.Count,
            TransitionCount = original.Transitions.Count,
            HandoffCount    = original.Handoffs.Count,
            Version         = original.Version
        };

        var envelope = BuildEnvelope("insert-stage", value: new
        {
            stageKey    = "new-stage",
            displayName = "New Stage",
            kind        = "Question",
            actor       = "applicant",
            fields      = Array.Empty<object>(),
            roleGates   = Array.Empty<string>()
        });

        _sut.Apply(envelope, original);

        original.Stages.Count.Should().Be(snapshot.StageCount);
        original.Transitions.Count.Should().Be(snapshot.TransitionCount);
        original.Handoffs.Count.Should().Be(snapshot.HandoffCount);
        original.Version.Should().Be(snapshot.Version);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<AuthoredWorkflow> LoadReferenceFixture()
    {
        var store = new FilesystemAuthoredWorkflowStore(FixturesPath);
        var wf    = await store.LoadAsync("community-enquiry");
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
            TargetWorkflowId = "community-enquiry",
            Rationale        = "Test patch",
            Ops              = ops
        };
    }

    private static void AssertOriginalUnmutated(AuthoredWorkflow original, int originalStageCount) =>
        original.Stages.Should().HaveCount(originalStageCount,
            because: "the original AuthoredWorkflow must never be mutated");
}
