using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Umbraco.Cms.Web.Common.Routing;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Filters;

/// <summary>
/// Enforces <see cref="IPrismPageAccessResolver"/> for every Umbraco content-routed request.
/// Registered as a global MVC resource filter (<c>MvcOptions.Filters</c>), not a raw
/// <c>app.Use(...)</c> middleware — confirmed against Umbraco.Web.Website's own
/// UmbracoRouteValueTransformer that content routing (which populates the
/// <see cref="UmbracoRouteValues"/> feature this reads) always runs during ASP.NET Core's own
/// endpoint-routing resolution, strictly before any matched endpoint's filter pipeline — so an
/// MVC filter can never repeat the raw-middleware-registered-too-early ordering bug that once
/// broke Money Modeller's own login gate (see that history in git log / Program.cs).
/// </summary>
public class PrismPageAccessFilter(
    IPrismPageAccessResolver resolver,
    IPrismContext prismContext) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var routeValues = context.HttpContext.Features.Get<UmbracoRouteValues>();
        var content = routeValues?.PublishedRequest.PublishedContent;
        if (content is null)
        {
            // Not a content-routed request (an API controller, a static asset, the backoffice,
            // ...) — Umbraco's own routing never resolved a published content node for it, so
            // there's nothing here for a page-access policy to apply to.
            await next();
            return;
        }

        var isAuthenticated = context.HttpContext.User.Identity?.IsAuthenticated == true;
        var result = resolver.Resolve(content.Key, prismContext.CurrentTenant, isAuthenticated);

        if (result == PrismPageAccessResult.Unavailable)
        {
            // Deliberately a plain 404, not an access-denied/redirect — a page a tenant isn't
            // entitled to doesn't exist for them, and nothing about it (including whether it
            // would otherwise require sign-in) should be revealed.
            context.Result = new NotFoundResult();
            return;
        }

        if (result == PrismPageAccessResult.RequiresSignIn)
        {
            // The resolver only ever returns this when isAuthenticated is false, by its own
            // contract — no need to re-check here.
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            context.Result = new RedirectResult("/auth/login?returnUrl=" + Uri.EscapeDataString(returnUrl));
            return;
        }

        await next();
    }
}
