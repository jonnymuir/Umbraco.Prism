using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.TestSite.Controllers;

/// <summary>
/// API endpoint used by the dashboard's downstream demo section.
/// Calls a configured URL using the current member's Prism Bearer token
/// and returns the raw response so the dashboard can display it inline.
/// </summary>
[ApiController]
[Route("api/prism/downstream-demo")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class DownstreamDemoController(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IPrismContext prismContext) : ControllerBase
{
    private static readonly JsonSerializerOptions PrettyPrint = new() { WriteIndented = true };

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? url = null)
    {
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
}
