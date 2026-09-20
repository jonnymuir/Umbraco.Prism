using FluentAssertions;
using Umbraco.Automate.Core.Automations;
using UmbracoPrism.TestSite;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Automate has no auto-arrange feature of its own to call (checked: no such API surfaces in
/// Umbraco.Automate.Core or its client bundle), and hand-picked step positions overlapped once
/// the juggling-licence-decision automation's graph grew past a simple chain — reported live, the
/// canvas showed step cards crashing into each other. LayoutSteps replaced those hardcoded
/// coordinates with a real BFS-depth/DFS-leaf-order layout; these tests pin down that it actually
/// produces a card-sized-clear, non-overlapping arrangement for the automation's real shape
/// (a branch that fans out twice, never rejoins) rather than just asserting it runs.
/// </summary>
public class JugglingLicenceDecisionAutomationSeederLayoutTests
{
    // Mirrors the step-card width Umbraco.Automate.Core's own ua-automation-canvas element
    // renders at (min-width:220px;max-width:280px) — anything less than this between two nodes
    // in the same column is a guaranteed overlap.
    private const double MinCardWidth = 280;
    private const double MinCardClearance = 100;

    [Fact]
    public void LayoutSteps_PlacesEveryStepAtAUniquePosition()
    {
        var (steps, connections) = BuildJugglingLicenceGraph();

        var positions = JugglingLicenceDecisionAutomationSeeder.LayoutSteps(steps, connections);

        positions.Values
            .Select(p => (p.X, p.Y))
            .Should().OnlyHaveUniqueItems("two step cards at the same coordinates would render on top of each other");
    }

    [Fact]
    public void LayoutSteps_NeverPlacesTwoStepsInTheSameColumnCloserThanACardWidth()
    {
        var (steps, connections) = BuildJugglingLicenceGraph();

        var positions = JugglingLicenceDecisionAutomationSeeder.LayoutSteps(steps, connections);

        var byColumn = positions.Values.GroupBy(p => p.X);
        foreach (var column in byColumn)
        {
            var ys = column.Select(p => p.Y).OrderBy(y => y).ToList();
            for (var i = 1; i < ys.Count; i++)
            {
                (ys[i] - ys[i - 1]).Should().BeGreaterThanOrEqualTo(MinCardClearance,
                    "two step cards in the same column need enough vertical clearance not to overlap");
            }
        }
    }

    [Fact]
    public void LayoutSteps_SpacesColumnsAtLeastOneCardWidthApart()
    {
        var (steps, connections) = BuildJugglingLicenceGraph();

        var positions = JugglingLicenceDecisionAutomationSeeder.LayoutSteps(steps, connections);

        var columns = positions.Values.Select(p => p.X).Distinct().OrderBy(x => x).ToList();
        for (var i = 1; i < columns.Count; i++)
        {
            (columns[i] - columns[i - 1]).Should().BeGreaterThanOrEqualTo(MinCardWidth,
                "adjacent columns need at least a full step-card's width between them or the cards overlap horizontally");
        }
    }

    [Fact]
    public void LayoutSteps_PlacesBranchesInLaterColumnsThanTheStepTheyBranchFrom()
    {
        var (steps, connections) = BuildJugglingLicenceGraph();
        var byName = steps.ToDictionary(s => s.Name);

        var positions = JugglingLicenceDecisionAutomationSeeder.LayoutSteps(steps, connections);

        positions[byName["Fast-track licence type?"].Id].X.Should().BeLessThan(
            positions[byName["Notify: approved (auto)"].Id].X,
            "a branch target must render after (to the right of) the step it branches from");
        positions[byName["Fast-track licence type?"].Id].X.Should().BeLessThan(
            positions[byName["Approve competitive/professional licence?"].Id].X);
    }

    /// <summary>Rebuilds the same shape BuildAutomation() produces — trigger, resolve, an if-branch fanning into an auto-approved fast path and a human-approval step that itself fans into approved/referred — without needing a live Automate workspace.</summary>
    private static (List<StepConfiguration> Steps, List<StepConnection> Connections) BuildJugglingLicenceGraph()
    {
        var resolveOwner = Guid.NewGuid();
        var checkLicenceType = Guid.NewGuid();
        var autoNotify = Guid.NewGuid();
        var autoResolve = Guid.NewGuid();
        var requestApproval = Guid.NewGuid();
        var approvedNotify = Guid.NewGuid();
        var approvedResolve = Guid.NewGuid();
        var referredNotify = Guid.NewGuid();
        var referredResolve = Guid.NewGuid();

        var steps = new List<StepConfiguration>
        {
            new() { Id = resolveOwner, ActionAlias = "wayfinder.resolveInstanceOwner", Name = "Resolve applicant" },
            new() { Id = checkLicenceType, ActionAlias = "umbracoAutomate.if", Name = "Fast-track licence type?" },
            new() { Id = autoNotify, ActionAlias = "prism.sendPushNotification", Name = "Notify: approved (auto)" },
            new() { Id = autoResolve, ActionAlias = "wayfinder.resolveSupportSystemOutcome", Name = "Resolve: approved (auto)" },
            new() { Id = requestApproval, ActionAlias = "umbracoAutomate.requestApproval", Name = "Approve competitive/professional licence?" },
            new() { Id = approvedNotify, ActionAlias = "prism.sendPushNotification", Name = "Notify: approved (reviewed)" },
            new() { Id = approvedResolve, ActionAlias = "wayfinder.resolveSupportSystemOutcome", Name = "Resolve: approved (reviewed)" },
            new() { Id = referredNotify, ActionAlias = "prism.sendPushNotification", Name = "Notify: referred" },
            new() { Id = referredResolve, ActionAlias = "wayfinder.resolveSupportSystemOutcome", Name = "Resolve: referred" },
        };

        var connections = new List<StepConnection>
        {
            new() { SourceStepId = Guid.Empty, TargetStepId = resolveOwner },
            new() { SourceStepId = resolveOwner, TargetStepId = checkLicenceType },
            new() { SourceStepId = checkLicenceType, TargetStepId = autoNotify, SourceHandle = "true", Outcome = "true" },
            new() { SourceStepId = autoNotify, TargetStepId = autoResolve },
            new() { SourceStepId = checkLicenceType, TargetStepId = requestApproval, SourceHandle = "false", Outcome = "false" },
            new() { SourceStepId = requestApproval, TargetStepId = approvedNotify, SourceHandle = "approved", Outcome = "approved" },
            new() { SourceStepId = approvedNotify, TargetStepId = approvedResolve },
            new() { SourceStepId = requestApproval, TargetStepId = referredNotify, SourceHandle = "rejected", Outcome = "rejected" },
            new() { SourceStepId = referredNotify, TargetStepId = referredResolve },
        };

        return (steps, connections);
    }
}
