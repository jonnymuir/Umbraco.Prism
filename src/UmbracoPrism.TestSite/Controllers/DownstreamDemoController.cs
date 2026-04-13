using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
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
    IWebHostEnvironment environment) : ControllerBase
{
    private static readonly JsonSerializerOptions PrettyPrint = new() { WriteIndented = true };

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? url = null)
    {
        // Phase 1 Security: Only allow downstream demo in Development environment
        // or when explicitly enabled via Prism:EnableDownstreamDemo config
        var isDemoEnabled = environment.IsDevelopment() 
            || configuration.GetValue<bool>("Prism:EnableDownstreamDemo", false);
        
        if (!isDemoEnabled)
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
        var client = httpClientFactory.CreateClient("prism-downstream-demo");
        client.DefaultRequestHeaders.Authorization = authHeader;
        client.Timeout = TimeSpan.FromSeconds(10);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.GetAsync(targetUrl);
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

    private string BuildTargetUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
            return url;

        var baseUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("PrismBusinessApp:WorkflowApiBaseUrl is not configured.");

        return $"{baseUrl}/api/backoffice/me";
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
}
