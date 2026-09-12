using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Middleware;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Regression tests for SEC-PT2-004 — security response headers middleware.
///
/// The middleware defers header-setting to HttpResponse.OnStarting (see its own remarks:
/// header-clearing mid-pipeline elsewhere in the response, found live) — DefaultHttpContext's
/// own default IHttpResponseFeature never actually invokes registered OnStarting callbacks
/// (confirmed: neither Response.StartAsync() nor the HttpResponse.WriteAsync extension, which
/// calls StartAsync() internally, triggers them). FiringResponseFeature below is a thin
/// IHttpResponseFeature that captures OnStarting registrations and lets a test fire them on
/// demand — the standard workaround for testing OnStarting-based middleware without a full
/// TestServer boot.
/// </summary>
public class PrismSecurityHeadersMiddlewareTests
{
    private static PrismSecurityHeadersMiddleware BuildMiddleware(
        PrismSecurityHeadersOptions? options = null)
    {
        var opts = Options.Create(options ?? new PrismSecurityHeadersOptions());
        return new PrismSecurityHeadersMiddleware(_ => Task.CompletedTask, opts);
    }

    private static (DefaultHttpContext Context, FiringResponseFeature Feature) BuildHttpsContext(string path = "/")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Request.IsHttps = true;
        var feature = new FiringResponseFeature(ctx.Features.Get<IHttpResponseFeature>()!);
        ctx.Features.Set<IHttpResponseFeature>(feature);
        return (ctx, feature);
    }

    private static (DefaultHttpContext Context, FiringResponseFeature Feature) BuildHttpContext(string path = "/")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Request.IsHttps = false;
        var feature = new FiringResponseFeature(ctx.Features.Get<IHttpResponseFeature>()!);
        ctx.Features.Set<IHttpResponseFeature>(feature);
        return (ctx, feature);
    }

    [Fact]
    public async Task SecurityHeaders_AreApplied_OnDefaultHttpsRequest()
    {
        var middleware = BuildMiddleware();
        var (ctx, feature) = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        ctx.Response.Headers.Should().ContainKey("X-Content-Type-Options");
        ctx.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        ctx.Response.Headers.Should().ContainKey("X-Frame-Options");
        ctx.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
        ctx.Response.Headers.Should().ContainKey("Referrer-Policy");
        ctx.Response.Headers.Should().ContainKey("Permissions-Policy");
        ctx.Response.Headers.Should().ContainKey("Strict-Transport-Security");
        ctx.Response.Headers.Should().ContainKey("Content-Security-Policy");
    }

    [Fact]
    public async Task HstsHeader_IsOmitted_OnHttpRequest()
    {
        var middleware = BuildMiddleware();
        var (ctx, feature) = BuildHttpContext("/dashboard");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        ctx.Response.Headers.Should().NotContainKey("Strict-Transport-Security",
            "HSTS must only be set on HTTPS responses");
    }

    [Fact]
    public async Task SecurityHeaders_AreSkipped_ForBackofficeRoutes()
    {
        var middleware = BuildMiddleware();
        var (ctx, feature) = BuildHttpsContext("/umbraco/backoffice/api/something");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        ctx.Response.Headers.Should().NotContainKey("X-Content-Type-Options",
            "backoffice routes are excluded from Prism security headers by default");
        ctx.Response.Headers.Should().NotContainKey("X-Frame-Options");
    }

    [Fact]
    public async Task SecurityHeaders_AreApplied_ForBackofficeRoutes_WhenExcludeBackofficeIsFalse()
    {
        var options = new PrismSecurityHeadersOptions { ExcludeBackoffice = false };
        var middleware = BuildMiddleware(options);
        var (ctx, feature) = BuildHttpsContext("/umbraco/backoffice/api/something");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        ctx.Response.Headers.Should().ContainKey("X-Content-Type-Options");
    }

    [Fact]
    public async Task SecurityHeaders_AreNotApplied_WhenMiddlewareIsDisabled()
    {
        var options = new PrismSecurityHeadersOptions { Enabled = false };
        var middleware = BuildMiddleware(options);
        var (ctx, feature) = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        ctx.Response.Headers.Should().NotContainKey("X-Content-Type-Options",
            "middleware must be fully disabled when Enabled=false");
    }

    [Fact]
    public async Task ContentSecurityPolicy_IsEnforcedByDefault_WithNoUnsafeInline()
    {
        var middleware = BuildMiddleware();
        var (ctx, feature) = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        var csp = ctx.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().NotBeEmpty("CSP ships enforced by default — every asset Prism itself " +
            "renders is a real external resource, never spliced inline, so there is nothing " +
            "left that needs unsafe-inline");
        csp.Should().NotContain("unsafe-inline");
        csp.Should().Contain("object-src 'none'").And.Contain("base-uri 'self'")
            .And.Contain("form-action 'self'",
            "these directives don't fall back to default-src per spec (unlike most others) " +
            "— found live via ZAP baseline (rule 10055) once CSP went from Report-Only to enforced");
        csp.Should().NotContain("img-src 'self' data: https:",
            "a blanket https: image wildcard was an unjustified default (ZAP rule 10055, " +
            "\"CSP: Wildcard Directive\") — nothing in Prism renders a third-party HTTPS image");
        ctx.Response.Headers.Should().NotContainKey("Content-Security-Policy-Report-Only",
            "Report-Only is opt-in (null by default) — a host enables it itself to test a " +
            "stricter draft policy alongside the enforced one");
    }

    [Fact]
    public async Task HstsHeader_HasCorrectValue()
    {
        var middleware = BuildMiddleware();
        var (ctx, feature) = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        ctx.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task SecurityHeaders_SurviveAResponseResetAfterTheMiddlewareRan()
    {
        // The exact live regression this whole OnStarting design defends against: Umbraco's
        // own "no content matches this URL" 404 page resets response state further down the
        // pipeline (after this middleware ran) before writing its branded body — proven by
        // Umbraco's front-end 404 carrying none of these headers before this fix, even though
        // the middleware sat earlier in the pipeline. Headers set inline (Headers.Append
        // called directly, not deferred to OnStarting) would be wiped by that reset; headers
        // deferred to OnStarting are not, because OnStarting fires after everything downstream
        // has already decided the final response.
        var middleware = BuildMiddleware();
        var (ctx, feature) = BuildHttpsContext("/some-unrouted-path");

        await middleware.InvokeAsync(ctx);
        ctx.Response.Headers.Clear(); // simulates the downstream reset
        await feature.FireOnStartingAsync();

        ctx.Response.Headers.Should().ContainKey("X-Content-Type-Options",
            "OnStarting-deferred headers must survive a downstream response reset");
    }

    [Fact]
    public async Task SecurityHeaders_ReplaceRatherThanDuplicate_AHeaderAlreadySetByDownstreamCode()
    {
        // General-purpose regression for the whole class of bug PR #194 fixed one instance of
        // (ASP.NET Core's own Antiforgery middleware appending X-Frame-Options independently): a
        // header sent twice, even with identical values, is a real regression — some browsers
        // treat a duplicated header as untrustworthy and drop it entirely. Every header this
        // middleware owns must be idempotent against something else downstream already having
        // set it, not just the one case already known about.
        var middleware = BuildMiddleware();
        var (ctx, feature) = BuildHttpsContext("/dashboard");

        ctx.Response.Headers.Append("X-Frame-Options", "DENY"); // simulates something else setting it first
        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        ctx.Response.Headers.GetCommaSeparatedValues("X-Frame-Options").Should().Equal(["SAMEORIGIN"],
            "the header must be replaced, not appended to, however it was already set");
    }

    [Fact]
    public async Task ContentSecurityPolicy_AppendsAdditionalSourcesToAnExistingDirective()
    {
        var options = new PrismSecurityHeadersOptions
        {
            AdditionalContentSecurityPolicySources = new Dictionary<string, string>
            {
                ["img-src"] = "https://images.example.com"
            }
        };
        var middleware = BuildMiddleware(options);
        var (ctx, feature) = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        var csp = ctx.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("img-src 'self' data: https://images.example.com",
            "the host's extra source is appended to Prism's own existing img-src, not replacing it");
    }

    [Fact]
    public async Task ContentSecurityPolicy_AddsANewDirective_WhenPrismDoesNotAlreadyEmitIt()
    {
        var options = new PrismSecurityHeadersOptions
        {
            AdditionalContentSecurityPolicySources = new Dictionary<string, string>
            {
                ["frame-src"] = "https://payments.example.com"
            }
        };
        var middleware = BuildMiddleware(options);
        var (ctx, feature) = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        var csp = ctx.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("frame-src https://payments.example.com");
    }

    [Fact]
    public async Task ContentSecurityPolicyReportOnly_IsOptIn_AndIndependentOfTheEnforcedPolicy()
    {
        var options = new PrismSecurityHeadersOptions
        {
            ContentSecurityPolicyReportOnly = "default-src 'none'"
        };
        var middleware = BuildMiddleware(options);
        var (ctx, feature) = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);
        await feature.FireOnStartingAsync();

        ctx.Response.Headers["Content-Security-Policy-Report-Only"].ToString().Should().Be("default-src 'none'");
        ctx.Response.Headers.Should().ContainKey("Content-Security-Policy",
            "the enforced policy stays active — Report-Only is additive, not a replacement");
    }

    private sealed class FiringResponseFeature(IHttpResponseFeature inner) : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object? State)> _onStarting = [];

        public void OnStarting(Func<object, Task> callback, object state) =>
            _onStarting.Add((callback, state));

        public async Task FireOnStartingAsync()
        {
            foreach (var (callback, state) in _onStarting)
                await callback(state!);
        }

        public void OnCompleted(Func<object, Task> callback, object state) =>
            inner.OnCompleted(callback, state);

        public int StatusCode
        {
            get => inner.StatusCode;
            set => inner.StatusCode = value;
        }

        public string? ReasonPhrase
        {
            get => inner.ReasonPhrase;
            set => inner.ReasonPhrase = value;
        }

        public IHeaderDictionary Headers
        {
            get => inner.Headers;
            set => inner.Headers = value;
        }

        public Stream Body
        {
            get => inner.Body;
            set => inner.Body = value;
        }

        public bool HasStarted => inner.HasStarted;
    }
}
