using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Injects tenant branding overrides into HTML responses.
/// </summary>
public class PrismBrandingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IPrismContext prismContext)
    {
        PersistMobileQueryFlagAsCookie(context);

        var tenant = prismContext.CurrentTenant;
        var overrides = tenant?.BrandingOverrides;
        var mobileOverrides = tenant?.MobileBrandingOverrides;
        var isPrismMobileRequest = PrismMobileRequestDetection.IsPrismMobileRequest(context);
        var hasBaseOverrides = overrides is { Count: > 0 };
        var hasMobileOverrides = isPrismMobileRequest && mobileOverrides is { Count: > 0 };
        var hasMobileShellGuards = isPrismMobileRequest;

        if (!hasBaseOverrides && !hasMobileOverrides && !hasMobileShellGuards)
        {
            await next(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await next(context);

        context.Response.Body = originalBody;

        if (context.Request.Method == HttpMethods.Head
            || context.Response.StatusCode == StatusCodes.Status304NotModified
            || context.Response.StatusCode == StatusCodes.Status204NoContent
            || context.Response.StatusCode == StatusCodes.Status205ResetContent)
        {
            return;
        }

        if (context.WebSockets.IsWebSocketRequest
            || context.Request.Headers.ContainsKey("Upgrade")
            || context.Response.Headers.ContainsKey("Upgrade"))
        {
            return;
        }

        buffer.Seek(0, SeekOrigin.Begin);

        var bodyText = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
        if (!ShouldInject(context, bodyText))
        {
            var originalBytes = Encoding.UTF8.GetBytes(bodyText);
            if (!context.Response.HasStarted)
            {
                context.Response.ContentLength = originalBytes.Length;
            }
            await context.Response.Body.WriteAsync(originalBytes);
            return;
        }

        var css = BuildCssOverrides(overrides, hasMobileOverrides ? mobileOverrides : null);
        var injected = InjectBranding(bodyText, css, hasMobileShellGuards);
        var bytes = Encoding.UTF8.GetBytes(injected);

        if (!context.Response.HasStarted)
        {
            context.Response.ContentLength = bytes.Length;
        }
        await context.Response.Body.WriteAsync(bytes);
    }

    private static void PersistMobileQueryFlagAsCookie(HttpContext context)
    {
        var queryFlag = PrismMobileRequestDetection.GetPrismMobileQueryFlag(context);
        if (!queryFlag.HasValue)
        {
            return;
        }

        if (queryFlag.Value)
        {
            context.Response.Cookies.Append(
                PrismMobileRequestDetection.CookieName,
                "1",
                new CookieOptions
                {
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = context.Request.IsHttps,
                    Path = "/"
                });
            return;
        }

        context.Response.Cookies.Delete(
            PrismMobileRequestDetection.CookieName,
            new CookieOptions
            {
                Path = "/"
            });
    }

    private static bool ShouldInject(HttpContext context, string bodyText)
    {
        if (context.Response.StatusCode < StatusCodes.Status200OK || context.Response.StatusCode >= StatusCodes.Status300MultipleChoices)
        {
            return false;
        }

        var contentType = context.Response.ContentType;
        if (!string.IsNullOrWhiteSpace(contentType) && contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return bodyText.Contains("</head>", StringComparison.OrdinalIgnoreCase)
            || bodyText.Contains("</body>", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCssOverrides(
        IReadOnlyDictionary<string, string>? overrides,
        IReadOnlyDictionary<string, string>? mobileOverrides)
    {
        var hasOverrides = (overrides is { Count: > 0 }) || (mobileOverrides is { Count: > 0 });
        if (!hasOverrides)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(":root{");

        AppendOverrides(builder, overrides);
        AppendOverrides(builder, mobileOverrides);

        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendOverrides(StringBuilder builder, IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides == null || overrides.Count == 0)
        {
            return;
        }

        foreach (var (name, value) in overrides)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) continue;
            builder.Append(name.Trim());
            builder.Append(':');
            builder.Append(value.Trim());
            builder.Append(';');
        }
    }

        private static string InjectBranding(string html, string css, bool includeMobileShellGuards)
    {
                var injection = new StringBuilder();

                if (!string.IsNullOrWhiteSpace(css))
                {
                        injection.Append($"<style id=\"prism-branding-overrides\">{css}</style>");
                }

                if (includeMobileShellGuards)
                {
                        if (!html.Contains("viewport-fit=cover", StringComparison.OrdinalIgnoreCase))
                        {
                                injection.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0, viewport-fit=cover\" />");
                        }

                        injection.Append(BuildMobileShellStyleTag());
                        injection.Append(BuildMobileShellGuardScriptTag());
                }

                var injectionMarkup = injection.ToString();
                if (string.IsNullOrWhiteSpace(injectionMarkup))
                {
                        return html;
                }

        var headCloseIndex = html.LastIndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headCloseIndex >= 0)
        {
                        return html.Insert(headCloseIndex, injectionMarkup);
        }

        var bodyCloseIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyCloseIndex >= 0)
        {
                        return html.Insert(bodyCloseIndex, injectionMarkup);
        }

                return html + injectionMarkup;
        }

        private static string BuildMobileShellStyleTag()
        {
                return """
<style id="prism-mobile-shell-base">
html.prism-mobile,
html.prism-mobile body {
    width: 100%;
    max-width: 100%;
    min-height: 100%;
    margin: 0;
    overflow-x: hidden;
}

html.prism-mobile body {
    padding-top: env(safe-area-inset-top, 0px);
    padding-right: env(safe-area-inset-right, 0px);
    padding-bottom: env(safe-area-inset-bottom, 0px);
    padding-left: env(safe-area-inset-left, 0px);
}

html.prism-mobile .container {
    width: 100%;
    max-width: none;
}
</style>
""";
        }

        private static string BuildMobileShellGuardScriptTag()
        {
                return """
<script id="prism-mobile-shell-guard">
(function () {
    var root = document.documentElement;
    if (!root.classList.contains('prism-mobile')) {
        root.classList.add('prism-mobile');
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (!(target instanceof Element)) return;

        var anchor = target.closest('a');
        if (!anchor) return;

        var href = anchor.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('javascript:')) return;

        if (href.startsWith('mailto:') || href.startsWith('tel:')) {
            event.preventDefault();
            return;
        }

        var forceInWebView = anchor.target && anchor.target.toLowerCase() === '_blank';
        if (!forceInWebView) return;

        event.preventDefault();
        window.location.assign(anchor.href);
    }, true);

    window.open = function (url) {
        if (typeof url === 'string' && url.length > 0) {
            window.location.assign(url);
        }
        return null;
    };
})();
</script>
""";
    }
}
