using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
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

    private static (PrismPageAccessFilter Filter, Mock<IPrismPageAccessResolver> Resolver, Mock<IPrismContext> PrismContext) BuildFilter()
    {
        var resolver = new Mock<IPrismPageAccessResolver>();
        var prismContext = new Mock<IPrismContext>();
        var filter = new PrismPageAccessFilter(resolver.Object, prismContext.Object);
        return (filter, resolver, prismContext);
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

    private static ResourceExecutingContext BuildContext(Action<HttpContext> configureHttpContext)
    {
        var httpContext = new DefaultHttpContext();
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
