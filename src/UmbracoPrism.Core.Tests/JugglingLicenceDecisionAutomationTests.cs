using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Conditions;
using UmbracoPrism.TestSite;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// The "Juggling Licence Decision" automation TestSite seeds, evaluated with Automate's own
/// condition and binding evaluators (and its built-in binding filters), so these hold for what a
/// run actually does rather than for how the seeder happens to build it.
/// </summary>
public class JugglingLicenceDecisionAutomationTests
{
    // Automate reads step settings case-insensitively with operator names (its own JsonOptions.Settings).
    private static readonly JsonSerializerOptions SettingsJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly BindingEvaluator Bindings = new(new BindingFilterCollection(() =>
        typeof(IBindingFilter).Assembly.GetTypes()
            .Where(t => typeof(IBindingFilter).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(t => (IBindingFilter)Activator.CreateInstance(t, nonPublic: true)!)
            .ToList()));

    private readonly Automation _automation = JugglingLicenceDecisionAutomationSeeder.BuildAutomation(Guid.NewGuid(), signingKey: "test-key");

    [Fact]
    public void TheFastTrackCondition_OpensInAutomatesConditionBuilderWithItsConditionShowing()
    {
        var conditions = JsonNode.Parse(Json(Step("Fast-track licence type?").Settings["conditions"]))!;

        // The backoffice condition builder only reads PascalCase "Groups"/"Conditions" with
        // operator names; anything else opens as an empty builder.
        var group = conditions["Groups"]!.AsArray().Should().ContainSingle().Subject!;
        group["Conditions"]!.AsArray().Select(c => c!["Operator"]!.GetValue<string>()).Should().Equal("Equals");
    }

    [Theory]
    [InlineData("Recreational", true)]
    [InlineData("Competitive", false)]
    [InlineData("Professional", false)]
    public void OnlyARecreationalLicence_FastTracksWithoutReview(string licenceType, bool fastTracks)
    {
        var conditions = JsonSerializer.Deserialize<ConditionSet>(Json(Step("Fast-track licence type?").Settings["conditions"]), SettingsJson)!;

        new ConditionEvaluator(Bindings).Evaluate(conditions, Run(licenceType: licenceType)).Should().Be(fastTracks);
    }

    [Theory]
    [InlineData("Resolve: approved (reviewed)")]
    [InlineData("Resolve: referred")]
    public void TheReviewersComment_ReachesTheApplicantsDecisionNoteExactlyAsTyped(string resolveStep)
    {
        const string comment = "Lovely \"five-club cascade\".\nBring proof of insurance: by 1 March.";

        DecisionNote(resolveStep, comment).Should().Be(comment);
    }

    [Theory]
    [InlineData("Resolve: approved (reviewed)", null, "Approved after review.")]
    [InlineData("Resolve: referred", "", "Referred for further review by the licensing team.")]
    public void ABlankComment_FallsBackToTheStandardNoteForThatOutcome(string resolveStep, string? comment, string expected)
    {
        DecisionNote(resolveStep, comment).Should().Be(expected);
    }

    [Fact]
    public void TheTriggerCard_SitsClearOfEveryStepCardOnTheCanvas()
    {
        var trigger = JsonNode.Parse(_automation.CanvasState!)!["triggerPosition"]!;
        var (x, y) = (trigger["x"]!.GetValue<double>(), trigger["y"]!.GetValue<double>());

        // A step card is at most 280px wide; anything closer horizontally in the same band overlaps.
        _automation.Steps.Should().NotContain(s => Math.Abs(s.Position!.X - x) < 280 && Math.Abs(s.Position.Y - y) < 120,
            "the trigger card would render on top of a step card");
    }

    [Fact]
    public void AnAutomaticApproval_ExplainsWhyNoOneReviewedIt()
    {
        DecisionNote("Resolve: approved (auto)", comment: "ignored").Should().Contain("fast-track");
    }

    private string? DecisionNote(string resolveStep, string? comment)
    {
        // A run exposes each step's output under its alias, so the reviewer's decision is keyed by
        // the approval step's own alias, proving the resolve step reads that step.
        var reviewAlias = _automation.Steps.Single(s => s.ActionAlias == "umbracoAutomate.requestApproval").Alias!;
        var payload = Bindings.Evaluate((string)Step(resolveStep).Settings["resultPayload"]!, Run(comment: comment, reviewAlias: reviewAlias));

        return JsonNode.Parse(payload)!["applicationDecisionNote"]?.GetValue<string>();
    }

    private StepConfiguration Step(string name) => _automation.Steps.Should().ContainSingle(s => s.Name == name).Subject;

    private static Dictionary<string, object?> Run(string? licenceType = null, string? comment = null, string reviewAlias = "") =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["trigger"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["body"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["invocationId"] = "invocation-1",
                    ["inputs"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["licenceType"] = licenceType },
                },
            },
            ["steps"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [reviewAlias] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["comment"] = comment },
            },
        };

    private static string Json(object? value) => value is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(value);
}
