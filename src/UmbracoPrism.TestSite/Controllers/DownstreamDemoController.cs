using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    IWebHostEnvironment environment,
    ILogger<DownstreamDemoController> logger) : ControllerBase
{
    private const int MaxDiagnosticBodyLength = 4096;
    private const int DownstreamTimeoutSeconds = 10;
    private const int DownstreamTimeoutMs = DownstreamTimeoutSeconds * 1000;
    private const string DiagnosticBodyTruncationNotice = "\n\n[Response body truncated for display.]";
    private const string CallerTraceIdHeaderName = "X-Prism-Caller-TraceId";
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
        var transportDiagnostics = BuildTransportDiagnostics(targetUrl);
        var callerTraceId = GetCallerTraceId();

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await SendDownstreamRequestAsync(targetUrl, authHeader);
            sw.Stop();

            var rawBody = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";

            // Validate that the response is JSON, not HTML or other non-JSON content
            // This catches Codespaces port-forwarding pages ("Connecting to the forwarded port...")
            // and other misconfigured endpoints that return HTML/plain text instead of JSON
            if (!IsJsonContentType(contentType))
            {
                var errorMessage = BuildInvalidResponseSummary(response, contentType, targetUrl, rawBody);
                var summary = BuildInvalidResponseUiSummary(response, contentType, targetUrl);
                var diagnosticBody = CreateDiagnosticBody(response, contentType, rawBody, out var diagnosticBodyTruncated);

                logger.LogWarning(
                    "Downstream demo received non-JSON response from {TargetUrl}. HTTP {StatusCode} {ReasonPhrase}; callerTraceId {CallerTraceId}; content-type {ContentType}; transport {Transport}; backchannel {Backchannel}; headers {Headers}; bodyLength {BodyLength}",
                    targetUrl,
                    (int)response.StatusCode,
                    response.ReasonPhrase ?? response.StatusCode.ToString(),
                    callerTraceId,
                    contentType,
                    transportDiagnostics.Transport,
                    transportDiagnostics.BackchannelPresent,
                    FormatHeaders(response),
                    rawBody.Length);

                return Ok(new
                {
                    statusCode = (int)response.StatusCode,
                    statusText = response.ReasonPhrase ?? response.StatusCode.ToString(),
                    url = TransformToDisplayUrl(targetUrl),
                    elapsedMs = sw.ElapsedMilliseconds,
                    contentType,
                    body = errorMessage,
                    summary,
                    diagnosticBody,
                    diagnosticBodyTruncated,
                    invalidResponse = true,
                    transport = CreateTransportPayload(transportDiagnostics)
                });
            }

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
                url = TransformToDisplayUrl(targetUrl),
                elapsedMs = sw.ElapsedMilliseconds,
                contentType,
                body = displayBody,
                transport = CreateTransportPayload(transportDiagnostics)
            });
        }
        catch (DownstreamRequestCancelledException ex)
        {
            sw.Stop();
            var cancellationSource = ex.TimedOutByRequestWindow
                ? "request-timeout-window"
                : HttpContext?.RequestAborted.IsCancellationRequested == true
                    ? "request-aborted"
                    : "external-cancellation";

            logger.LogWarning(
                ex,
                "Downstream demo request did not complete calling {TargetUrl} after {ElapsedMs}ms. CallerTraceId: {CallerTraceId}; Transport: {Transport}; Backchannel present: {BackchannelPresent}; TargetPath: {TargetPath}; TransportBaseUrl: {TransportBaseUrl}; Cancellation source: {CancellationSource}",
                targetUrl,
                sw.ElapsedMilliseconds,
                callerTraceId,
                transportDiagnostics.Transport,
                transportDiagnostics.BackchannelPresent,
                transportDiagnostics.TargetPath,
                transportDiagnostics.TransportBaseUrl,
                cancellationSource);

            var timeoutDetail = ex.TimedOutByRequestWindow
                ? $"Request timed out after {DownstreamTimeoutSeconds} seconds."
                : "Request was cancelled before completion.";

            var summary = ex.TimedOutByRequestWindow
                ? $"Timed out after {DownstreamTimeoutSeconds} seconds via {transportDiagnostics.Transport} while targeting {transportDiagnostics.TargetPath}."
                : $"Request was cancelled before the {DownstreamTimeoutSeconds}-second timeout while targeting {transportDiagnostics.TargetPath}.";
            var hint = BuildTimeoutHint(transportDiagnostics);
            var nextCheck = BuildTimeoutNextCheck(transportDiagnostics);

            return Ok(new
            {
                statusCode = 0,
                statusText = ex.TimedOutByRequestWindow ? "Timeout" : "Cancelled",
                url = TransformToDisplayUrl(targetUrl),
                elapsedMs = sw.ElapsedMilliseconds,
                contentType = "none",
                summary,
                nextCheck,
                body = $"{timeoutDetail} {hint}",
                timeout = new
                {
                    timedOutByUs = ex.TimedOutByRequestWindow,
                    timeoutWindowMs = DownstreamTimeoutMs,
                    cancellationSource,
                    detail = timeoutDetail
                },
                transport = CreateTransportPayload(transportDiagnostics)
            });
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            logger.LogWarning(
                ex,
                "Downstream demo could not reach {TargetUrl} after {ElapsedMs}ms. CallerTraceId: {CallerTraceId}; Transport: {Transport}; Backchannel present: {BackchannelPresent}; TargetPath: {TargetPath}; TransportBaseUrl: {TransportBaseUrl}",
                targetUrl,
                sw.ElapsedMilliseconds,
                callerTraceId,
                transportDiagnostics.Transport,
                transportDiagnostics.BackchannelPresent,
                transportDiagnostics.TargetPath,
                transportDiagnostics.TransportBaseUrl);
            
            var hint = transportDiagnostics.Transport == "internal-backchannel"
                ? "\n\nThe request used an internal backchannel URL. If running in Codespaces, check that AppHost is passing the correct dynamic endpoint. Try `bash scripts/codespaces/refresh.sh`."
                : "\n\nMake sure MockBusinessApp is running:\n  dotnet run --project src/UmbracoPrism.MockBusinessApp";
            
            return Ok(new
            {
                statusCode = 0,
                statusText = "Network Error",
                url = TransformToDisplayUrl(targetUrl),
                elapsedMs = sw.ElapsedMilliseconds,
                contentType = "none",
                body = $"Could not reach the service: {ex.Message}{hint}",
                transport = CreateTransportPayload(transportDiagnostics)
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

        var baseUrl = ResolveBusinessAppTransportBaseUrl();
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
        using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        request.Headers.Authorization = authHeader;
        request.Headers.TryAddWithoutValidation(CallerTraceIdHeaderName, GetCallerTraceId());
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DownstreamTimeoutSeconds));

        try
        {
            return await client.SendAsync(request, timeoutCts.Token);
        }
        catch (TaskCanceledException ex)
        {
            throw new DownstreamRequestCancelledException(
                timeoutCts.IsCancellationRequested || ex.CancellationToken.IsCancellationRequested,
                ex);
        }
    }

    private bool IsDemoEnabled()
    {
        return environment.IsDevelopment()
            || configuration.GetValue<bool>("Prism:EnableDownstreamDemo", false);
    }

    private string GetCallerTraceId() => HttpContext?.TraceIdentifier ?? "unknown";

    private TransportDiagnostics BuildTransportDiagnostics(string targetUrl)
    {
        var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
        var backchannelPresent = !string.IsNullOrWhiteSpace(backchannelUrl);
        
        string transport;
        string transportBaseUrl;
        
        if (backchannelPresent)
        {
            transport = "internal-backchannel";
            transportBaseUrl = MaskInternalUrl(backchannelUrl!);
        }
        else
        {
            var publicUrl = configuration["PrismBusinessApp:ApiBaseUrl"]?.TrimEnd('/');
            transport = IsCodespacesUrl(publicUrl ?? "") ? "public-tunnel" : "public-url";
            transportBaseUrl = MaskPublicUrl(publicUrl ?? "");
        }

        var targetUri = Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) ? uri : null;
        var targetUrlScheme = targetUri?.Scheme ?? "unknown";
        var targetPath = targetUri?.AbsolutePath ?? "/";

        return new TransportDiagnostics(
            transport,
            transport == "internal-backchannel",
            backchannelPresent,
            transportBaseUrl,
            targetUrlScheme,
            targetPath);
    }

    private static string MaskInternalUrl(string url)
    {
        // Show scheme and localhost indicator but not the actual port for security
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var hostIndicator = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                               uri.Host.Equals("127.0.0.1", StringComparison.Ordinal)
                ? "localhost"
                : "internal";
            return $"{uri.Scheme}://{hostIndicator}:****";
        }
        return "internal:****";
    }

    private static string MaskPublicUrl(string url)
    {
        // Show full public URL since it's browser-visible anyway
        return url;
    }

    private static object CreateTransportPayload(TransportDiagnostics diagnostics) => new
    {
        transport = diagnostics.Transport,
        usingBackchannel = diagnostics.UsingBackchannel,
        backchannelPresent = diagnostics.BackchannelPresent,
        transportBaseUrl = diagnostics.TransportBaseUrl,
        targetUrlScheme = diagnostics.TargetUrlScheme,
        targetPath = diagnostics.TargetPath
    };

    private static string BuildTimeoutHint(TransportDiagnostics diagnostics)
    {
        if (diagnostics.UsingBackchannel)
        {
            return $"The request used the internal backchannel path `{diagnostics.TargetPath}`. Check that MockBusinessApp is listening and that AppHost passed the current BUSINESSAPP_BACKCHANNEL_URL. If Codespaces was resumed or refreshed, run `bash scripts/codespaces/refresh.sh`.";
        }

        return diagnostics.Transport == "public-tunnel"
            ? $"The request was targeting the public Codespaces URL for `{diagnostics.TargetPath}` instead of the internal backchannel. Check whether BUSINESSAPP_BACKCHANNEL_URL is present in AppHost, then verify the forwarded MockBusinessApp port is healthy."
            : $"The request was targeting the configured public URL for `{diagnostics.TargetPath}`. Check that MockBusinessApp is running and reachable from TestSite.";
    }

    private static string BuildTimeoutNextCheck(TransportDiagnostics diagnostics)
    {
        if (diagnostics.UsingBackchannel)
        {
            return $"Check MockBusinessApp health for `{diagnostics.TargetPath}` and confirm AppHost injected BUSINESSAPP_BACKCHANNEL_URL.";
        }

        return diagnostics.Transport == "public-tunnel"
            ? $"Check whether AppHost should be using BUSINESSAPP_BACKCHANNEL_URL instead of the public Codespaces tunnel for `{diagnostics.TargetPath}`."
            : $"Check that the configured public base URL can serve `{diagnostics.TargetPath}` from TestSite.";
    }

    private string ResolveBusinessAppTransportBaseUrl()
    {
        var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(backchannelUrl))
            return backchannelUrl;

        var baseUrl = configuration["PrismBusinessApp:ApiBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("PrismBusinessApp:ApiBaseUrl is not configured.");

        return baseUrl;
    }

    private string ResolveBusinessAppDisplayBaseUrl()
    {
        var baseUrl = configuration["PrismBusinessApp:ApiBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("PrismBusinessApp:ApiBaseUrl is not configured.");

        return baseUrl;
    }

    private string TransformToDisplayUrl(string transportUrl)
    {
        var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(backchannelUrl))
            return transportUrl;

        if (!transportUrl.StartsWith(backchannelUrl, StringComparison.OrdinalIgnoreCase))
            return transportUrl;

        var displayBaseUrl = ResolveBusinessAppDisplayBaseUrl();
        return displayBaseUrl + transportUrl.Substring(backchannelUrl.Length);
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

        var defaultBaseUrl = configuration["PrismBusinessApp:ApiBaseUrl"]?.TrimEnd('/');
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

    private sealed class DownstreamRequestCancelledException(bool timedOutByRequestWindow, Exception innerException)
        : Exception(innerException.Message, innerException)
    {
        public bool TimedOutByRequestWindow { get; } = timedOutByRequestWindow;
    }

    private readonly record struct TransportDiagnostics(
        string Transport,
        bool UsingBackchannel,
        bool BackchannelPresent,
        string TransportBaseUrl,
        string TargetUrlScheme,
        string TargetPath);

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

    private static bool IsCodespacesUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Host.EndsWith(".app.github.dev", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildInvalidResponseSummary(
        HttpResponseMessage response,
        string contentType,
        string targetUrl,
        string rawBody)
    {
        var errorMessage = $"Expected JSON but received {contentType} (HTTP {(int)response.StatusCode} {response.ReasonPhrase ?? response.StatusCode.ToString()}).";

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            errorMessage += " The downstream service returned an empty body.";
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            errorMessage += " This usually means Mock Business App rejected the bearer token before the request reached the controller.";
            if (IsLocalBackchannelUrl(targetUrl) && HasInvalidTokenChallenge(response))
            {
                errorMessage += " The displayed localhost URL is the internal TestSite → Mock Business App backchannel hop, not the browser URL. " +
                    "In Codespaces, the clearest next step is to run `bash scripts/codespaces/refresh.sh`; if it still fails, check Mock Business App `/debug/auth` for backchannel and JWKS state.";
            }
        }

        if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            if (IsCodespacesUrl(targetUrl))
            {
                errorMessage += " Received an HTML page instead of JSON. " +
                    "This usually means the request hit the GitHub Codespaces port-forwarding proxy " +
                    "instead of Mock Business App. Private or not-yet-ready forwarded ports can return " +
                    "an HTML tunnel/auth page for server-side requests.";
            }
            else if (rawBody.Contains("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage += " Received an HTML page instead of JSON. " +
                    "If running in Codespaces, the port may not be forwarded correctly yet. " +
                    "Wait a moment and try again.";
            }
        }

        return errorMessage;
    }

    private static string BuildInvalidResponseUiSummary(HttpResponseMessage response, string contentType, string targetUrl)
    {
        var status = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase ?? response.StatusCode.ToString()}";
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (IsLocalBackchannelUrl(targetUrl) && HasInvalidTokenChallenge(response))
            {
                return $"The downstream service replied with {status}. The displayed localhost URL is the internal TestSite → Mock Business App hop, not the browser URL. In Codespaces, run `bash scripts/codespaces/refresh.sh`, then check `/debug/auth` if it still fails.";
            }

            return $"The downstream service replied with {status}. This usually means Mock Business App rejected the bearer token before your request reached the controller.";
        }

        if (contentType.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            return $"The downstream service replied with {status} but did not identify the response type. See diagnostics below.";
        }

        return $"The downstream service replied with {status} and {contentType} instead of JSON. See diagnostics below.";
    }

    private static bool HasInvalidTokenChallenge(HttpResponseMessage response)
    {
        return response.Headers.WwwAuthenticate.Any(header =>
            string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(header.Parameter) &&
            header.Parameter.Contains("error=\"invalid_token\"", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLocalBackchannelUrl(string targetUrl)
    {
        return Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) &&
               (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("127.0.0.1", StringComparison.Ordinal));
    }

    private static string CreateDiagnosticBody(
        HttpResponseMessage response,
        string contentType,
        string rawBody,
        out bool wasTruncated)
    {
        wasTruncated = false;

        var responseBody = string.IsNullOrWhiteSpace(rawBody)
            ? "[No response body]"
            : rawBody.Trim();

        var lines = new List<string>
        {
            $"HTTP {(int)response.StatusCode} {response.ReasonPhrase ?? response.StatusCode.ToString()}",
            $"Content-Type: {contentType}"
        };

        foreach (var header in GetDiagnosticHeaders(response))
        {
            lines.Add(header);
        }

        lines.Add(string.Empty);
        lines.Add(responseBody);

        var diagnosticText = string.Join(Environment.NewLine, lines);
        if (diagnosticText.Length <= MaxDiagnosticBodyLength)
        {
            return diagnosticText;
        }

        wasTruncated = true;
        return diagnosticText[..MaxDiagnosticBodyLength] + DiagnosticBodyTruncationNotice;
    }

    private static IReadOnlyList<string> GetDiagnosticHeaders(HttpResponseMessage response)
    {
        return response.Headers
            .Concat(response.Content.Headers)
            .Select(header => $"{header.Key}: {string.Join(", ", header.Value)}")
            .ToList();
    }

    private static string FormatHeaders(HttpResponseMessage response)
    {
        var headers = GetDiagnosticHeaders(response);
        return headers.Count == 0
            ? "[none]"
            : string.Join("; ", headers);
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
        var touchpointPage = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedTouchpointPage(roots, TestSiteSeedContract.BlueprintKey),
            TestSiteSeedContract.TouchpointPageAlias,
            TestSiteSeedContract.TouchpointPageName,
            TestSiteSeedContract.ServiceRequestPageUrl);
        var serviceRequestHub = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedByAlias(roots, TestSiteSeedContract.ServiceRequestHubAlias),
            TestSiteSeedContract.ServiceRequestHubAlias,
            TestSiteSeedContract.ServiceRequestHubName,
            TestSiteSeedContract.ServiceRequestHubUrl);
        var planningTouchpointPage = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedTouchpointPage(roots, TestSiteSeedContract.PlanningBlueprintKey),
            TestSiteSeedContract.TouchpointPageAlias,
            TestSiteSeedContract.PlanningTouchpointPageName,
            TestSiteSeedContract.PlanningTouchpointPageUrl);
        var paymentDemoPage = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedTouchpointPage(roots, TestSiteSeedContract.PaymentDemoBlueprintKey),
            TestSiteSeedContract.TouchpointPageAlias,
            TestSiteSeedContract.PaymentDemoPageName,
            TestSiteSeedContract.PaymentDemoPageUrl);
        var informationRequestPage = BuildSeededRoute(
            TestSiteSeedContract.FindPublishedTouchpointPage(roots, TestSiteSeedContract.InformationRequestBlueprintKey),
            TestSiteSeedContract.TouchpointPageAlias,
            TestSiteSeedContract.InformationRequestPageName,
            TestSiteSeedContract.InformationRequestPageUrl);
        var settings = TestSiteSeedContract.FindPublishedByAlias(roots, TestSiteSeedContract.SettingsAlias);
        var mobileNav = BuildMobileNavStatus(settings);
        var challengePath = $"/auth/login?ReturnUrl={Uri.EscapeDataString(TestSiteSeedContract.ServiceRequestHubUrl)}";
        // routeContractReady waits for every authored URL the Playwright suite navigates to, so
        // the first request to any of them lands on a fully-warm Umbraco route + Razor view —
        // not a cold-start that returns 404 / Home / a half-rendered page (the symptom that
        // showed up after the v2.0 polymorphic component schema rollout made first-render
        // view compilation slower than the test's 5s default visibility timeout).
        var routeContractReady =
            home.MatchesExpected &&
            dashboard.MatchesExpected &&
            touchpointPage.MatchesExpected &&
            serviceRequestHub.MatchesExpected &&
            planningTouchpointPage.MatchesExpected &&
            paymentDemoPage.MatchesExpected &&
            informationRequestPage.MatchesExpected &&
            mobileNav.Ready;

        return new SeedContractStatus(
            Ready: routeContractReady,
            RouteContractReady: routeContractReady,
            Auth: new SeedAuthStatus("/auth/login", "/auth/logout", challengePath),
            Home: home,
            Dashboard: dashboard,
            TouchpointPage: touchpointPage,
            ServiceRequestHub: serviceRequestHub,
            PlanningTouchpointPage: planningTouchpointPage,
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
        var hasServiceRequestHub = navUrls.Contains(NormalizePath(TestSiteSeedContract.ServiceRequestHubUrl));
        var ready =
            settings != null &&
            mobileNavLinks != null &&
            mobileNavLinks.Any() &&
            hasHome &&
            hasDashboard &&
            hasServiceRequestHub;

        return new MobileNavStatus(settings != null, mobileNavLinks?.Count ?? 0, hasHome, hasDashboard, hasServiceRequestHub, ready);
    }

    private sealed record SeedContractStatus(
        bool Ready,
        bool RouteContractReady,
        SeedAuthStatus Auth,
        SeededRouteStatus Home,
        SeededRouteStatus Dashboard,
        SeededRouteStatus TouchpointPage,
        SeededRouteStatus ServiceRequestHub,
        SeededRouteStatus PlanningTouchpointPage,
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
        bool HasServiceRequestHub,
        bool Ready);

    private static string NormalizePath(string? path)
        => TestSiteSeedContract.NormalizeUrl(path);

    private static bool IsJsonContentType(string contentType)
    {
        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("text/json", StringComparison.OrdinalIgnoreCase);
    }
}
