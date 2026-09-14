using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Sets standard security response headers on every non-backoffice response.
/// Registered automatically by <see cref="UmbracoPrism.Core.PrismComposer"/> via
/// <c>UmbracoPipelineFilter</c>. Configure via <see cref="PrismSecurityHeadersOptions"/>.
///
/// SEC-PT2-004: adds HSTS, X-Content-Type-Options, Referrer-Policy, X-Frame-Options,
/// Permissions-Policy, and an enforced Content-Security-Policy by default. Also adds
/// Cache-Control: no-store on any authenticated response (see
/// <see cref="PrismSecurityHeadersOptions.NoCacheAuthenticated"/>) and on any CSS/JS response
/// (see <see cref="PrismSecurityHeadersOptions.NoCacheStaticAssets"/>).
///
/// form-action also widens itself automatically to the current tenant's own OIDC provider
/// host(s) (Entra or a generic authority) — found live: sign-out silently did nothing, on both
/// web and mobile. AccountController.Logout's SignOut() correctly redirects through the
/// provider's own end-session endpoint (a real security requirement — the local Prism cookie
/// isn't the only session that needs killing), confirmed live via a direct network capture: the
/// server issues the redirect correctly, and navigating to that exact URL directly works fine,
/// but the *browser itself* cancels it (net::ERR_ABORTED) specifically because it results from
/// a form submission. CSP's form-action directive governs not just a form's own submit target
/// but any redirect chain that results from it — 'self' alone can never be enough for any tenant
/// using external OIDC, since signing out of it is definitionally cross-origin. Derived per
/// request from IPrismContext.CurrentTenant, not hardcoded — see BuildOidcFormActionSources.
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

    public async Task InvokeAsync(HttpContext context, IPrismContext prismContext)
    {
        if (_options.Enabled && !IsExcluded(context))
        {
            // Resolved now (IPrismContext is request-scoped — the same instance
            // PrismTenantMiddleware, further down the pipeline, populates CurrentTenant on),
            // but not read until the OnStarting callback below actually fires, by which point
            // next(context) — and so tenant resolution — has already completed.
            context.Response.OnStarting(() =>
            {
                SetSecurityHeaders(context, prismContext.CurrentTenant);
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

    private void SetSecurityHeaders(HttpContext context, PrismTenant? tenant)
    {
        var headers = context.Response.Headers;

        // Found live: the OIDC sign-in/sign-out redirect chain (AccountController.Login/Logout's
        // SignOut()/Challenge() calls) issues plain 3xx responses with no Content-Type at all
        // (a redirect has no body to describe) — verified directly: `curl -D-` against
        // /auth/login returns "HTTP/2 302", "content-length: 0", no content-type header, and
        // this middleware's own X-Content-Type-Options: nosniff sitting alongside it regardless.
        // On a real device, WKWebView treats that combination — nosniff, on a response with no
        // declared type to trust — as an undeterminable resource and offers it as a phantom
        // zero-byte "download" instead of just following the Location header, breaking sign-in
        // and sign-out on mobile (silently fine on desktop browsers, and invisible to the
        // Playwright-driven tests that verified the sign-out flow earlier, since neither
        // reproduces this specific real-WKWebView fallback). nosniff exists to stop a browser
        // misinterpreting *rendered content* as something other than its declared type — a
        // redirect has no rendered content, so skipping it here loses no real protection.
        var isRedirect = context.Response.StatusCode is >= 300 and < 400;

        if (_options.ContentTypeOptions is not null && !isRedirect)
            headers["X-Content-Type-Options"] = _options.ContentTypeOptions;

        if (_options.FrameOptions is not null)
            headers["X-Frame-Options"] = _options.FrameOptions;

        if (_options.ReferrerPolicy is not null)
            headers["Referrer-Policy"] = _options.ReferrerPolicy;

        if (_options.PermissionsPolicy is not null)
            headers["Permissions-Policy"] = _options.PermissionsPolicy;

        if (_options.HstsValue is not null && context.Request.IsHttps)
            headers["Strict-Transport-Security"] = _options.HstsValue;

        var effectiveSources = BuildEffectiveCspSources(tenant);

        if (_options.ContentSecurityPolicy is not null)
            headers["Content-Security-Policy"] = CspPolicyBuilder.WithAdditionalSources(
                _options.ContentSecurityPolicy, effectiveSources);

        if (_options.ContentSecurityPolicyReportOnly is not null)
            headers["Content-Security-Policy-Report-Only"] = CspPolicyBuilder.WithAdditionalSources(
                _options.ContentSecurityPolicyReportOnly, effectiveSources);

        // Checked here (inside the OnStarting callback, not up front in InvokeAsync) so it sees
        // the authentication middleware's final verdict on context.User, not whatever it was
        // before the rest of the pipeline ran.
        if (_options.NoCacheAuthenticated && context.User.Identity?.IsAuthenticated == true)
        {
            headers["Cache-Control"] = "no-store, must-revalidate";
            headers["Pragma"] = "no-cache";
        }

        // Deliberately independent of the authenticated check above, and deliberately
        // site-wide rather than scoped to any one project's static-asset folder — see the
        // option's own doc comment: an intermediary CDN/edge cache, not the browser, is what
        // silently served stale copies across multiple redeploys, and it caches by file
        // extension, not by path.
        if (_options.NoCacheStaticAssets && IsCssOrJavaScript(context))
        {
            headers["Cache-Control"] = "no-store, must-revalidate";
            headers["Pragma"] = "no-cache";
        }
    }

    /// <summary>
    /// Merges the host's own configured <see cref="PrismSecurityHeadersOptions
    /// .AdditionalContentSecurityPolicySources"/> with the current tenant's own OIDC provider
    /// host(s), appended to <c>form-action</c> and <c>frame-src</c> specifically (never
    /// replacing a host's own configured value for either directive — both apply).
    ///
    /// <c>frame-src</c> widening exists for the mobile app's own silent sign-out: the native
    /// shell's single WebView can't show the IdP's federated end-session page as a visible
    /// top-level navigation the way web sign-out does (Entra's own hosted logout UI includes an
    /// account-chooser interstitial that reads as broken on a single-account app — see
    /// prism-biometric-signout.js's own remarks), so it instead loads that exact same
    /// already-correct URL (built by the exact same code path as the web flow — this directive
    /// only widens WHERE an iframe may navigate, it doesn't change what URL gets built or
    /// requested) into a hidden iframe, clearing the IdP's own session cookie without the user
    /// ever seeing its UI. Without this, that iframe navigation is silently blocked by CSP —
    /// frame-src has no fallback to form-action, only to default-src 'self', so the two
    /// directives need widening independently even though they share the same host list.
    /// </summary>
    private Dictionary<string, string> BuildEffectiveCspSources(PrismTenant? tenant)
    {
        var sources = new Dictionary<string, string>(
            _options.AdditionalContentSecurityPolicySources, StringComparer.OrdinalIgnoreCase);

        var oidcHosts = BuildOidcFormActionSources(tenant);
        if (oidcHosts.Count > 0)
        {
            var formatted = string.Join(' ', oidcHosts.Select(host => $"https://{host}"));
            sources["form-action"] = sources.TryGetValue("form-action", out var existing) && existing.Length > 0
                ? $"{existing} {formatted}"
                : formatted;
            sources["frame-src"] = sources.TryGetValue("frame-src", out var existingFrame) && existingFrame.Length > 0
                ? $"{existingFrame} {formatted}"
                : formatted;
        }

        return sources;
    }

    /// <summary>
    /// The OIDC provider host(s) a tenant's own sign-in/sign-out flow can redirect through —
    /// derived from the tenant's own EntraTenantId/OidcAuthority, nothing hardcoded per
    /// deployment. Entra hosts are only added when EntraTenantId is actually set (precise, not
    /// the broader always-on set MobileBundleService's own allowNavigation list uses for the
    /// generated app — a CSP directive is a security boundary, worth being exact about, where a
    /// native app's navigation allow-list is more about functional connectivity than strict
    /// enforcement).
    /// </summary>
    private static IReadOnlyList<string> BuildOidcFormActionSources(PrismTenant? tenant)
    {
        if (tenant is null) return [];

        var hosts = new List<string>();

        var oidcAuthority = tenant.OidcAuthority?.Trim();
        if (!string.IsNullOrWhiteSpace(oidcAuthority) &&
            Uri.TryCreate(oidcAuthority, UriKind.Absolute, out var authorityUri))
        {
            AddHost(hosts, authorityUri.Authority);
        }

        var entraTenantId = tenant.EntraTenantId?.Trim();
        if (!string.IsNullOrWhiteSpace(entraTenantId))
        {
            AddHost(hosts, "login.microsoftonline.com");
            AddHost(hosts, "*.ciamlogin.com");
            AddHost(hosts, "*.b2clogin.com");
            AddHost(hosts, $"{entraTenantId}.ciamlogin.com");
            AddHost(hosts, $"{entraTenantId}.b2clogin.com");
        }

        return hosts;
    }

    private static void AddHost(List<string> hosts, string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return;
        if (!hosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            hosts.Add(host);
        }
    }

    private static bool IsCssOrJavaScript(HttpContext context)
    {
        var contentType = context.Response.ContentType;
        if (!string.IsNullOrEmpty(contentType))
        {
            return contentType.Contains("text/css", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase);
        }

        // Content-Type should always be set by the time OnStarting fires for a real static
        // file response — this is a defensive fallback, not the primary path.
        var path = context.Request.Path.Value;
        return path is not null &&
            (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase));
    }
}
