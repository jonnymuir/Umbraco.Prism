using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Labels each response to an app built with saved pages with the version of the site's content
/// it was rendered from, and whether a member was signed in, so the app's service worker
/// (<c>prism-offline-sw.js</c>) can tell whether a fresh copy of a page differs from its saved one.
/// </summary>
public class PrismSavedPagesMiddleware(RequestDelegate next, PrismContentVersion contentVersion)
{
    internal const string HeaderName = "Prism-Content-Version";

    public Task InvokeAsync(HttpContext context)
    {
        if (PrismMobileRequestDetection.SavesPages(context))
        {
            context.Response.OnStarting(() =>
            {
                var audience = context.User.Identity?.IsAuthenticated == true ? "member" : "visitor";
                context.Response.Headers[HeaderName] = $"{contentVersion.Current}.{audience}";
                return Task.CompletedTask;
            });
        }

        return next(context);
    }
}
