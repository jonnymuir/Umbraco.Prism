using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// HTTP client that calls the external Business Application's workflow API.
/// Configured via <c>PrismBusinessApp:WorkflowApiBaseUrl</c>.
/// </summary>
/// <remarks>
/// This is the primary integration point between Umbraco and the Business App.
/// It handles serialization, error handling, and logging for all workflow API calls.
/// </remarks>
public class BusinessAppWorkflowClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<BusinessAppWorkflowClient> logger) : IBusinessAppWorkflowClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc/>
    public async Task<WorkflowResponseEnvelope> GetCurrentAsync(
        string workflowKey, string tenantId, string userId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/api/workflow/{workflowKey}/current";
        var payload = new { TenantId = tenantId, UserId = userId };

        logger.LogDebug("BusinessAppWorkflowClient: GET current {WorkflowKey} for tenant={Tenant}", workflowKey, tenantId);

        try
        {
            var response = await CreateClient().PostAsJsonAsync(url, payload, cancellationToken);
            return await ReadEnvelopeAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling Business App workflow current for '{WorkflowKey}'", workflowKey);
            return ErrorEnvelope($"Business App is unavailable: {ex.Message}", "BUSINESS_APP_UNAVAILABLE");
        }
    }

    /// <inheritdoc/>
    public async Task<WorkflowResponseEnvelope> AdvanceAsync(
        string workflowKey, string tenantId, string userId,
        string instanceId, string action, int stateVersion,
        Dictionary<string, object?>? fieldValues = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/api/workflow/{workflowKey}/advance";
        var payload = new
        {
            InstanceId = instanceId,
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            StateVersion = stateVersion,
            FieldValues = fieldValues
        };

        logger.LogDebug("BusinessAppWorkflowClient: ADVANCE {WorkflowKey} instance={Instance} action={Action}", workflowKey, instanceId, action);

        try
        {
            var response = await CreateClient().PostAsJsonAsync(url, payload, cancellationToken);
            return await ReadEnvelopeAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling Business App workflow advance for instance '{InstanceId}'", instanceId);
            return ErrorEnvelope($"Business App is unavailable: {ex.Message}", "BUSINESS_APP_UNAVAILABLE");
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Gets the workflow API base URL from configuration.</summary>
    /// <returns>The base URL, with trailing slashes removed.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the URL is not configured.</exception>
    private string BaseUrl
    {
        get
        {
            var url = configuration["PrismBusinessApp:WorkflowApiBaseUrl"];
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException(
                    "PrismBusinessApp:WorkflowApiBaseUrl is not configured. " +
                    "Add it to appsettings.Development.json pointing at the running Mock Business App.");
            return url.TrimEnd('/');
        }
    }

    /// <summary>Creates an HTTP client for calling the Business App.</summary>
    private HttpClient CreateClient() => httpClientFactory.CreateClient("PrismBusinessApp");

    /// <summary>
    /// Reads a workflow response envelope from an HTTP response, handling errors and deserialisation.
    /// </summary>
    /// <param name="response">The HTTP response from the Business App.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A parsed WorkflowResponseEnvelope; on error, returns an error envelope.</returns>
    /// <remarks>
    /// This method tolerates HTTP errors (5xx, timeouts) and JSON deserialisation failures,
    /// returning an error envelope instead of throwing. This allows the UI to display user-friendly messages.
    /// </remarks>
    private async Task<WorkflowResponseEnvelope> ReadEnvelopeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
        {
            logger.LogWarning("Business App returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            return ErrorEnvelope($"Business App error (HTTP {(int)response.StatusCode})", "BUSINESS_APP_ERROR");
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowResponseEnvelope>(body, JsonOptions)
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
    /// <returns>A WorkflowResponseEnvelope in error state.</returns>
    private static WorkflowResponseEnvelope ErrorEnvelope(string message, string code) =>
        new()
        {
            InstanceId = string.Empty,
            ResponseState = "error",
            StateVersion = 0,
            CorrelationId = Guid.NewGuid().ToString(),
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Problems = [new WorkflowProblem { FieldKey = string.Empty, Message = message, Code = code }]
        };
}
