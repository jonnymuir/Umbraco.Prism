using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Middleware;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Regression tests for two independent ASP.NET Core Antiforgery framework defaults, both found
/// live via the same DAST baseline scan and both fixed in the same PrismComposer configuration
/// block (SEC-PT2-004 follow-up):
///
/// 1. Duplicate X-Frame-Options: the framework's own antiforgery middleware sets
///    X-Frame-Options: SAMEORIGIN automatically whenever IAntiforgery.GetAndStoreTokens issues a
///    token — independently of, and in addition to, PrismSecurityHeadersMiddleware's own copy.
///    Every page that mints a token sent the header twice, which some browsers treat as
///    untrustworthy and ignore entirely, silently disabling clickjacking protection on exactly
///    the pages that most need it (real forms). Originally fixed via explicit suppression
///    (AntiforgeryOptions.SuppressXFrameOptionsHeader); PrismSecurityHeadersMiddleware's later
///    move to setting headers via the indexer (replacing, not Append-ing) closes the same
///    vulnerability class more generally, independently of that specific suppression flag — see
///    XFrameOptions_IsSentExactlyOnce_EvenWhenTheFrameworksOwnCopyIsNotSuppressed below.
/// 2. Antiforgery cookie missing Secure: AntiforgeryOptions's own Cookie.SecurePolicy defaults to
///    CookieSecurePolicy.None (confirmed: `new AntiforgeryOptions().Cookie.SecurePolicy` is
///    `None` out of the box, not SameAsRequest as might be assumed) — so the antiforgery cookie
///    itself shipped with no Secure flag on every token-minting page (ZAP: "Cookie Without
///    Secure Flag [10011]").
///
/// This exercises the real ASP.NET Core antiforgery middleware and PrismSecurityHeadersMiddleware
/// together through a minimal TestServer pipeline — not a mock, and not just an assertion that
/// an options flag is set — to prove the framework's own documented behaviour (suppressed only
/// when explicitly configured) is what this repo is actually relying on. UmbracoPrism.Core.
/// IntegrationTests' own booted TestSite fixture can't reach this: it seeds no front-end
/// content, so nothing there ever mints a token to reproduce the bugs against.
/// </summary>
public class PrismAntiforgeryXFrameOptionsTests
{
    private static async Task<IHost> BuildHostAsync(bool applyPrismAntiforgeryConfiguration)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddAntiforgery();
                    if (applyPrismAntiforgeryConfiguration)
                    {
                        // The exact configuration PrismComposer applies.
                        services.Configure<AntiforgeryOptions>(options =>
                        {
                            options.SuppressXFrameOptionsHeader = true;
                            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        });
                    }

                    services.Configure<PrismSecurityHeadersOptions>(_ => { });
                });
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<PrismSecurityHeadersMiddleware>();
                    app.Run(async context =>
                    {
                        // Stands in for any page that renders a form with an antiforgery
                        // token — Wayfinder.Umbraco's stage/worklist pages, in production.
                        context.RequestServices.GetRequiredService<IAntiforgery>()
                            .GetAndStoreTokens(context);
                        await context.Response.WriteAsync("ok");
                    });
                });
            });

        return await builder.StartAsync();
    }

    /// <summary>
    /// TestServer honours the request URI's scheme for HttpContext.Request.IsHttps (there's no
    /// real TLS negotiation) — needed because Cookie.SecurePolicy = Always makes ASP.NET Core's
    /// own antiforgery system throw on a non-SSL request (CheckSSLConfig), and TestServer's
    /// default client base address is http://. TestSite itself (and every real deployment) is
    /// HTTPS-only, so this matches production, not a workaround for it.
    /// </summary>
    private static HttpClient GetHttpsTestClient(IHost host)
    {
        var client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        return client;
    }

    [Fact]
    public async Task XFrameOptions_IsSentExactlyOnce_WhenTheFrameworksOwnCopyIsSuppressed()
    {
        using var host = await BuildHostAsync(applyPrismAntiforgeryConfiguration: true);
        using var client = GetHttpsTestClient(host);

        var response = await client.GetAsync("/");

        response.Headers.TryGetValues("X-Frame-Options", out var values).Should().BeTrue();
        values.Should().ContainSingle(
            "PrismSecurityHeadersOptions.FrameOptions is the one source of truth — the " +
            "framework's own antiforgery-triggered copy must be suppressed, not merely present " +
            "alongside it");
    }

    [Fact]
    public async Task XFrameOptions_IsSentExactlyOnce_EvenWhenTheFrameworksOwnCopyIsNotSuppressed()
    {
        // This used to be "IsSentTwice" — proving the duplicate was real when
        // SuppressXFrameOptionsHeader wasn't set. PrismSecurityHeadersMiddleware now SETS its
        // headers via the indexer (replacing any existing value) rather than Append, deferred to
        // OnStarting — which fires after GetAndStoreTokens' own synchronous, non-deferred header
        // write in this pipeline, so Prism's copy always wins regardless of suppression. That's a
        // second, more general layer of defence against the same vulnerability class (any header
        // this middleware owns being duplicated by something downstream, known or not), on top of
        // the explicit suppression flag PrismComposer still sets. Kept as its own test — proving
        // the general fix actually covers this specific historical case, not just a fresh one.
        using var host = await BuildHostAsync(applyPrismAntiforgeryConfiguration: false);
        using var client = GetHttpsTestClient(host);

        var response = await client.GetAsync("/");

        response.Headers.TryGetValues("X-Frame-Options", out var values).Should().BeTrue();
        values.Should().ContainSingle(
            "PrismSecurityHeadersMiddleware's indexer-based, OnStarting-deferred header set " +
            "overwrites the framework's own earlier copy, independently of explicit suppression");
    }

    [Fact]
    public async Task AntiforgeryCookie_CarriesSecureFlag_WhenPrismConfigurationIsApplied()
    {
        using var host = await BuildHostAsync(applyPrismAntiforgeryConfiguration: true);
        using var client = GetHttpsTestClient(host);

        var response = await client.GetAsync("/");

        var antiforgeryCookie = GetAntiforgerySetCookieHeader(response);
        antiforgeryCookie.Should().Contain("secure",
            "Cookie.SecurePolicy = Always must mark the antiforgery cookie itself Secure — " +
            "TestSite (and every real deployment) is HTTPS-only, so there is no legitimate " +
            "plain-HTTP case this cookie needs to survive");
    }

    [Fact]
    public async Task AntiforgeryCookie_LacksSecureFlag_WhenPrismConfigurationIsNotApplied()
    {
        // Proves the bug this fix addresses is real, not a misdiagnosis: AntiforgeryOptions's
        // own default Cookie.SecurePolicy is CookieSecurePolicy.None (confirmed directly:
        // `new AntiforgeryOptions().Cookie.SecurePolicy` is None out of the box), so without
        // PrismComposer's explicit override the antiforgery cookie genuinely ships with no
        // Secure flag — this is what the DAST scan caught as "Cookie Without Secure Flag [10011]".
        using var host = await BuildHostAsync(applyPrismAntiforgeryConfiguration: false);
        using var client = GetHttpsTestClient(host);

        var response = await client.GetAsync("/");

        var antiforgeryCookie = GetAntiforgerySetCookieHeader(response);
        antiforgeryCookie.Should().NotContain("secure",
            "this documents the live bug: the framework's own default antiforgery Cookie " +
            ".SecurePolicy is None, not SameAsRequest, unless explicitly overridden");
    }

    private static string GetAntiforgerySetCookieHeader(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var antiforgeryCookie = cookies!.SingleOrDefault(c =>
            c.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        antiforgeryCookie.Should().NotBeNull("the pipeline always mints one via GetAndStoreTokens");
        return antiforgeryCookie!;
    }
}
