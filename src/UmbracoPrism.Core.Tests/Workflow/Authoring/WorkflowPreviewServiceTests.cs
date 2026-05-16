using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Core.Workflow.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Verifies <see cref="WorkflowPreviewService"/>:
/// – diff entries accurately describe changes between original and patched workflows.
/// – journey trace from the planning fixture follows the expected happy path.
/// </summary>
public class WorkflowPreviewServiceTests
{
    private static readonly string FixturesPath = GetFixturesPath();

    private readonly WorkflowProjector    _projector = new();
    private readonly WorkflowPatchService _patcher;
    private readonly WorkflowPreviewService _sut;

    public WorkflowPreviewServiceTests()
    {
        _patcher = new WorkflowPatchService(_projector);
        _sut     = new WorkflowPreviewService(_projector);
    }

    [Fact]
    public async Task Preview_PlanningFixture_JourneyTraceMatchesHappyPath()
    {
        var workflow = await LoadPlanningFixture();

        // Use the same fixture as both original and patched (no diff, just trace)
        var result = _sut.Preview(workflow, workflow);

        result.JourneyTrace.Should().Equal(
            ["declaration", "application-form", "check-answers", "submitted"],
            because: "the planning fixture happy path goes declaration → application-form → check-answers → submitted");
    }

    [Fact]
    public async Task Preview_StageAdded_AppearsInDiff()
    {
        var original = await LoadPlanningFixture();

        var envelope = BuildInsertStageEnvelope("site-notice", "Site Notice", after: "declaration");
        var patchResult = _patcher.Apply(envelope, original);
        patchResult.HasErrors.Should().BeFalse();

        var preview = _sut.Preview(original, patchResult.Updated);

        preview.Diff.OfType<StageAdded>().Should().ContainSingle(sa => sa.Key == "site-notice",
            because: "inserting site-notice should appear as StageAdded in the diff");
    }

    [Fact]
    public async Task Preview_StageRemoved_AppearsInDiff()
    {
        var original = await LoadPlanningFixture();

        // Remove application-form via patch
        var envelope = new ProposalEnvelope
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetWorkflowId = "planning-application",
            Rationale        = "Remove application-form for preview test",
            Ops              = [new PatchOp { Op = "remove-stage", Path = "/stages/application-form" }]
        };

        var patchResult = _patcher.Apply(envelope, original);
        // Patch may have projection errors (dangling transitions) — we preview the patched version anyway
        var patched = patchResult.Updated;

        var preview = _sut.Preview(original, patched);

        preview.Diff.OfType<StageRemoved>().Should().ContainSingle(sr => sr.Key == "application-form",
            because: "removing application-form should appear as StageRemoved in the diff");
    }

    [Fact]
    public async Task Preview_StageUpdated_AppearsInDiff()
    {
        var original  = await LoadPlanningFixture();
        var envelope  = BuildUpdateStageEnvelope("declaration", "Updated Declaration");
        var patched   = _patcher.Apply(envelope, original).Updated;

        var preview = _sut.Preview(original, patched);

        preview.Diff.OfType<StageUpdated>().Should().ContainSingle(su => su.Key == "declaration",
            because: "changing declaration's displayName should appear as StageUpdated");

        var diff = preview.Diff.OfType<StageUpdated>().Single(su => su.Key == "declaration");
        diff.FieldChanges.Should().Contain("displayName");
    }

    [Fact]
    public async Task Preview_ProjectedFile_IsNotNull()
    {
        var workflow = await LoadPlanningFixture();
        var preview  = _sut.Preview(workflow, workflow);

        preview.ProjectedFile.Should().NotBeNull();
        preview.Checksum.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task Preview_NoDiff_WhenOriginalAndPatchedAreEqual()
    {
        var workflow = await LoadPlanningFixture();
        var preview  = _sut.Preview(workflow, workflow);

        preview.Diff.Should().BeEmpty(because: "identical original and patched produces no diff");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<AuthoredWorkflow> LoadPlanningFixture()
    {
        var store = new FilesystemAuthoredWorkflowStore(FixturesPath);
        var wf    = await store.LoadAsync("planning");
        return wf ?? throw new InvalidOperationException("planning fixture not found");
    }

    private static ProposalEnvelope BuildInsertStageEnvelope(
        string stageKey, string displayName, string? after = null) =>
        new()
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetWorkflowId = "planning-application",
            Rationale        = "Preview test",
            Ops              =
            [
                new PatchOp
                {
                    Op    = "insert-stage",
                    After = after,
                    Value = JsonSerializer.SerializeToElement(new
                    {
                        stageKey,
                        displayName,
                        kind      = "Question",
                        actor     = "applicant",
                        fields    = Array.Empty<object>(),
                        roleGates = Array.Empty<string>()
                    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                }
            ]
        };

    private static ProposalEnvelope BuildUpdateStageEnvelope(string stageKey, string newDisplayName) =>
        new()
        {
            Id               = Guid.NewGuid(),
            CreatedAt        = DateTimeOffset.UtcNow,
            Agent            = new PatchAgent { Kind = "human-assisted", Identity = "test" },
            TargetWorkflowId = "planning-application",
            Rationale        = "Preview test",
            Ops              =
            [
                new PatchOp
                {
                    Op    = "update-stage",
                    Path  = $"/stages/{stageKey}",
                    Value = JsonSerializer.SerializeToElement(new
                    {
                        stageKey,
                        displayName = newDisplayName,
                        kind        = "Question",
                        actor       = "applicant",
                        fields      = Array.Empty<object>(),
                        roleGates   = Array.Empty<string>()
                    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                }
            ]
        };

    private static string GetFixturesPath() =>
        Path.Combine(
            Path.GetDirectoryName(typeof(WorkflowPreviewServiceTests).Assembly.Location)!,
            "Workflow", "Authoring", "Fixtures");
}
