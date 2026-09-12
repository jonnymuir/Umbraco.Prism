using System.Net;
using FluentAssertions;

namespace UmbracoPrism.Core.IntegrationTests;

/// <summary>
/// SECURITY REGRESSION — auth-contract Layer 2: the authorization <em>behaviour</em> of Prism's
/// HTTP surface, exercised through the real booted UmbracoPrism.TestSite host.
///
/// AuthorizationContractTests (Layer 1) proves each endpoint <em>declares</em> a decision; this
/// proves the pipeline <em>enforces</em> it — [Authorize] actually challenges, the policies
/// actually deny, the antiforgery filter actually fires. Deny paths only: they need neither
/// Keycloak nor a real identity. The authenticated end-to-end stays in localhost-auth-playwright.
/// </summary>
[Collection(BootedTestSite.Name)]
public sealed class AuthorizationBehaviourTests(TestSiteFactory factory)
{
    // BaseAddress must be https:// — TestSite is HTTPS-only in every real deployment, and
    // AntiforgeryOptions.Cookie.SecurePolicy = Always (PrismComposer, SEC-PT2-004 follow-up)
    // makes ASP.NET Core's own antiforgery system hard-throw (CheckSSLConfig) on a non-HTTPS
    // request for both minting and validating a token — WebApplicationFactory's default
    // http://localhost base address doesn't match that reality and would 500 every
    // antiforgery-guarded request here instead of exercising the real deny path.
    private HttpClient Anonymous() => factory.CreateClient(new()
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    // ---- Endpoints behind [Authorize(AuthenticationSchemes = "PrismMemberCookie")] ----

    [Theory]
    [InlineData("DELETE", "/api/prism/device/some-device")]           // DeviceAdminController (+ PrismAdmins)
    [InlineData("POST", "/umbraco/prism/mobile/biometric/register")]  // BiometricController.Register
    [InlineData("DELETE", "/umbraco/prism/mobile/biometric/revoke")]  // BiometricController.Revoke
    [InlineData("POST", "/umbraco/prism/push/register")]              // PrismNotificationController
    [InlineData("POST", "/umbraco/prism/push/subscribe")]
    [InlineData("POST", "/umbraco/prism/vinyl/back-in-stock")]        // PrismVinylNotificationController (TestSite)
    public async Task Member_scheme_endpoints_reject_an_anonymous_caller(string method, string path)
    {
        using var client = Anonymous();
        using var req = new HttpRequestMessage(new HttpMethod(method), path);

        var res = await client.SendAsync(req);

        res.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Redirect },
            "{0} {1} is behind [Authorize] — an unauthenticated call must be rejected before the action runs",
            method, path);
    }

    // ---- Management API: [Authorize(Policy = BackOfficeAccess)] + [Authorize(Policy = "PrismAdmins")] ----

    [Fact]
    public async Task Tenant_management_api_rejects_an_anonymous_caller()
    {
        using var client = Anonymous();
        var res = await client.GetAsync("/umbraco/management/api/v1/prism/tenants");
        res.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
    }

    // ---- BiometricController.Exchange is [AllowAnonymous] — the JWT is the credential ----

    [Fact]
    public async Task Biometric_exchange_is_reachable_anonymously_and_issues_no_session_without_a_token()
    {
        using var client = Anonymous();
        using var body = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        factory.DrainRecentErrorLogs(); // discard anything logged/thrown before this request
        factory.DrainRawExceptions();
        var res = await client.PostAsync("/umbraco/prism/mobile/biometric/exchange", body);

        // The contract this asserts: Exchange is [AllowAnonymous] (the request reaches the action
        // rather than being challenged by the auth middleware) and, given no valid BiometricToken
        // JWT, it never issues a PrismMemberCookie session.
        //
        // RESOLVED — this was never a cold-runner timing flake at all, despite the framing on the
        // original note; it was 100% deterministic in CI from the very first run, just never
        // actually diagnosed until the response-body capture below finally showed the real
        // exception: BiometricTokenService's own constructor throws InvalidOperationException when
        // Prism:Biometric:SigningKey is absent — during controller DI activation, before Exchange's
        // own try/catch (or any middleware-level one) can reach it. UmbracoPrism.TestSite normally
        // gets that value from `dotnet user-secrets` (its UserSecretsId), auto-loaded because this
        // factory forces the Development environment — user secrets live outside the repo, so every
        // developer machine that ever ran `dotnet user-secrets set` for this project had it and CI
        // never did. Fixed in TestSiteFactory's own config (see its comment) with fixed test-only
        // values; confirmed by removing the local secrets file entirely and re-running, which
        // reproduced the exact CI failure locally for the first time, then passed once fixed.
        // Two other things landed chasing this, both kept as good on their own merits even though
        // neither was the actual cause: TenantService.GetByDomainAsync no longer crashes the request
        // on a database failure during tenant lookup, and RawExceptionCapture (an IStartupFilter
        // wrapping the entire pipeline) backstops HostErrorLogCapture for whatever the next one is.
        if (res.StatusCode == HttpStatusCode.InternalServerError)
        {
            var rawExceptions = factory.DrainRawExceptions();
            var errors = factory.DrainRecentErrorLogs();
            var responseBody = await res.Content.ReadAsStringAsync();
            var responseHeaders = string.Join(", ", res.Headers
                .Concat(res.Content.Headers)
                .Select(h => $"{h.Key}={string.Join("|", h.Value)}"));
            var details = rawExceptions.Count > 0
                ? string.Join("\n---\n", rawExceptions.Select(ex => ex.ToString()))
                : errors.Count > 0
                    ? string.Join("\n---\n", errors)
                    : "(no exception captured by RawExceptionCapture and no Error/Critical host log either — the response may have started successfully and failed while writing the body, outside any middleware's try/catch)";
            Assert.Fail(
                $"Exchange returned 500 unexpectedly.\n" +
                $"Response headers: {responseHeaders}\n" +
                $"Response body: {(string.IsNullOrEmpty(responseBody) ? "(empty)" : responseBody)}\n" +
                $"Captured diagnostics for this request:\n{details}");
        }

        res.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "Exchange is [AllowAnonymous]");
        res.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, "Exchange is [AllowAnonymous]");
        res.Headers.Contains("Set-Cookie").Should().BeFalse("no session may be issued without a valid biometric token");
    }

    // ---- AccountController.Logout: [AllowAnonymous] class, [ValidateAntiForgeryToken] (SEC-PT2-003) ----

    [Fact]
    public async Task Logout_rejects_a_post_with_no_antiforgery_token()
    {
        using var client = Anonymous();
        var res = await client.PostAsync("/auth/logout", content: null);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "[ValidateAntiForgeryToken] guards logout-CSRF; a 400 (not 401) also confirms the class is [AllowAnonymous]");
    }

    // ---- TestSite public file download: [AllowAnonymous], engine ownership is the boundary ----

    [Fact]
    public async Task Public_file_download_is_not_an_idor__an_unknown_instance_is_404_never_the_file()
    {
        using var client = Anonymous();
        var res = await client.GetAsync($"/service-request/files/{Guid.NewGuid()}/someField");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        res.Content.Headers.ContentDisposition.Should().BeNull("no file may be served for an unowned instance");
    }

}
