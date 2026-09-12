using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Configuration;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Sets standard security response headers on every non-backoffice response.
/// Registered automatically by <see cref="UmbracoPrism.Core.PrismComposer"/> via
/// <c>UmbracoPipelineFilter</c>. Configure via <see cref="PrismSecurityHeadersOptions"/>.
///
/// SEC-PT2-004: adds HSTS, X-Content-Type-Options, Referrer-Policy, X-Frame-Options,
/// Permissions-Policy, and Content-Security-Policy-Report-Only by default.
///
/// Headers are set via <see cref="HttpResponse.OnStarting"/>, not inline before
/// <c>next(context)</c>. Found live: a genuine 404 — Umbraco's own "no content matches this
/// URL" page, not a bare framework 404 — carried none of these headers, even after moving
/// this middleware to the very front of the pipeline via <c>IStartupFilter</c> (tried and
/// reverted — pipeline *position* wasn't the cause). Umbraco's own content-resolution
/// middleware evidently resets the response before writing that branded page, which wipes
/// out anything set inline earlier in the same request regardless of where in the pipeline
/// it ran. <c>OnStarting</c> registers a callback that fires at the last possible moment —
/// right before the response's headers actually go out — so it survives that reset instead
/// of racing it.
///
/// Headers are SET via the indexer (replacing any existing value), not <c>Append</c>ed. A
/// header sent twice — even with identical values — is a real regression some browsers treat
/// as untrustworthy and drop entirely; this already bit X-Frame-Options once (ASP.NET Core's
/// own Antiforgery middleware appends it independently — see AntiforgeryOptions
/// .SuppressXFrameOptionsHeader in PrismComposer). The indexer makes every header this
/// middleware owns idempotent against being set again by anything else downstream — including
/// something not yet discovered — rather than fixing that one known case and leaving the same
/// class of risk open for every other header.
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
            context.Response.OnStarting(() =>
            {
                SetSecurityHeaders(context);
                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    private bool IsExcluded(HttpContext context)
    {
        if (!_options.ExcludeBackoffice)
            return false;

        return context.Request.Path.StartsWithSegments("/umbraco", StringComparison.OrdinalIgnoreCase);
    }

    private void SetSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        if (_options.ContentTypeOptions is not null)
            headers["X-Content-Type-Options"] = _options.ContentTypeOptions;

        if (_options.FrameOptions is not null)
            headers["X-Frame-Options"] = _options.FrameOptions;

        if (_options.ReferrerPolicy is not null)
            headers["Referrer-Policy"] = _options.ReferrerPolicy;

        if (_options.PermissionsPolicy is not null)
            headers["Permissions-Policy"] = _options.PermissionsPolicy;

        if (_options.HstsValue is not null && context.Request.IsHttps)
            headers["Strict-Transport-Security"] = _options.HstsValue;

        if (_options.ContentSecurityPolicyReportOnly is not null)
            headers["Content-Security-Policy-Report-Only"] = CspPolicyBuilder.WithAdditionalSources(
                _options.ContentSecurityPolicyReportOnly, _options.AdditionalContentSecurityPolicySources);
    }
}
