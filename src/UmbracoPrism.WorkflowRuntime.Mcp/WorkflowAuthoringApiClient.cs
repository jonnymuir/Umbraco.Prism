using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace UmbracoPrism.WorkflowRuntime.Mcp;

/// <summary>
/// Thin wrapper over the target app's <c>MapPrismWorkflowAuthoringApi()</c> endpoints. Deals in
/// raw JSON strings only — no shared model types, so this server stays usable against any
/// conformant host, not just Umbraco Prism's own reference app.
/// </summary>
public sealed class WorkflowAuthoringApiClient(HttpClient http, string apiPrefix)
{
    public async Task<string> ListWorkflowsAsync(CancellationToken ct)
    {
        var response = await http.GetAsync($"{apiPrefix}/workflows", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string?> ReadWorkflowAsync(string definitionKey, CancellationToken ct)
    {
        var response = await http.GetAsync($"{apiPrefix}/workflows/{Uri.EscapeDataString(definitionKey)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> ValidateWorkflowAsync(string workflowJson, CancellationToken ct)
    {
        using var content = JsonContent(workflowJson);
        var response = await http.PostAsync($"{apiPrefix}/workflows/validate", content, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> SaveWorkflowAsync(string workflowJson, CancellationToken ct)
    {
        var definitionKey = JsonNode.Parse(workflowJson)?["definitionKey"]?.GetValue<string>()
            ?? throw new InvalidOperationException("workflowJson has no definitionKey.");

        using var content = JsonContent(workflowJson);
        var response = await http.PutAsync($"{apiPrefix}/workflows/{Uri.EscapeDataString(definitionKey)}", content, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> SimulateWorkflowAsync(string workflowJson, string stepsJson, CancellationToken ct)
    {
        var requestBody = new JsonObject
        {
            ["workflow"] = JsonNode.Parse(workflowJson),
            ["steps"] = JsonNode.Parse(stepsJson)
        };

        var response = await http.PostAsJsonAsync($"{apiPrefix}/workflows/simulate", requestBody, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");
}
