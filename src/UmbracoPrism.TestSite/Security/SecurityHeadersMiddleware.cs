using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.TestSite.Security;

/// <summary>
/// Sets standard security response headers on every non-backoffice response of the reference
/// site. Registered by <see cref="SecurityHeadersRegistration"/> via <c>UmbracoPipelineFilter</c>;
/// configure via <see cref="SecurityHeadersOptions"/>. The policy is the host's, not Prism's:
/// Prism contributes only the sources its sign-in needs
/// (<see cref="PrismContentSecurityPolicy.SignInSources"/>).
///
/// SEC-PT2-004: adds HSTS, X-Content-Type-Options, Referrer-Policy, X-Frame-Options,
/// Permissions-Policy, and an enforced Content-Security-Policy by default. Also adds
/// Cache-Control: no-store on any authenticated response (see
/// <see cref="SecurityHeadersOptions.NoCacheAuthenticated"/>), and a CSS/JS cache policy (see
/// <see cref="SecurityHeadersOptions.NoCacheStaticAssets"/>).
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
/// request from IPrismContext.CurrentTenant, not hardcoded: see PrismContentSecurityPolicy.SignInSources.
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
/// .SuppressXFrameOptionsHeader in SecurityHeadersRegistration). The indexer makes every header this
/// middleware owns idempotent against being set again by anything else downstream — including
/// something not yet discovered — rather than fixing that one known case and leaving the same
/// class of risk open for every other header.
/// </summary>
public sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IOptions<SecurityHeadersOptions> options)
{
    private readonly SecurityHeadersOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, IPrismContext prismContext)
    {
        if (_options.Enabled && !IsExcluded(context))
        {
            // Resolved now (IPrismContext is request-scoped) but not read until the OnStarting
            // callback below fires, by which point the rest of the pipeline, tenant resolution
            // included, has already run.
            context.Response.OnStarting(() =>
            {
                SetSecurityHeaders(context, prismContext.CurrentTenant);
                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    // /umbraco/prism/branding.css (PrismBrandingAssetsController) is an anonymous, publicly
    // referenced CSS resource, not backoffice UI — it just happens to live under /umbraco
    // because that's Umbraco's own reserved path prefix, guaranteed never to collide with a
    // published content node's URL (the convention every Prism controller under /umbraco/prism
    // follows). The blanket backoffice exclusion below was swallowing it too, so it never got
    // NoCacheStaticAssets' Cache-Control: no-store — Cloudflare then cached whatever it last saw
    // (including an empty response from before a tenant had any branding configured) for its own
    // default ~4h static-extension TTL, surviving a manual purge since the very next re-fetch got
    // re-cached the same way. Found live: a tenant's saved text-colour override never reached the
    // browser, even with the browser's own cache disabled.
    private static readonly PathString BrandingCssPath = "/umbraco/prism/branding.css";

    // The query key asp-append-version adds: a hash of the file's contents, so the URL changes
    // whenever the file does.
    private const string VersionQueryKey = "v";

    private bool IsExcluded(HttpContext context)
    {
        if (!_options.ExcludeBackoffice)
            return false;

        if (context.Request.Path.Equals(BrandingCssPath, StringComparison.OrdinalIgnoreCase))
            return false;

        return context.Request.Path.StartsWithSegments("/umbraco", StringComparison.OrdinalIgnoreCase);
    }

    private void SetSecurityHeaders(HttpContext context, PrismTenant? tenant)
    {
        var headers = context.Response.Headers;

        // Found live (round 1): the OIDC sign-in/sign-out redirect chain (AccountController
        // .Login/Logout's SignOut()/Challenge() calls) issues plain 3xx responses with no
        // Content-Type at all (a redirect has no body to describe) — verified directly:
        // `curl -D-` against /auth/login returns "HTTP/2 302", "content-length: 0", no
        // content-type header, and this middleware's own X-Content-Type-Options: nosniff
        // sitting alongside it regardless. On a real device, WKWebView treats that combination
        // — nosniff, on a response with no declared type to trust — as an undeterminable
        // resource and offers it as a phantom zero-byte "download" instead of just rendering
        // (or, for a redirect, following the Location header), breaking sign-in/out on mobile
        // (silently fine on desktop browsers, and invisible to Playwright, since neither
        // reproduces this specific real-WKWebView fallback).
        //
        // Found live (round 2): scoping the fix to 3xx alone wasn't enough — sign-out still
        // triggered the phantom download after the above shipped. The sign-out chain (see
        // prism-biometric-signout.js's own remarks) ends by loading /signout-callback-oidc,
        // ASP.NET Core's own OIDC SignedOutCallbackPath handler — verified directly: `curl -D-`
        // against it returns "HTTP/2 200", "content-length: 0", no content-type header, nosniff
        // present regardless, the exact same trigger signature as the 3xx case, just on a 200.
        // The real condition was never "is this a redirect", it's "does this response have any
        // actual content whose type nosniff could even apply to" — nosniff on an empty,
        // typeless body gives WebKit nothing to trust and nothing to render, so it falls back to
        // treating it as a download either way, regardless of status code.
        var hasNoRenderableBody = context.Response.ContentLength is null or 0
            && string.IsNullOrEmpty(context.Response.ContentType);

        if (_options.ContentTypeOptions is not null && !hasNoRenderableBody)
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

        SetCacheHeaders(context, headers);
    }

    private void SetCacheHeaders(HttpContext context, IHeaderDictionary headers)
    {
        // Checked here (inside the OnStarting callback, not up front in InvokeAsync) so it sees
        // the authentication middleware's final verdict on context.User, not whatever it was
        // before the rest of the pipeline ran.
        if (_options.NoCacheAuthenticated && context.User.Identity?.IsAuthenticated == true)
        {
            headers["Cache-Control"] = "no-store, must-revalidate";
            headers["Pragma"] = "no-cache";
        }

        // Independent of the authenticated check above: static files are not per-member, and
        // an edge cache applies its own extension-based default unless the origin says otherwise.
        if (_options.NoCacheStaticAssets && IsCssOrJavaScript(context))
        {
            if (context.Request.Query.ContainsKey(VersionQueryKey))
            {
                headers["Cache-Control"] = "public, max-age=31536000, immutable";
                headers.Remove("Pragma");
            }
            else
            {
                headers["Cache-Control"] = "no-cache";
                headers["Pragma"] = "no-cache";
            }
        }
    }

    /// <summary>
    /// Merges the host's own configured <see cref="SecurityHeadersOptions
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

        foreach (var (directive, signInSources) in PrismContentSecurityPolicy.SignInSources(tenant))
        {
            sources[directive] = sources.TryGetValue(directive, out var existing) && existing.Length > 0
                ? $"{existing} {signInSources}"
                : signInSources;
        }

        return sources;
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
