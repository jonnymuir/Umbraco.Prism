using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.TestSite.Controllers;

/// <summary>
/// API endpoint used by the dashboard's downstream demo section.
/// Calls a configured URL using the current member's Prism Bearer token
/// and returns the raw response so the dashboard can display it inline.
/// 
/// SECURITY: This endpoint is restricted to Development environment or
/// when explicitly enabled via configuration to prevent token forwarding
/// to arbitrary URLs in production.
/// </summary>
[ApiController]
[Route("api/prism/downstream-demo")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class DownstreamDemoController(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IPrismContext prismContext,
    IPublishedContentQuery publishedContentQuery,
    IWebHostEnvironment environment) : ControllerBase
{
    private static readonly JsonSerializerOptions PrettyPrint = new() { WriteIndented = true };

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? url = null)
    {
        if (!IsDemoEnabled())
        {
            return StatusCode(403, new 
            { 
                error = "Downstream demo is disabled in this environment for security reasons.",
                hint = "Set Prism:EnableDownstreamDemo to true in appsettings if you need this feature outside Development."
            });
        }

        // Phase 1 Security: Validate URL is in allowlist if custom URL provided
        if (!string.IsNullOrWhiteSpace(url) && !IsUrlAllowed(url))
        {
            return BadRequest(new 
            { 
                error = "The provided URL is not in the allowlist.",
                hint = "Only configured business app URLs are allowed. Configure Prism:DownstreamDemo:AllowedUrls in appsettings."
            });
        }

        var authHeader = await prismContext.GetAuthorizationHeaderAsync();
        if (authHeader == null)
            return Unauthorized(new { error = "No Prism session — please sign in again." });

        var targetUrl = BuildTargetUrl(url);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await SendDownstreamRequestAsync(targetUrl, authHeader);
            sw.Stop();

            var rawBody = await response.Content.ReadAsStringAsync();

            // Attempt pretty-print if the response is JSON
            string displayBody;
            try
            {
                var doc = JsonDocument.Parse(rawBody);
                displayBody = JsonSerializer.Serialize(doc, PrettyPrint);
            }
            catch
            {
                displayBody = rawBody;
            }

            return Ok(new
            {
                statusCode = (int)response.StatusCode,
                statusText = response.StatusCode.ToString(),
                url = targetUrl,
                elapsedMs = sw.ElapsedMilliseconds,
                contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown",
                body = displayBody
            });
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return Ok(new
            {
                statusCode = 0,
                statusText = "Timeout",
                url = targetUrl,
                elapsedMs = sw.ElapsedMilliseconds,
                contentType = "none",
                body = "Request timed out after 10 seconds. Is MockBusinessApp running?"
            });
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return Ok(new
            {
                statusCode = 0,
                statusText = "Network Error",
                url = targetUrl,
                elapsedMs = sw.ElapsedMilliseconds,
                contentType = "none",
                body = $"Could not reach the service: {ex.Message}\n\nMake sure MockBusinessApp is running:\n  dotnet run --project src/UmbracoPrism.MockBusinessApp"
            });
        }
    }

    [HttpGet("session-contract")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSessionContract()
    {
        if (!IsDemoEnabled())
        {
            return StatusCode(403, new
            {
                error = "Downstream demo is disabled in this environment for security reasons.",
                hint = "Set Prism:EnableDownstreamDemo to true in appsettings if you need this feature outside Development."
            });
        }

        var authResult = await HttpContext.AuthenticateAsync("PrismMemberCookie");
        var tokens = authResult.Properties?.GetTokens()?.ToArray() ?? [];
        var accessToken = GetTokenValue(tokens, "access_token");
        var refreshToken = GetTokenValue(tokens, "refresh_token");
        var idToken = GetTokenValue(tokens, "id_token");
        var expiresAt = GetTokenValue(tokens, "expires_at");
        var authHeader = authResult.Succeeded ? await prismContext.GetAuthorizationHeaderAsync() : null;
        var tenant = prismContext.CurrentTenant;

        return Ok(new
        {
            tenant = new
            {
                resolved = tenant != null,
                hostname = tenant?.Hostname,
                mode = GetTenantMode(tenant),
                oidcAuthority = tenant?.OidcAuthority,
                oidcClientId = tenant?.OidcClientId,
                entraTenantId = tenant?.EntraTenantId,
                entraClientId = tenant?.EntraClientId
            },
            cookie = new
            {
                isAuthenticated = authResult.Succeeded,
                hasAccessToken = !string.IsNullOrWhiteSpace(accessToken),
                hasRefreshToken = !string.IsNullOrWhiteSpace(refreshToken),
                hasIdToken = !string.IsNullOrWhiteSpace(idToken),
                expiresAt,
                accessTokenExpired = IsExpired(expiresAt)
            },
            downstream = new
            {
                authorizationHeaderReady = authHeader != null,
                scheme = authHeader?.Scheme,
                failureReason = prismContext.LastAuthorizationFailureReason
            },
            logout = new
            {
                endSessionEndpoint = BuildLogoutEndpoint(tenant),
                clientId = tenant?.OidcClientId ?? tenant?.EntraClientId,
                idTokenHintReady = !string.IsNullOrWhiteSpace(idToken),
                signedOutCallbackPath = "/signout-callback-oidc"
            },
            seed = BuildSeedContract()
        });
    }

    [HttpGet("seed-contract-ready")]
    [AllowAnonymous]
    public IActionResult GetSeedContractReady()
    {
        if (!IsDemoEnabled())
        {
            return StatusCode(403, new
            {
                error = "Downstream demo is disabled in this environment for security reasons.",
                hint = "Set Prism:EnableDownstreamDemo to true in appsettings if you need this feature outside Development."
            });
        }

        var contract = BuildSeedContract();

        return contract.Ready
            ? Ok(contract)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, contract);
    }

    private string BuildTargetUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
            return url;

        // In Codespaces, BUSINESSAPP_BACKCHANNEL_URL points to the internal endpoint
        // for server-to-server communication (bypasses GitHub port-forwarding proxy).
        // Outside Codespaces, falls back to PrismBusinessApp:WorkflowApiBaseUrl.
        var baseUrl = configuration["BUSINESSAPP_BACKCHANNEL_URL"]?.TrimEnd('/')
            ?? configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("PrismBusinessApp:WorkflowApiBaseUrl is not configured.");

        return $"{baseUrl}/api/backoffice/me";
    }

    private async Task<HttpResponseMessage> SendDownstreamRequestAsync(
        string targetUrl,
        AuthenticationHeaderValue authHeader)
    {
        var response = await SendDownstreamRequestCoreAsync(targetUrl, authHeader);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        var refreshedHeader = await prismContext.GetAuthorizationHeaderAsync(forceRefresh: true);
        if (refreshedHeader == null)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return await SendDownstreamRequestCoreAsync(targetUrl, refreshedHeader);
    }

    private async Task<HttpResponseMessage> SendDownstreamRequestCoreAsync(
        string targetUrl,
        AuthenticationHeaderValue authHeader)
    {
        var client = httpClientFactory.CreateClient("prism-downstream-demo");
        client.DefaultRequestHeaders.Authorization = authHeader;
        client.Timeout = TimeSpan.FromSeconds(10);
        return await client.GetAsync(targetUrl);
    }

    private bool IsDemoEnabled()
    {
        return environment.IsDevelopment()
            || configuration.GetValue<bool>("Prism:EnableDownstreamDemo", false);
    }

    private bool IsUrlAllowed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var allowedUrls = configuration.GetSection("Prism:DownstreamDemo:AllowedUrls").Get<string[]>();
        if (allowedUrls != null)
        {
            foreach (var allowedUrl in allowedUrls)
            {
                if (UrlStartsWithAllowed(url, allowedUrl.TrimEnd('/')))
                    return true;
            }
        }

        var defaultBaseUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(defaultBaseUrl) && UrlStartsWithAllowed(url, defaultBaseUrl))
            return true;

        if (environment.IsDevelopment() && 
            (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || 
             uri.Host.Equals("127.0.0.1", StringComparison.Ordinal)))
            return true;

        return false;
    }

    private static string? GetTokenValue(IEnumerable<AuthenticationToken> tokens, string name)
    {
        return tokens.FirstOrDefault(token => token.Name == name)?.Value;
    }

    private static bool IsExpired(string? expiresAt)
    {
        return DateTimeOffset.TryParse(expiresAt, out var parsed)
            && parsed <= DateTimeOffset.UtcNow.AddMinutes(1);
    }

    private static string GetTenantMode(PrismTenant? tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant?.OidcAuthority))
        {
            return "generic-oidc";
        }

        if (!string.IsNullOrWhiteSpace(tenant?.EntraTenantId))
        {
            return "entra";
        }

        return "none";
    }

    private static string? BuildLogoutEndpoint(PrismTenant? tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant?.OidcAuthority))
        {
            return $"{tenant.OidcAuthority.TrimEnd('/')}/protocol/openid-connect/logout";
        }

        if (!string.IsNullOrWhiteSpace(tenant?.EntraTenantId))
        {
            return $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/oauth2/v2.0/logout";
        }

        return null;
    }

    private static bool UrlStartsWithAllowed(string url, string allowedPrefix)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri) ||
            !Uri.TryCreate(allowedPrefix, UriKind.Absolute, out var allowedUri))
        {
            return false;
        }

        return targetUri.Scheme.Equals(allowedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               targetUri.Host.Equals(allowedUri.Host, StringComparison.OrdinalIgnoreCase) &&
               targetUri.Port == allowedUri.Port &&
               targetUri.AbsolutePath.StartsWith(allowedUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    private SeedContractStatus BuildSeedContract()
    {
        var roots = publishedContentQuery.ContentAtRoot().ToList();
        var home = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedByAlias(roots, TestSiteSeedContract.HomePageAlias),
            TestSiteSeedContract.HomePageAlias,
            TestSiteSeedContract.HomePageName,
            TestSiteSeedContract.HomePageUrl);
        var dashboard = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedByAlias(roots, TestSiteSeedContract.DashboardAlias),
            TestSiteSeedContract.DashboardAlias,
            TestSiteSeedContract.DashboardName,
            TestSiteSeedContract.DashboardUrl);
        var workflowPage = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedWorkflowPage(roots, TestSiteSeedContract.WorkflowKey),
            TestSiteSeedContract.WorkflowPageAlias,
            TestSiteSeedContract.WorkflowPageName,
            TestSiteSeedContract.WorkflowPageUrl);
        var workflowHub = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedByAlias(roots, TestSiteSeedContract.WorkflowHubAlias),
            TestSiteSeedContract.WorkflowHubAlias,
            TestSiteSeedContract.WorkflowHubName,
            TestSiteSeedContract.WorkflowHubUrl);
        var planningWorkflowPage = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedWorkflowPage(roots, TestSiteSeedContract.PlanningWorkflowKey),
            TestSiteSeedContract.WorkflowPageAlias,
            TestSiteSeedContract.PlanningWorkflowPageName,
            TestSiteSeedContract.PlanningWorkflowPageUrl);
        var paymentDemoPage = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedWorkflowPage(roots, TestSiteSeedContract.PaymentDemoWorkflowKey),
            TestSiteSeedContract.WorkflowPageAlias,
            TestSiteSeedContract.PaymentDemoPageName,
            TestSiteSeedContract.PaymentDemoPageUrl);
        var informationRequestPage = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedWorkflowPage(roots, TestSiteSeedContract.InformationRequestWorkflowKey),
            TestSiteSeedContract.WorkflowPageAlias,
            TestSiteSeedContract.InformationRequestPageName,
            TestSiteSeedContract.InformationRequestPageUrl);
        var settings = TestSiteSeedContract.FindPublishedByAlias(roots, TestSiteSeedContract.SettingsAlias);
        var mobileNav = BuildMobileNavStatus(settings);
        var challengePath = $"/auth/login?ReturnUrl={Uri.EscapeDataString(TestSiteSeedContract.WorkflowHubUrl)}";
        // routeContractReady waits for every authored URL the Playwright suite navigates to, so
        // the first request to any of them lands on a fully-warm Umbraco route + Razor view —
        // not a cold-start that returns 404 / Home / a half-rendered page (the symptom that
        // showed up after the v2.0 polymorphic component schema rollout made first-render
        // view compilation slower than the test's 5s default visibility timeout).
        var routeContractReady =
            home.MatchesExpected &&
            dashboard.MatchesExpected &&
            workflowPage.MatchesExpected &&
            workflowHub.MatchesExpected &&
            planningWorkflowPage.MatchesExpected &&
            paymentDemoPage.MatchesExpected &&
            informationRequestPage.MatchesExpected &&
            mobileNav.Ready;

        return new SeedContractStatus(
            Ready: routeContractReady,
            RouteContractReady: routeContractReady,
            Auth: new SeedAuthStatus("/auth/login", "/auth/logout", challengePath),
            Home: home,
            Dashboard: dashboard,
            WorkflowPage: workflowPage,
            WorkflowHub: workflowHub,
            PlanningWorkflowPage: planningWorkflowPage,
            PaymentDemoPage: paymentDemoPage,
            InformationRequestPage: informationRequestPage,
            MobileNav: mobileNav);
    }

    private static SeededRouteStatus BuildSeededRoute(
        IPublishedContent? content,
        string alias,
        string expectedName,
        string expectedUrl)
    {
        var url = NormalizePath(content?.Url());
        var matchesExpected =
            content != null &&
            string.Equals(content.Name, expectedName, StringComparison.Ordinal) &&
            string.Equals(url, NormalizePath(expectedUrl), StringComparison.OrdinalIgnoreCase);

        return new SeededRouteStatus(alias, expectedName, expectedUrl, content != null, url, matchesExpected);
    }

    private static MobileNavStatus BuildMobileNavStatus(IPublishedContent? settings)
    {
        var mobileNavLinks = settings?.Value<BlockListModel>("mobileNavLinks");
        var navUrls = mobileNavLinks?
            .Select(block => NormalizePath(block.Content?.Value<string>("navUrl")))
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [];
        var hasHome = navUrls.Contains(NormalizePath(TestSiteSeedContract.HomePageUrl));
        var hasDashboard = navUrls.Contains(NormalizePath(TestSiteSeedContract.DashboardUrl));
        var hasWorkflowHub = navUrls.Contains(NormalizePath(TestSiteSeedContract.WorkflowHubUrl));
        var ready =
            settings != null &&
            mobileNavLinks != null &&
            mobileNavLinks.Any() &&
            hasHome &&
            hasDashboard &&
            hasWorkflowHub;

        return new MobileNavStatus(settings != null, mobileNavLinks?.Count ?? 0, hasHome, hasDashboard, hasWorkflowHub, ready);
    }

    private sealed record SeedContractStatus(
        bool Ready,
        bool RouteContractReady,
        SeedAuthStatus Auth,
        SeededRouteStatus Home,
        SeededRouteStatus Dashboard,
        SeededRouteStatus WorkflowPage,
        SeededRouteStatus WorkflowHub,
        SeededRouteStatus PlanningWorkflowPage,
        SeededRouteStatus PaymentDemoPage,
        SeededRouteStatus InformationRequestPage,
        MobileNavStatus MobileNav);

    private sealed record SeedAuthStatus(string LoginPath, string LogoutPath, string ChallengePath);

    private sealed record SeededRouteStatus(
        string Alias,
        string ExpectedName,
        string ExpectedUrl,
        bool Published,
        string? Url,
        bool MatchesExpected);

    private sealed record MobileNavStatus(
        bool Published,
        int ItemCount,
        bool HasHome,
        bool HasDashboard,
        bool HasWorkflowHub,
        bool Ready);

    private static string NormalizePath(string? path)
        => TestSiteSeedContract.NormalizeUrl(path);
}
