using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Web.Common.Routing;
using UmbracoPrism.Core.Filters;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismPageAccessFilterTests
{
    [Fact]
    public async Task OnResourceExecutionAsync_PassesThrough_WhenNoUmbracoRouteValuesArePresent()
    {
        // A non-content-routed request (an API controller, a static asset, ...) — Umbraco's own
        // routing never resolved a published content node, so there's nothing to enforce.
        var (filter, resolver, _) = BuildFilter();
        var context = BuildContext(httpContext => { });
        var nextCalled = false;

        await filter.OnResourceExecutionAsync(context, BuildNext(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
        resolver.Verify(r => r.Resolve(It.IsAny<Guid>(), It.IsAny<PrismTenant?>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_PassesThrough_WhenResultIsPublic()
    {
        var contentKey = Guid.NewGuid();
        var (filter, resolver, _) = BuildFilter();
        resolver.Setup(r => r.Resolve(contentKey, It.IsAny<PrismTenant?>(), It.IsAny<bool>()))
            .Returns(PrismPageAccessResult.Public);

        var context = BuildContext(httpContext => SetUmbracoRouteValues(httpContext, contentKey));
        var nextCalled = false;

        await filter.OnResourceExecutionAsync(context, BuildNext(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnResourceExecutionAsync_Returns404_WhenResultIsUnavailable()
    {
        var contentKey = Guid.NewGuid();
        var (filter, resolver, _) = BuildFilter();
        resolver.Setup(r => r.Resolve(contentKey, It.IsAny<PrismTenant?>(), It.IsAny<bool>()))
            .Returns(PrismPageAccessResult.Unavailable);

        var context = BuildContext(httpContext => SetUmbracoRouteValues(httpContext, contentKey));
        var nextCalled = false;

        await filter.OnResourceExecutionAsync(context, BuildNext(context, () => nextCalled = true));

        nextCalled.Should().BeFalse("an unavailable page must never reach the action");
        context.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnResourceExecutionAsync_RedirectsToLogin_WhenResultIsRequiresSignIn()
    {
        var contentKey = Guid.NewGuid();
        var (filter, resolver, _) = BuildFilter();
        resolver.Setup(r => r.Resolve(contentKey, It.IsAny<PrismTenant?>(), It.IsAny<bool>()))
            .Returns(PrismPageAccessResult.RequiresSignIn);

        var context = BuildContext(httpContext =>
        {
            SetUmbracoRouteValues(httpContext, contentKey);
            httpContext.Request.Path = "/money-modeller";
        });
        var nextCalled = false;

        await filter.OnResourceExecutionAsync(context, BuildNext(context, () => nextCalled = true));

        nextCalled.Should().BeFalse();
        var redirect = context.Result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("/auth/login?returnUrl=%2Fmoney-modeller");
    }

    private static readonly PrismTenant SigningInTenant = new()
    {
        Hostname = "members.example.test",
        OidcAuthority = "https://idp.example.test/realms/members",
        OidcClientId = "members",
    };

    private static (PrismPageAccessFilter Filter, Mock<IPrismPageAccessResolver> Resolver, Mock<IPrismContext> PrismContext) BuildFilter(
        PrismTenant? tenant = null, bool useDefaultTenant = true)
    {
        var resolver = new Mock<IPrismPageAccessResolver>();
        var prismContext = new Mock<IPrismContext>();
        prismContext.SetupGet(c => c.CurrentTenant).Returns(useDefaultTenant ? tenant ?? SigningInTenant : tenant);
        var filter = new PrismPageAccessFilter(resolver.Object, prismContext.Object);
        return (filter, resolver, prismContext);
    }

    // ── Signed in means a Prism member session, nothing else ───────────────

    [Fact]
    public async Task AHostApplicationLogin_DoesNotCountAsSignedIn_ForAPrismProtectedPage()
    {
        // HttpContext.User holds the host's own login (the application's default scheme on a
        // hostname without Prism sign-in); Prism's cookie holds nothing.
        var contentKey = Guid.NewGuid();
        var (filter, resolver, _) = BuildFilter();
        resolver.Setup(r => r.Resolve(contentKey, It.IsAny<PrismTenant?>(), false)).Returns(PrismPageAccessResult.RequiresSignIn);
        resolver.Setup(r => r.Resolve(contentKey, It.IsAny<PrismTenant?>(), true)).Returns(PrismPageAccessResult.Public);

        var context = BuildContext(httpContext =>
        {
            SetUmbracoRouteValues(httpContext, contentKey);
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "admin@host.example")], "HostCookie"));
        });
        var nextCalled = false;

        await filter.OnResourceExecutionAsync(context, BuildNext(context, () => nextCalled = true));

        nextCalled.Should().BeFalse("a host session is not a Prism member and must not open the page");
        context.Result.Should().BeOfType<RedirectResult>();
    }

    [Fact]
    public async Task APrismMemberSession_CountsAsSignedIn()
    {
        var contentKey = Guid.NewGuid();
        var (filter, resolver, _) = BuildFilter();
        resolver.Setup(r => r.Resolve(contentKey, It.IsAny<PrismTenant?>(), false)).Returns(PrismPageAccessResult.RequiresSignIn);
        resolver.Setup(r => r.Resolve(contentKey, It.IsAny<PrismTenant?>(), true)).Returns(PrismPageAccessResult.Public);

        var context = BuildContext(httpContext => SetUmbracoRouteValues(httpContext, contentKey), memberSignedIn: true);
        var nextCalled = false;

        await filter.OnResourceExecutionAsync(context, BuildNext(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
    }

    public static TheoryData<string, PrismTenant?> HostnamesWithoutSignIn() => new()
    {
        { "no tenant for this hostname", null },
        { "a tenant with no identity provider", new PrismTenant { Hostname = "plain.example.test" } },
    };

    [Theory]
    [MemberData(nameof(HostnamesWithoutSignIn))]
    public async Task ASignInOnlyPage_IsNotFound_WhereNobodyCanSignIn(string because, PrismTenant? tenant)
    {
        var contentKey = Guid.NewGuid();
        var (filter, resolver, _) = BuildFilter(tenant, useDefaultTenant: false);
        resolver.Setup(r => r.Resolve(contentKey, It.IsAny<PrismTenant?>(), It.IsAny<bool>()))
            .Returns(PrismPageAccessResult.RequiresSignIn);

        var context = BuildContext(httpContext => SetUmbracoRouteValues(httpContext, contentKey));

        await filter.OnResourceExecutionAsync(context, BuildNext(context, () => { }));

        context.Result.Should().BeOfType<NotFoundResult>(because);
    }

    private static void SetUmbracoRouteValues(HttpContext httpContext, Guid contentKey)
    {
        var content = new Mock<IPublishedContent>();
        content.Setup(c => c.Key).Returns(contentKey);

        var publishedRequest = new Mock<IPublishedRequest>();
        publishedRequest.Setup(r => r.PublishedContent).Returns(content.Object);

        var routeValues = new UmbracoRouteValues(publishedRequest.Object, new ControllerActionDescriptor());
        httpContext.Features.Set(routeValues);
    }

    private static ResourceExecutingContext BuildContext(Action<HttpContext> configureHttpContext, bool memberSignedIn = false)
    {
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), "PrismMemberCookie"))
            .ReturnsAsync(memberSignedIn
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity("PrismMemberCookie")), "PrismMemberCookie"))
                : AuthenticateResult.NoResult());

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(authentication.Object).BuildServiceProvider()
        };
        configureHttpContext(httpContext);

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ResourceExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new List<IValueProviderFactory>());
    }

    private static ResourceExecutionDelegate BuildNext(ResourceExecutingContext context, Action onNext)
    {
        return () =>
        {
            onNext();
            return Task.FromResult(new ResourceExecutedContext(context, new List<IFilterMetadata>()));
        };
    }
}
