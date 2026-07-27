using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// HTTP client that calls the external Business Application's workflow API.
/// Configured via <c>PrismBusinessApp:ApiBaseUrl</c>.
/// </summary>
/// <remarks>
/// This is the primary integration point between Umbraco and the Business App.
/// It handles serialization, error handling, and logging for all workflow API calls.
///
/// The authenticated member's Entra Bearer token is forwarded on every request so the
/// Business App can independently verify the caller's identity rather than trusting the
/// TenantId/UserId values in the request body alone.
/// </remarks>
public class BusinessAppProcessManagerClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IPrismContext prismContext,
    ILogger<BusinessAppProcessManagerClient> logger) : IBusinessAppProcessManagerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc/>
    public async Task<ServiceRequestResponseEnvelope> GetCurrentAsync(
        string blueprintKey,
        string? instanceId = null,
        string? action = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/api/service-request/{blueprintKey}/current";

        logger.LogDebug("BusinessAppProcessManagerClient: GET current {BlueprintKey} instanceId={InstanceId} action={Action}", 
            blueprintKey, instanceId ?? "(none)", action ?? "(none)");

        try
        {
            var response = await SendWithTokenRefreshRetryAsync(
                client =>
                {
                    // Send JSON body only if instanceId or action are provided
                    if (!string.IsNullOrEmpty(instanceId) || !string.IsNullOrEmpty(action))
                    {
                        var payload = new { InstanceId = instanceId, Action = action };
                        return client.PostAsJsonAsync(url, payload, cancellationToken);
                    }
                    return client.PostAsync(url, null, cancellationToken);
                },
                allowRefreshRetry: true,
                cancellationToken);
            return await ReadEnvelopeAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling Business App workflow current for '{BlueprintKey}'", blueprintKey);
            return ErrorEnvelope($"Business App is unavailable: {ex.Message}", "BUSINESS_APP_UNAVAILABLE");
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceRequestResponseEnvelope> AdvanceAsync(
        string blueprintKey,
        string instanceId,
        string action,
        int stateVersion,
        Dictionary<string, object?>? fieldValues = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/api/service-request/{blueprintKey}/advance";
        var payload = new
        {
            InstanceId = instanceId,
            Action = action,
            StateVersion = stateVersion,
            FieldValues = fieldValues
        };

        logger.LogDebug("BusinessAppProcessManagerClient: ADVANCE {BlueprintKey} instance={Instance} action={Action}", blueprintKey, instanceId, action);

        try
        {
            var response = await SendWithTokenRefreshRetryAsync(
                client => client.PostAsJsonAsync(url, payload, cancellationToken),
                allowRefreshRetry: true,
                cancellationToken);
            return await ReadEnvelopeAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling Business App workflow advance for instance '{InstanceId}'", instanceId);
            return ErrorEnvelope($"Business App is unavailable: {ex.Message}", "BUSINESS_APP_UNAVAILABLE");
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceRequestListEnvelope> GetInstancesAsync(
        bool allowRefreshRetry = true,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/api/service-request/instances";

        logger.LogDebug("BusinessAppProcessManagerClient: GET instances");

        try
        {
            var response = await SendWithTokenRefreshRetryAsync(
                client => client.GetAsync(url, cancellationToken),
                allowRefreshRetry,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Business App returned {StatusCode} for instances list", (int)response.StatusCode);
                return new ServiceRequestListEnvelope();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<ServiceRequestListEnvelope>(body, JsonOptions)
                   ?? new ServiceRequestListEnvelope();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling Business App workflow instances");
            return new ServiceRequestListEnvelope();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialise Business App workflow instances response");
            return new ServiceRequestListEnvelope();
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Gets the workflow API base URL, preferring the internal backchannel in Codespaces.</summary>
    /// <returns>The base URL, with trailing slashes removed.</returns>
    /// <exception cref="InvalidOperationException">Thrown if neither backchannel nor config URL is set.</exception>
    /// <remarks>
    /// In Codespaces, <c>BUSINESSAPP_BACKCHANNEL_URL</c> is injected by AppHost so server-to-server
    /// calls bypass the GitHub forwarded-port proxy (which blocks unauthenticated requests with 401).
    /// <c>PrismBusinessApp:ApiBaseUrl</c> remains the browser-facing public URL.
    /// </remarks>
    private string BaseUrl
    {
        get
        {
            var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(backchannelUrl))
                return backchannelUrl;

            var url = configuration["PrismBusinessApp:ApiBaseUrl"];
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException(
                    "PrismBusinessApp:ApiBaseUrl is not configured. " +
                    "Add it to appsettings.Development.json pointing at the running Mock Business App.");
            return url.TrimEnd('/');
        }
    }

    /// <summary>Creates an HTTP client for calling the Business App, with the member's Bearer token attached.</summary>
    private async Task<HttpClient> CreateClientAsync(bool forceRefresh = false)
    {
        var client = httpClientFactory.CreateClient("PrismBusinessApp");
        var authHeader = await prismContext.GetAuthorizationHeaderAsync(forceRefresh);
        if (authHeader != null)
        {
            client.DefaultRequestHeaders.Authorization = authHeader;
        }
        else
        {
            logger.LogWarning(
                "BusinessAppProcessManagerClient: GetAuthorizationHeaderAsync returned null (forceRefresh={ForceRefresh}). " +
                "No Authorization header will be sent — Business App will reject with 401. " +
                "Check that PrismTenantMiddleware has resolved CurrentTenant before this call.",
                forceRefresh);
        }
        return client;
    }

    private async Task<HttpResponseMessage> SendWithTokenRefreshRetryAsync(
        Func<HttpClient, Task<HttpResponseMessage>> send,
        bool allowRefreshRetry,
        CancellationToken cancellationToken)
    {
        var client = await CreateClientAsync();
        var response = await send(client);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !allowRefreshRetry)
        {
            return response;
        }

        logger.LogInformation("Business App returned 401; forcing a refresh-token exchange before retrying the request.");
        response.Dispose();

        cancellationToken.ThrowIfCancellationRequested();

        var refreshedClient = await CreateClientAsync(forceRefresh: true);
        return await send(refreshedClient);
    }

    /// <summary>
    /// Reads a workflow response envelope from an HTTP response, handling errors and deserialisation.
    /// </summary>
    /// <param name="response">The HTTP response from the Business App.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A parsed ServiceRequestResponseEnvelope; on error, returns an error envelope.</returns>
    /// <remarks>
    /// This method tolerates HTTP errors (5xx, timeouts) and JSON deserialisation failures,
    /// returning an error envelope instead of throwing. This allows the UI to display user-friendly messages.
    /// </remarks>
    private async Task<ServiceRequestResponseEnvelope> ReadEnvelopeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
        {
            logger.LogWarning("Business App returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            return ErrorEnvelope($"Business App error (HTTP {(int)response.StatusCode})", "BUSINESS_APP_ERROR");
        }

        try
        {
            return JsonSerializer.Deserialize<ServiceRequestResponseEnvelope>(body, JsonOptions)
                   ?? ErrorEnvelope("Business App returned an empty response.", "EMPTY_RESPONSE");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialise Business App workflow response: {Body}", body);
            return ErrorEnvelope("Business App returned an unrecognised response format.", "INVALID_RESPONSE");
        }
    }

    /// <summary>Creates a standard error envelope with a message and code.</summary>
    /// <param name="message">The error message to display to the user.</param>
    /// <param name="code">The error code for programmatic handling (e.g. "BUSINESS_APP_UNAVAILABLE").</param>
    /// <returns>A ServiceRequestResponseEnvelope in error state.</returns>
    private static ServiceRequestResponseEnvelope ErrorEnvelope(string message, string code) =>
        new()
        {
            InstanceId = string.Empty,
            ResponseState = "error",
            StateVersion = 0,
            CorrelationId = Guid.NewGuid().ToString(),
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Problems = [new ServiceRequestProblem { FieldKey = string.Empty, Message = message, Code = code }]
        };
}
