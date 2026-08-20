using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Models.ServiceDesign.SupportSystems;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// Registers the <see cref="SupportSystemDescriptor"/> for Mock Business App's own generic
/// support-system surface (<c>UmbracoPrism.MockBusinessApp/Services/SupportSystem/SupportSystemEndpoints.cs</c>)
/// — a real, separate downstream decisioning backend, the same pattern
/// <c>SafetyNetUnderwriting</c> demonstrates in the core Wayfinder repo (see
/// docs/guides/support-systems.md there). Deliberately whole-file, not row-level: Mock Business
/// App's <c>/submissions</c> endpoint is plain JSON, not a file-in/file-out exchange, so this
/// capability submits just the file's own metadata (name, size) for a human reviewer at Mock
/// Business App to approve/reject via its own <c>/queue</c> page — see
/// <see cref="MockBusinessAppContributionsClient"/> for the HTTP calls.
/// </summary>
public static class MockBusinessAppContributions
{
    public const string SupportSystemKey = "mock-business-app-contributions";
    public const string ValidateContributionsFileCapability = "validate-contributions-file";
    public const string ApprovedOutcome = "approved";
    public const string RejectedOutcome = "rejected";

    public static void Register() =>
        SupportSystemRegistry.Register(new SupportSystemDescriptor
        {
            Key = SupportSystemKey,
            DisplayName = "Mock Business App",
            Description = "A real, separate downstream system that reviews an NJF contributions file submission.",
            Capabilities =
            [
                new SupportSystemCapabilityDescriptor
                {
                    Key = ValidateContributionsFileCapability,
                    DisplayName = "Validate a contributions file",
                    Description = "Submits the uploaded file's own metadata to Mock Business App's staff queue for a human approve/reject decision.",
                    Inputs =
                    [
                        new()
                        {
                            Key = "file", Title = "Contributions file",
                            Description = "The file-upload field carrying the NJF's contributions CSV.",
                            ValueKind = ComponentPropertyValueKind.String, Format = "field-ref", Required = true,
                        },
                    ],
                    Outputs =
                    [
                        new()
                        {
                            Key = "contributionsDecision", Title = "Decision",
                            ValueKind = ComponentPropertyValueKind.String,
                        },
                        new()
                        {
                            Key = "contributionsDecisionNotes", Title = "Decision notes",
                            ValueKind = ComponentPropertyValueKind.String,
                        },
                    ],
                    SupportedCompletionModes = [SupportSystemCompletionMode.Poll],
                    Outcomes =
                    [
                        new() { Key = ApprovedOutcome, DisplayName = "Approved" },
                        new() { Key = RejectedOutcome, DisplayName = "Rejected" },
                    ],
                },
            ],
        });
}

/// <summary>
/// Talks to Mock Business App's generic support-system endpoints over HTTP — the host-side half
/// of the registration in <see cref="MockBusinessAppContributions"/>. Reads only the uploaded
/// file's own metadata (<see cref="Wayfinder.Models.ServiceDesign.ServiceRequestFileReference.OriginalFileName"/>/
/// <c>SizeBytes</c>) via the capability input's already-resolved
/// <see cref="SupportSystemInputValue.FileReference"/> — never opens the file itself, since Mock
/// Business App's own <c>/submissions</c> endpoint only accepts plain JSON fields, not a file body.
/// </summary>
public sealed class MockBusinessAppContributionsClient(IHttpClientFactory httpClientFactory) : ISupportSystemClient
{
    public const string HttpClientName = "mock-business-app-contributions";

    public string SupportSystemKey => MockBusinessAppContributions.SupportSystemKey;

    public async Task<SupportSystemInvocationReceipt> InvokeAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, SupportSystemInputValue> inputs,
        SupportSystemInvocationContext context,
        CancellationToken ct = default)
    {
        var fileReference = inputs.GetValueOrDefault("file")?.FileReference;

        var client = httpClientFactory.CreateClient(HttpClientName);
        var response = await client.PostAsJsonAsync("/submissions", new
        {
            fileName = fileReference?.OriginalFileName ?? "(unknown file)",
            sizeBytes = fileReference?.SizeBytes ?? 0
        }, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct)
                   ?? throw new InvalidOperationException("Mock Business App returned an empty submission response.");

        return new SupportSystemInvocationReceipt
        {
            ExternalReference = body["submissionId"]?.GetValue<string>()
                                 ?? throw new InvalidOperationException("Mock Business App response had no submissionId.")
        };
    }

    public async Task<SupportSystemOutcome?> CheckStatusAsync(
        string capabilityKey,
        SupportSystemInvocationReceipt receipt,
        CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var response = await client.GetAsync($"/submissions/{receipt.ExternalReference}", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
        var status = body?["status"]?.GetValue<string>();
        if (status != "decided")
        {
            return null;
        }

        var outcomeKey = body?["outcomeKey"]?.GetValue<string>() ?? MockBusinessAppContributions.RejectedOutcome;
        return new SupportSystemOutcome
        {
            OutcomeKey = outcomeKey,
            ResultPayload = new JsonObject
            {
                ["contributionsDecision"] = outcomeKey,
                ["contributionsDecisionNotes"] = outcomeKey == MockBusinessAppContributions.ApprovedOutcome
                    ? "Mock Business App approved this file."
                    : "Mock Business App rejected this file — see the staff queue for details."
            }
        };
    }
}
