using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Middleware;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Regression test for the duplicate-X-Frame-Options bug (SEC-PT2-004 follow-up): ASP.NET
/// Core's own antiforgery middleware sets X-Frame-Options: SAMEORIGIN automatically whenever
/// IAntiforgery.GetAndStoreTokens issues a token — independently of, and in addition to,
/// PrismSecurityHeadersMiddleware's own copy. Found live via the DAST baseline scan: every
/// page that mints a token sent the header twice, which some browsers treat as untrustworthy
/// and ignore entirely, silently disabling clickjacking protection on exactly the pages that
/// most need it (real forms).
///
/// This exercises the real ASP.NET Core antiforgery middleware and PrismSecurityHeadersMiddleware
/// together through a minimal TestServer pipeline — not a mock, and not just an assertion that
/// an options flag is set — to prove the framework's own documented behaviour (suppressed when
/// AntiforgeryOptions.SuppressXFrameOptionsHeader = true) is what this repo is actually relying
/// on. UmbracoPrism.Core.IntegrationTests' own booted TestSite fixture can't reach this: it seeds
/// no front-end content, so nothing there ever mints a token to reproduce the bug against.
/// </summary>
public class PrismAntiforgeryXFrameOptionsTests
{
    private static async Task<IHost> BuildHostAsync(bool suppressXFrameOptionsHeader)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddAntiforgery();
                    if (suppressXFrameOptionsHeader)
                    {
                        // The exact configuration PrismComposer applies.
                        services.Configure<AntiforgeryOptions>(options =>
                            options.SuppressXFrameOptionsHeader = true);
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

    [Fact]
    public async Task XFrameOptions_IsSentExactlyOnce_WhenTheFrameworksOwnCopyIsSuppressed()
    {
        using var host = await BuildHostAsync(suppressXFrameOptionsHeader: true);
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/");

        response.Headers.TryGetValues("X-Frame-Options", out var values).Should().BeTrue();
        values.Should().ContainSingle(
            "PrismSecurityHeadersOptions.FrameOptions is the one source of truth — the " +
            "framework's own antiforgery-triggered copy must be suppressed, not merely present " +
            "alongside it");
    }

    [Fact]
    public async Task XFrameOptions_IsSentTwice_WhenTheFrameworksOwnCopyIsNotSuppressed()
    {
        // Proves the bug this suppression fixes is real, not a misdiagnosis: without
        // AntiforgeryOptions.SuppressXFrameOptionsHeader, the same minimal pipeline —
        // PrismSecurityHeadersMiddleware plus a single GetAndStoreTokens call — genuinely
        // does send the header twice.
        using var host = await BuildHostAsync(suppressXFrameOptionsHeader: false);
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/");

        response.Headers.TryGetValues("X-Frame-Options", out var values).Should().BeTrue();
        values.Should().HaveCount(2,
            "this documents the live bug: ASP.NET Core's own antiforgery middleware adds its " +
            "own X-Frame-Options on top of PrismSecurityHeadersMiddleware's, unless suppressed");
    }
}
