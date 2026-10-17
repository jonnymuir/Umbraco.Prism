using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace UmbracoPrism.TestSite.Security;

/// <summary>
/// The reference site's own response-security policy: <see cref="SecurityHeadersMiddleware"/> and
/// the framework cookie defaults that go with it. Site-wide policy is the host's decision, which is
/// why this lives here rather than in the Prism package.
/// </summary>
public static class SecurityHeadersRegistration
{
    public static IUmbracoBuilder AddSecurityHeaders(this IUmbracoBuilder builder)
    {
        // SEC-PT2-004: X-Content-Type-Options, X-Frame-Options (SAMEORIGIN), Referrer-Policy,
        // Permissions-Policy, HSTS (HTTPS only), and an enforced Content-Security-Policy (self
        // only, no unsafe-inline: Prism's branding/mobile-shell/biometric assets and this site's
        // own pages are all external resources, never spliced inline), widened per request with
        // the sources Prism's sign-in needs. Backoffice paths excluded by default.
        builder.Services.Configure<SecurityHeadersOptions>(
            builder.Config.GetSection(SecurityHeadersOptions.SectionName));

        ConfigureFrameworkCookieDefaults(builder.Services);

        builder.Services.Configure<UmbracoPipelineOptions>(options =>
            options.AddFilter(new UmbracoPipelineFilter(
                "TestSiteSecurityHeaders",
                app => app.UseMiddleware<SecurityHeadersMiddleware>())));

        return builder;
    }

    /// <summary>
    /// Overrides ASP.NET Core framework defaults that the DAST baseline reports on every host:
    /// the antiforgery middleware's own X-Frame-Options copy, and cookies that ship without Secure.
    /// </summary>
    private static void ConfigureFrameworkCookieDefaults(IServiceCollection services)
    {
        // ASP.NET Core's own antiforgery middleware sets X-Frame-Options: SAMEORIGIN itself,
        // automatically, on any response where a token gets issued (IAntiforgery
        // .GetAndStoreTokens), an easy-to-miss built-in behaviour, not something either
        // Wayfinder.Umbraco or this repo's own code asks for. Found live: every page that
        // mints an antiforgery token (the citizen stage form, the caseworker worklist) sent
        // X-Frame-Options TWICE: once from here, once from the framework's own default.
        // Two independent, identically-valued instances of the same header is a real
        // clickjacking-protection regression, not a cosmetic duplicate: some browsers treat a
        // header sent more than once as untrustworthy and ignore it entirely rather than pick
        // one value. SecurityHeadersOptions.FrameOptions already covers this
        // (configurably, including the ability to omit it), so suppress the framework's own
        // unconditional, unconfigurable copy so there is exactly one source of truth.
        //
        // A second, unrelated framework default fixed in the same place: AntiforgeryOptions's
        // own Cookie.SecurePolicy defaults to CookieSecurePolicy.None, not SameAsRequest as its
        // sibling cookie-auth handler does (confirmed: `new AntiforgeryOptions().Cookie
        // .SecurePolicy` is `None` out of the box), so the antiforgery cookie itself ships
        // with no Secure flag on every page that mints a token, found live via the same DAST
        // scan (Cookie Without Secure Flag [10011]) on both /apply-for-a-juggling-licence and
        // /caseworker-queue. TestSite (and every real deployment) is HTTPS-only, so there is no
        // legitimate plain-HTTP case this cookie needs to survive: Always, matching
        // PrismMemberCookie's own SecurePolicy.
        services.Configure<AntiforgeryOptions>(options =>
        {
            options.SuppressXFrameOptionsHeader = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        // The same default hides in MVC's TempData cookie (CookieTempDataProviderOptions's own
        // Cookie.SecurePolicy is None): any host page that POSTs through Wayfinder's stage
        // endpoint sets it, and the DAST baseline flagged it (Cookie Without Secure Flag [10011]
        // on /umbraco/wayfinder-stage/advance). HTTPS-only, like the cookies above.
        services.Configure<CookieTempDataProviderOptions>(options =>
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
    }
}
