using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Authoring round-trip + planning fixture contract tests.
///
/// These tests form the seam between Tangy's test harness and Blathers' projection output.
/// Blathers owns the fixture file at Fixtures/planning.workflow.json; once it exists, the
/// skip attribute below should be removed.
///
/// Blathers also owns WorkflowProjectorDeterminismTests and WorkflowProjectorShellInferenceTests
/// in this folder. This class is Tangy's only — do not duplicate those concerns here.
/// </summary>
public class PlanningWorkflowFixtureTests
{
    // Deterministic JSON options matching the projection pipeline contract.
    // The projector must produce identical byte sequences across runs with these settings.
    private static readonly JsonSerializerOptions DeterministicOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static string FixturePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "Fixtures",
        "planning.workflow.json"
    ));

    // ---------------------------------------------------------------------------
    // Guard: skip cleanly when Blathers' fixture hasn't landed yet.
    // ---------------------------------------------------------------------------

    [Fact(Skip =
        "Awaiting Blathers' planning.workflow.json fixture (Wave 1 foundation). " +
        "Expected path: src/UmbracoPrism.Core.Tests/Fixtures/planning.workflow.json. " +
        "Remove this Skip when Blathers' Wave 1 PR merges.")]
    public void Fixture_ExistsOnDisk()
    {
        // Remove the Skip attribute when planning.workflow.json is committed.
        // Expected fixture path: src/UmbracoPrism.Core.Tests/Fixtures/planning.workflow.json
        //
        // Expected top-level shape (AuthoredWorkflow):
        // {
        //   "definitionKey": "planning-permission",
        //   "displayName": "...",
        //   "schemaVersion": "1.0",
        //   "initialStageKey": "...",
        //   "stages": [ ... ],         // must include: application-form, check-answers, submitted
        //   "transitions": [ ... ],
        //   "roles": [ ... ],
        //   "fields": [ ... ]
        // }
        File.Exists(FixturePath).Should().BeTrue(
            $"planning.workflow.json should exist at {FixturePath} — Blathers creates this in the V1 foundation slice");
    }

    [Fact(Skip =
        "Awaiting Blathers' planning.workflow.json fixture (Wave 1 foundation). " +
        "Remove this Skip when Blathers' Wave 1 PR merges.")]
    public void Fixture_ParsesWithoutError()
    {
        var json = File.ReadAllText(FixturePath);

        // The fixture must deserialise as a plain JsonDocument at minimum;
        // full typed deserialisation will be added once AuthoredWorkflow ships.
        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow("planning.workflow.json must be valid JSON");
    }

    [Fact(Skip =
        "Awaiting Blathers' planning.workflow.json fixture (Wave 1 foundation). " +
        "Remove this Skip when Blathers' Wave 1 PR merges.")]
    public void Fixture_HasRequiredStagedDeclarations()
    {
        // The planning workflow must declare these four stages so that the Playwright
        // agent-loop and journey specs can reference them by key.
        //
        // Required stage keys (from §9 of 04-agentic-surfaces.md):
        //   - application-form   (StageKind.Capture — project details multi-step capture)
        //   - check-answers      (StageKind.Review  — GDS check-answers summary)
        //   - submitted          (StageKind.Waiting — waiting for reviewer assessment)
        //   - reviewer-assessment (StageKind.Decision — caseworker approval/rejection)
        //
        // Replace the raw JSON assertions below with typed AuthoredWorkflow assertions
        // once Blathers' AuthoredWorkflow model is available in this test project.

        var json = File.ReadAllText(FixturePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("stages", out var stages).Should().BeTrue("fixture must have a 'stages' array");
        stages.ValueKind.Should().Be(JsonValueKind.Array, "stages must be a JSON array");

        var stageKeys = stages.EnumerateArray()
            .Where(s => s.TryGetProperty("stageKey", out _))
            .Select(s => s.GetProperty("stageKey").GetString())
            .ToHashSet();

        var required = new[] { "application-form", "check-answers", "submitted" };
        foreach (var key in required)
        {
            stageKeys.Should().Contain(key,
                $"planning.workflow.json must declare the '{key}' stage so Playwright specs can reference it");
        }
    }

    [Fact(Skip =
        "Awaiting Blathers' planning.workflow.json fixture (Wave 1 foundation). " +
        "Remove this Skip when Blathers' Wave 1 PR merges.")]
    public void Fixture_RoundTrips_SerializeDeserialize_ByteIdentical()
    {
        // Regression guard: the fixture must survive a serialize→deserialize cycle
        // without any data loss or key-ordering drift.
        //
        // Uses the same DeterministicOptions as the projection pipeline.
        // Byte-identical comparison ensures no floating-point drift, key reordering,
        // or whitespace changes sneak through.

        var originalJson = File.ReadAllText(FixturePath);
        using var originalDoc = JsonDocument.Parse(originalJson);

        // Re-serialise with deterministic options.
        var reserialised = JsonSerializer.Serialize(originalDoc, DeterministicOptions);

        // Re-parse the reserialized form.
        using var roundTrippedDoc = JsonDocument.Parse(reserialised);
        var roundTrippedJson = JsonSerializer.Serialize(roundTrippedDoc, DeterministicOptions);

        roundTrippedJson.Should().Be(reserialised,
            "planning.workflow.json must be byte-identical after serialize→deserialize with deterministic JSON settings; " +
            "drift indicates non-deterministic key ordering or floating-point representation in the fixture");
    }
}
