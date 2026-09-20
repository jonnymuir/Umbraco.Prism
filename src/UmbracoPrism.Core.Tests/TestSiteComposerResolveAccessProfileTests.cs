using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UmbracoPrism.Core.Services;
using UmbracoPrism.TestSite;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// TestSiteComposer.ResolveAccessProfile now takes the blueprint key Wayfinder.Umbraco itself
/// resolves (Wayfinder.Umbraco 2.0+, jonnymuir/Wayfinder.Umbraco#104) instead of reverse
/// -engineering it from the request's own path/form/query shape — the prior version of this
/// resolver did exactly that and missed the join-gateway wait screen's poll endpoint entirely,
/// silently resolving the wrong access profile and 404ing on every attempt for a signed-in
/// applicant. These tests exercise the resolver directly against every persona it hands out.
/// </summary>
public class TestSiteComposerResolveAccessProfileTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JugglingLicenceBlueprintKey_AlwaysGetsPublicVisitorQueue_RegardlessOfAuthentication(bool isAuthenticated)
    {
        var ctx = BuildContext(isAuthenticated, email: isAuthenticated ? "njf-caseworker@prism.local" : null);

        var profile = TestSiteComposer.ResolveAccessProfile(ctx, "apply-for-a-juggling-licence");

        profile.Should().BeSameAs(UmbracoPrism.TestSite.Services.ServiceDesign.PublicVisitorQueue.AccessProfile,
            "the poll endpoint's own resolved blueprintKey must reach this resolver the same way the page's did — this is the exact bug that shipped a 404 for every signed-in applicant");
    }

    [Fact]
    public void AnonymousVisitor_WithNoBlueprintKey_GetsPublicVisitorQueue()
    {
        var ctx = BuildContext(isAuthenticated: false, email: null);

        var profile = TestSiteComposer.ResolveAccessProfile(ctx, blueprintKey: null);

        profile.Should().BeSameAs(UmbracoPrism.TestSite.Services.ServiceDesign.PublicVisitorQueue.AccessProfile,
            "an anonymous caller — e.g. a caseworker worklist query, which never has a single blueprint in scope — must still resolve safely");
    }

    [Fact]
    public void MoneyModellerBlueprintKey_ForASignedInMember_GetsMoneyModellerAccess()
    {
        var ctx = BuildContext(isAuthenticated: true, email: "demo@prism.local");

        var profile = TestSiteComposer.ResolveAccessProfile(ctx, "money-modeller");

        profile.Should().BeSameAs(UmbracoPrism.TestSite.Services.ServiceDesign.MoneyModellerAccess.AccessProfile);
    }

    [Fact]
    public void OtherBlueprintKey_ForASignedInNjfCaseworker_GetsNjfAccessProfile()
    {
        var ctx = BuildContext(isAuthenticated: true, email: "njf-caseworker@prism.local");

        var profile = TestSiteComposer.ResolveAccessProfile(ctx, blueprintKey: null);

        profile.Should().BeSameAs(UmbracoPrism.TestSite.Services.ServiceDesign.NjfContributionsTeam.AccessProfile);
    }

    [Fact]
    public void OtherBlueprintKey_ForASignedInPlainMember_GetsNoAccessProfile()
    {
        // demo@prism.local is deliberately not an NJF Contributions Team member — signing in
        // must never silently grant NJF access just from being authenticated.
        var ctx = BuildContext(isAuthenticated: true, email: "demo@prism.local");

        var profile = TestSiteComposer.ResolveAccessProfile(ctx, blueprintKey: null);

        profile.Should().BeSameAs(UmbracoPrism.TestSite.Services.ServiceDesign.NjfContributionsTeam.NoAccessProfile);
    }

    private static HttpContext BuildContext(bool isAuthenticated, string? email)
    {
        var userContext = new Mock<IPrismUserContext>();
        userContext.SetupGet(u => u.Email).Returns(email);

        var services = new ServiceCollection();
        services.AddSingleton(userContext.Object);

        var identity = isAuthenticated
            ? new ClaimsIdentity(authenticationType: "TestAuth")
            : new ClaimsIdentity();

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(identity),
        };
    }
}
