using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Configuration;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Appends standard security response headers to every non-backoffice response.
/// Registered automatically by <see cref="UmbracoPrism.Core.PrismComposer"/> via
/// <c>UmbracoPipelineFilter</c>. Configure via <see cref="PrismSecurityHeadersOptions"/>.
///
/// SEC-PT2-004: adds HSTS, X-Content-Type-Options, Referrer-Policy, X-Frame-Options,
/// Permissions-Policy, and Content-Security-Policy-Report-Only by default.
/// </summary>
internal sealed class PrismSecurityHeadersMiddleware(
    RequestDelegate next,
    IOptions<PrismSecurityHeadersOptions> options)
{
    private readonly PrismSecurityHeadersOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (_options.Enabled && !IsExcluded(context))
        {
            AppendSecurityHeaders(context);
        }

        await next(context);
    }

    private bool IsExcluded(HttpContext context)
    {
        if (!_options.ExcludeBackoffice)
            return false;

        return context.Request.Path.StartsWithSegments("/umbraco", StringComparison.OrdinalIgnoreCase);
    }

    private void AppendSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        if (_options.ContentTypeOptions is not null)
            headers.Append("X-Content-Type-Options", _options.ContentTypeOptions);

        if (_options.FrameOptions is not null)
            headers.Append("X-Frame-Options", _options.FrameOptions);

        if (_options.ReferrerPolicy is not null)
            headers.Append("Referrer-Policy", _options.ReferrerPolicy);

        if (_options.PermissionsPolicy is not null)
            headers.Append("Permissions-Policy", _options.PermissionsPolicy);

        if (_options.HstsValue is not null && context.Request.IsHttps)
            headers.Append("Strict-Transport-Security", _options.HstsValue);

        if (_options.ContentSecurityPolicyReportOnly is not null)
            headers.Append("Content-Security-Policy-Report-Only", _options.ContentSecurityPolicyReportOnly);
    }
}
