using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Injects tenant branding overrides into HTML responses.
/// </summary>
public class PrismBrandingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IPrismContext prismContext)
    {
        var tenant = prismContext.CurrentTenant;
        var overrides = tenant?.BrandingOverrides;

        if (overrides == null || overrides.Count == 0)
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

        var css = BuildCssOverrides(overrides);
        var injected = InjectBranding(bodyText, css);
        var bytes = Encoding.UTF8.GetBytes(injected);

        if (!context.Response.HasStarted)
        {
            context.Response.ContentLength = bytes.Length;
        }
        await context.Response.Body.WriteAsync(bytes);
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

    private static string BuildCssOverrides(IReadOnlyDictionary<string, string> overrides)
    {
        var builder = new StringBuilder();
        builder.Append(":root{");

        foreach (var (name, value) in overrides)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) continue;
            builder.Append(name.Trim());
            builder.Append(':');
            builder.Append(value.Trim());
            builder.Append(';');
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string InjectBranding(string html, string css)
    {
        var styleTag = $"<style id=\"prism-branding-overrides\">{css}</style>";
        var headCloseIndex = html.LastIndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headCloseIndex >= 0)
        {
            return html.Insert(headCloseIndex, styleTag);
        }

        var bodyCloseIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyCloseIndex >= 0)
        {
            return html.Insert(bodyCloseIndex, styleTag);
        }

        return html + styleTag;
    }
}
