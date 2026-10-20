using System.Security.Claims;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismMobileManifestControllerTests
{
    private sealed class RecordingTabsProvider(params PrismMobileTab[] tabs) : IPrismMobileTabsProvider
    {
        public ClaimsPrincipal? SeenUser { get; private set; }

        public Task<IReadOnlyList<PrismMobileTab>> GetTabsAsync(HttpContext context, CancellationToken cancellationToken)
        {
            SeenUser = context.User;
            return Task.FromResult<IReadOnlyList<PrismMobileTab>>(tabs);
        }
    }

    private static PrismMobileManifestController BuildController(IPrismMobileTabsProvider provider, ClaimsPrincipal? member = null, string? ifNoneMatch = null)
    {
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(service => service.AuthenticateAsync(It.IsAny<HttpContext>(), PrismAuthenticationSchemes.MemberCookie))
            .ReturnsAsync(member is null
                ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(new AuthenticationTicket(member, PrismAuthenticationSchemes.MemberCookie)));

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(authentication.Object).BuildServiceProvider()
        };
        if (ifNoneMatch is not null)
        {
            context.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        return new PrismMobileManifestController(provider) { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    [Fact]
    public async Task Manifest_ListsTheHostsTabs_AndTheCookieThatMarksASignedInSession()
    {
        var controller = BuildController(new RecordingTabsProvider(
            new PrismMobileTab("Home", "/", "home"),
            new PrismMobileTab("Dashboard", "/dashboard", "/media/abc/dashboard.svg")));

        var result = await controller.Manifest(CancellationToken.None);

        var json = JsonDocument.Parse(result.Should().BeOfType<ContentResult>().Subject.Content!).RootElement;
        json.GetProperty("version").GetInt32().Should().Be(1);
        json.GetProperty("sessionCookie").GetString().Should().Be(PrismAuthenticationSchemes.MemberCookie);
        var tabs = json.GetProperty("tabs").EnumerateArray().ToList();
        tabs.Select(tab => tab.GetProperty("label").GetString()).Should().Equal("Home", "Dashboard");
        tabs[1].GetProperty("path").GetString().Should().Be("/dashboard");
        tabs[1].GetProperty("icon").GetString().Should().Be("/media/abc/dashboard.svg");
    }

    [Fact]
    public async Task Manifest_GivesTheProviderTheSignedInMember_SoTabsCanDependOnWhoTheyAre()
    {
        var provider = new RecordingTabsProvider(new PrismMobileTab("Home", "/"));
        var member = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "demo")], PrismAuthenticationSchemes.MemberCookie));

        await BuildController(provider, member).Manifest(CancellationToken.None);

        provider.SeenUser!.Identity!.IsAuthenticated.Should().BeTrue();
        provider.SeenUser.Identity.Name.Should().Be("demo");
    }

    [Fact]
    public async Task Manifest_IsNotSharedBetweenMembers_AndAnUnchangedOneIsNotSentAgain()
    {
        var provider = new RecordingTabsProvider(new PrismMobileTab("Home", "/"));
        var first = BuildController(provider);
        await first.Manifest(CancellationToken.None);
        var etag = first.Response.Headers.ETag.ToString();

        first.Response.Headers.CacheControl.ToString().Should().Be("private, no-cache");
        etag.Should().NotBeNullOrEmpty();

        var repeat = await BuildController(provider, ifNoneMatch: etag).Manifest(CancellationToken.None);

        repeat.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
    }

    [Fact]
    public async Task WithoutAHostProvider_TheAppGetsASingleHomeTab()
    {
        var tabs = await new PrismDefaultMobileTabsProvider().GetTabsAsync(new DefaultHttpContext(), CancellationToken.None);

        tabs.Should().ContainSingle().Which.Should().Be(new PrismMobileTab("Home", "/", "home"));
    }
}
