using FluentAssertions;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.TestSite;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Reported live: a signed-in juggling-licence applicant's own join-gateway wait screen polled
/// ServiceRequestPollController and got a 404 on every single attempt, whether or not the
/// automation had actually finished. TestSiteComposer.ResolveAccessProfile only recognised two of
/// the three request shapes a Wayfinder.Umbraco-hosted blueprint can be reached through (the page
/// GET, and the stage-advance POST's own form field) — never the poll GET's own query string — so
/// a signed-in member's poll request fell through to NjfContributionsTeam.NoAccessProfile, which
/// has no rights over their own instance at all. An anonymous applicant never hit this: the
/// IsAuthenticated != true fallback already gives them PublicVisitorQueue.AccessProfile
/// regardless, which is why this only ever showed up when testing while signed in.
/// </summary>
public class TestSiteComposerBlueprintContextTests
{
    [Fact]
    public void IsJugglingLicenceContext_RecognisesThePollEndpoint_ByItsQueryStringBlueprintKey()
    {
        var ctx = BuildPollRequest("apply-for-a-juggling-licence");

        TestSiteComposer.IsJugglingLicenceContext(ctx).Should().BeTrue(
            "a signed-in applicant's own poll request must resolve the same access profile their page did");
    }

    [Fact]
    public void IsJugglingLicenceContext_IgnoresAPollForADifferentBlueprint()
    {
        var ctx = BuildPollRequest("money-modeller");

        TestSiteComposer.IsJugglingLicenceContext(ctx).Should().BeFalse();
    }

    [Fact]
    public void IsMoneyModellerContext_RecognisesThePollEndpoint_ByItsQueryStringBlueprintKey()
    {
        var ctx = BuildPollRequest("money-modeller");

        TestSiteComposer.IsMoneyModellerContext(ctx).Should().BeTrue();
    }

    [Fact]
    public void IsJugglingLicenceContext_StillRecognisesThePageItself()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/apply-for-a-juggling-licence";

        TestSiteComposer.IsJugglingLicenceContext(ctx).Should().BeTrue();
    }

    private static DefaultHttpContext BuildPollRequest(string blueprintKey)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/wayfinder/workflow/poll";
        ctx.Request.QueryString = new QueryString(
            $"?blueprintKey={blueprintKey}&instanceId=11111111-1111-1111-1111-111111111111&knownStateVersion=1");
        return ctx;
    }
}
