using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Models.ServiceDesign.SupportSystems;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// Registers the <see cref="SupportSystemDescriptor"/> for Mock Business App's member profile
/// endpoints (<c>UmbracoPrism.MockBusinessApp/Services/Profile/ProfileEndpoints.cs</c>), the
/// reference for a support system that acts as the signed-in member rather than as the host. See
/// docs/walkthroughs/authenticated-business-app-call.md.
/// </summary>
public static class MockBusinessAppProfile
{
    public const string SupportSystemKey = "mock-business-app-profile";
    public const string LoadProfileCapability = "load-profile";
    public const string SaveProfileCapability = "save-profile";

    public const string RegisteredOutcome = "registered";
    public const string NotRegisteredOutcome = "not-registered";
    public const string UpdatedOutcome = "updated";
    public const string RejectedOutcome = "rejected";

    public static void Register() =>
        SupportSystemRegistry.Register(new SupportSystemDescriptor
        {
            Key = SupportSystemKey,
            DisplayName = "Mock Business App member profile",
            Description = "The business system's record of a member's contact details, read and written with the signed-in member's own bearer token.",
            Capabilities =
            [
                new SupportSystemCapabilityDescriptor
                {
                    Key = LoadProfileCapability,
                    DisplayName = "Load the signed-in member's profile",
                    Description = "Reads the member's record for the tenant their token belongs to. Needs no inputs: who is asking, and for which tenant, comes from the bearer token alone.",
                    Inputs = [],
                    Outputs =
                    [
                        Output("profileName", "Member name"),
                        Output("profileEmail", "Member email"),
                        Output("profileTenant", "Tenant name"),
                        Output("profileRole", "Member role"),
                        Output("profilePhone", "Telephone number"),
                        Output("profileContactPreference", "Contact preference"),
                    ],
                    SupportedCompletionModes = [SupportSystemCompletionMode.Poll],
                    Outcomes =
                    [
                        new() { Key = RegisteredOutcome, DisplayName = "Registered member" },
                        new() { Key = NotRegisteredOutcome, DisplayName = "Not a registered member" },
                    ],
                },
                new SupportSystemCapabilityDescriptor
                {
                    Key = SaveProfileCapability,
                    DisplayName = "Save the signed-in member's profile",
                    Description = "Writes the member's contact details. The business system validates them and answers updated or rejected.",
                    Inputs =
                    [
                        Input("phone", "Telephone number", required: false),
                        Input("contactPreference", "Contact preference", required: true),
                    ],
                    Outputs =
                    [
                        Output("profileUpdateReference", "Update reference"),
                        Output("profileUpdateNote", "Why the update was rejected"),
                    ],
                    SupportedCompletionModes = [SupportSystemCompletionMode.Poll],
                    Outcomes =
                    [
                        new() { Key = UpdatedOutcome, DisplayName = "Updated" },
                        new() { Key = RejectedOutcome, DisplayName = "Rejected" },
                    ],
                },
            ],
        });

    private static ComponentPropertyDescriptor Output(string key, string title) =>
        new() { Key = key, Title = title, ValueKind = ComponentPropertyValueKind.String };

    private static ComponentPropertyDescriptor Input(string key, string title, bool required) =>
        new() { Key = key, Title = title, ValueKind = ComponentPropertyValueKind.String, Format = "field-ref", Required = required };
}

/// <summary>
/// Calls Mock Business App as the signed-in member. Both capabilities run in the member's own
/// browser request (the stage is entered by their Advance), so <see cref="MemberBearerProvider"/>
/// can release their access token after the tenant-binding check every downstream call goes
/// through. The token is attached to the outgoing request only: it is never put in the invocation
/// envelope, a field value, or a log line.
/// <para/>
/// The engine's contract is that <c>InvokeAsync</c> starts a call and a poll resolves it, so the
/// result is held in memory against the invocation id until the next poll collects it. That is
/// enough for a demo; a deployment with several instances, or one that must survive a restart
/// mid-call, would keep the result in a shared store instead.
/// </summary>
public sealed class MockBusinessAppProfileClient(
    IHttpClientFactory httpClientFactory,
    MemberBearerProvider memberBearer) : ISupportSystemClient
{
    public const string HttpClientName = "mock-business-app-profile";
    private const string ProfilePath = "/api/backoffice/profile";

    private readonly ConcurrentDictionary<string, SupportSystemOutcome> _resolved = new();

    public string SupportSystemKey => MockBusinessAppProfile.SupportSystemKey;

    public async Task<SupportSystemInvocationReceipt> InvokeAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, SupportSystemInputValue> inputs,
        SupportSystemInvocationContext context,
        CancellationToken ct = default)
    {
        using var request = capabilityKey switch
        {
            MockBusinessAppProfile.LoadProfileCapability => new HttpRequestMessage(HttpMethod.Get, ProfilePath),
            MockBusinessAppProfile.SaveProfileCapability => new HttpRequestMessage(HttpMethod.Put, ProfilePath)
            {
                Content = JsonContent.Create(new
                {
                    phone = InputText(inputs, "phone"),
                    contactPreference = InputText(inputs, "contactPreference"),
                }),
            },
            _ => throw new InvalidOperationException($"Unknown capability '{capabilityKey}'."),
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        request.Headers.Authorization = await memberBearer.GetForAsync(client);
        using var response = await client.SendAsync(request, ct);

        _resolved[context.InvocationId] = capabilityKey == MockBusinessAppProfile.LoadProfileCapability
            ? await ToLoadOutcomeAsync(response, ct)
            : await ToSaveOutcomeAsync(response, ct);

        return new SupportSystemInvocationReceipt { ExternalReference = context.InvocationId };
    }

    public Task<SupportSystemOutcome?> CheckStatusAsync(
        string capabilityKey,
        SupportSystemInvocationReceipt receipt,
        CancellationToken ct = default) =>
        Task.FromResult(_resolved.TryRemove(receipt.ExternalReference, out var outcome) ? outcome : null);

    private static async Task<SupportSystemOutcome> ToLoadOutcomeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct)
                   ?? throw new InvalidOperationException("Mock Business App returned an empty profile response.");

        if (body["registered"]?.GetValue<bool>() != true)
        {
            return new SupportSystemOutcome
            {
                OutcomeKey = MockBusinessAppProfile.NotRegisteredOutcome,
                ResultPayload = new JsonObject { ["profileTenant"] = body["tenant"]?.GetValue<string>() },
            };
        }

        return new SupportSystemOutcome
        {
            OutcomeKey = MockBusinessAppProfile.RegisteredOutcome,
            ResultPayload = new JsonObject
            {
                ["profileName"] = body["name"]?.GetValue<string>(),
                ["profileEmail"] = body["email"]?.GetValue<string>(),
                ["profileTenant"] = body["tenant"]?.GetValue<string>(),
                ["profileRole"] = body["role"]?.GetValue<string>(),
                ["profilePhone"] = body["phone"]?.GetValue<string>(),
                ["profileContactPreference"] = body["contactPreference"]?.GetValue<string>(),
            },
        };
    }

    private static async Task<SupportSystemOutcome> ToSaveOutcomeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
            return new SupportSystemOutcome
            {
                OutcomeKey = MockBusinessAppProfile.UpdatedOutcome,
                ResultPayload = new JsonObject { ["profileUpdateReference"] = body?["reference"]?.GetValue<string>() },
            };
        }

        // The business system saying no is an answer the journey routes on. Anything else (401, 5xx)
        // means the call itself failed, which is not an outcome.
        if (response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Forbidden)
        {
            var body = response.StatusCode == HttpStatusCode.UnprocessableEntity
                ? await response.Content.ReadFromJsonAsync<JsonObject>(ct)
                : null;
            return new SupportSystemOutcome
            {
                OutcomeKey = MockBusinessAppProfile.RejectedOutcome,
                ResultPayload = new JsonObject
                {
                    ["profileUpdateNote"] = body?["error"]?.GetValue<string>() ?? "We could not update your details.",
                },
            };
        }

        response.EnsureSuccessStatusCode();
        throw new InvalidOperationException("Unreachable: EnsureSuccessStatusCode throws for a non-success status.");
    }

    private static string InputText(IReadOnlyDictionary<string, SupportSystemInputValue> inputs, string key) =>
        inputs.TryGetValue(key, out var value) ? value.RawValue?.ToString() ?? "" : "";
}
