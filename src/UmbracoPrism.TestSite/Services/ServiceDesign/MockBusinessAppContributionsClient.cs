using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Models.ServiceDesign.SupportSystems;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// Registers the <see cref="SupportSystemDescriptor"/> for Mock Business App's own contributions
/// validation endpoints (<c>UmbracoPrism.MockBusinessApp/Services/SupportSystem/ContributionsEndpoints.cs</c>)
/// — a real, separate downstream system that validates every row of an NJF contributions file
/// automatically and hands back the same file annotated with a matched member id and any
/// error/warning text, the exact shape <c>bulk-dataset-ingest</c> expects. Mirrors the core
/// Wayfinder repo's own <c>SafetyNetUnderwriting</c> reference implementation of the identical
/// pattern (see docs/guides/bulk-data-review.md there) — automatic rules, not a human decision,
/// so there's no staff queue involved for this capability and only Poll makes sense as a
/// completion mode.
/// </summary>
public static class MockBusinessAppContributions
{
    public const string SupportSystemKey = "mock-business-app-contributions";
    public const string ValidateContributionsFileCapability = "validate-contributions-file";
    public const string ProcessedOutcome = "processed";
    public const string ContributionsResponseFileOutputKey = "contributionsResponseFile";

    public static void Register() =>
        SupportSystemRegistry.Register(new SupportSystemDescriptor
        {
            Key = SupportSystemKey,
            DisplayName = "Mock Business App",
            Description = "A real, separate downstream system that validates an NJF contributions file submission.",
            Capabilities =
            [
                new SupportSystemCapabilityDescriptor
                {
                    Key = ValidateContributionsFileCapability,
                    DisplayName = "Validate a contributions file",
                    Description = "Uploads a CSV of member contributions; Mock Business App returns the same file " +
                                  "annotated with a matched member ID and per-row error/warning status — see " +
                                  "docs/guides/bulk-data-review.md in the core Wayfinder repo.",
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
                            Key = ContributionsResponseFileOutputKey, Title = "Annotated response file",
                            Description = "Mock Business App's own response — the same CSV with a matched member " +
                                          "ID and per-row error/warning columns appended.",
                            ValueKind = ComponentPropertyValueKind.String,
                        },
                    ],
                    SupportedCompletionModes = [SupportSystemCompletionMode.Poll],
                    Outcomes = [new() { Key = ProcessedOutcome, DisplayName = "Processed" }],
                },
            ],
        });
}

/// <summary>
/// Talks to the real, separately-running Mock Business App over HTTP — the host-side half of the
/// registration in <see cref="MockBusinessAppContributions"/>. Reads file bytes itself via
/// <see cref="IServiceRequestFileStorage"/> when a capability input resolves to a
/// <see cref="ServiceRequestFileReference"/>, exactly the way any other host code reads an
/// uploaded file — the engine that invoked this client never touched the bytes.
/// </summary>
public sealed class MockBusinessAppContributionsClient(
    IHttpClientFactory httpClientFactory,
    IServiceRequestFileStorage fileStorage) : ISupportSystemClient
{
    public const string HttpClientName = "mock-business-app-contributions";

    // CheckStatusAsync only ever gets a capabilityKey + receipt, no instanceId — but saving the
    // response file via IServiceRequestFileStorage needs one (SaveAsync partitions by instance).
    // Captured here at InvokeAsync time instead, the same "no server-side session, just correlate
    // by the one token we're given" shape SupportSystemInvocationContext.InvocationId already
    // uses elsewhere. This client is a singleton shared across concurrent requests, so this must
    // be concurrency-safe.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _instanceIdByExternalReference = new();

    public string SupportSystemKey => MockBusinessAppContributions.SupportSystemKey;

    public async Task<SupportSystemInvocationReceipt> InvokeAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, SupportSystemInputValue> inputs,
        SupportSystemInvocationContext context,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        await AddFilePartAsync(form, inputs, ct);

        var client = httpClientFactory.CreateClient(HttpClientName);
        var response = await client.PostAsync("/contributions/submissions", form, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct)
                   ?? throw new InvalidOperationException("Mock Business App returned an empty submission response.");
        var submissionId = body["submissionId"]?.GetValue<string>()
                            ?? throw new InvalidOperationException("Mock Business App response had no submissionId.");

        _instanceIdByExternalReference[submissionId] = context.InstanceId;
        return new SupportSystemInvocationReceipt { ExternalReference = submissionId };
    }

    public async Task<SupportSystemOutcome?> CheckStatusAsync(
        string capabilityKey,
        SupportSystemInvocationReceipt receipt,
        CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var statusResponse = await client.GetAsync($"/contributions/submissions/{receipt.ExternalReference}", ct);
        statusResponse.EnsureSuccessStatusCode();

        var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonObject>(ct);
        if (statusBody?["status"]?.GetValue<string>() != "processed")
        {
            return null;
        }

        var fileResponse = await client.GetAsync($"/contributions/submissions/{receipt.ExternalReference}/file", ct);
        fileResponse.EnsureSuccessStatusCode();
        var csvBytes = await fileResponse.Content.ReadAsByteArrayAsync(ct);

        if (!_instanceIdByExternalReference.TryGetValue(receipt.ExternalReference, out var instanceId))
        {
            throw new InvalidOperationException(
                $"No instance id captured for submission '{receipt.ExternalReference}' — InvokeAsync must run before CheckStatusAsync.");
        }

        await using var contentStream = new MemoryStream(csvBytes);
        var storageKey = await fileStorage.SaveAsync(
            instanceId, MockBusinessAppContributions.ContributionsResponseFileOutputKey, contentStream, "contributions-response.csv", ct);

        var fileReference = new ServiceRequestFileReference
        {
            StorageKey = storageKey,
            OriginalFileName = "contributions-response.csv",
            ContentType = "text/csv",
            SizeBytes = csvBytes.LongLength,
        };

        return new SupportSystemOutcome
        {
            OutcomeKey = MockBusinessAppContributions.ProcessedOutcome,
            ResultPayload = new JsonObject
            {
                [MockBusinessAppContributions.ContributionsResponseFileOutputKey] = System.Text.Json.JsonSerializer.SerializeToNode(fileReference)
            }
        };
    }

    private async Task AddFilePartAsync(
        MultipartFormDataContent form, IReadOnlyDictionary<string, SupportSystemInputValue> inputs, CancellationToken ct)
    {
        if (inputs.GetValueOrDefault("file")?.FileReference is not { } fileReference)
        {
            return;
        }

        var fileStream = await fileStorage.OpenReadAsync(fileReference.StorageKey, ct);
        if (fileStream is null)
        {
            return;
        }

        await using (fileStream)
        {
            using var memory = new MemoryStream();
            await fileStream.CopyToAsync(memory, ct);
            var fileContent = new ByteArrayContent(memory.ToArray());
            if (!string.IsNullOrWhiteSpace(fileReference.ContentType))
            {
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(fileReference.ContentType);
            }

            form.Add(fileContent, "file", fileReference.OriginalFileName);
        }
    }
}
