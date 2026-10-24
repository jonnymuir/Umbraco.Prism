using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Umbraco.AI.Core.Providers;
using Umbraco.AI.Google;

namespace UmbracoPrism.TestSite.FieldRecording;

/// <summary>
/// Umbraco.AI's Google provider, but with a request timeout long enough for a free-tier Gemini model
/// to answer. See the registration in Program.cs for why this exists and when to remove it.
/// </summary>
/// <remarks>
/// Umbraco.AI.Google builds the Google SDK client with no HTTP options, so every request gets
/// .NET's default HttpClient timeout of 100 seconds, and a busy free-tier model often takes longer.
/// This keeps the "google" provider id, so existing connections and profiles are unaffected.
/// </remarks>
[AIProvider("google", "Google")]
public sealed class PatientGoogleProvider : GoogleProvider
{
    public PatientGoogleProvider(IAIProviderInfrastructure infrastructure, IMemoryCache cache)
        : base(infrastructure, cache)
    {
        Capabilities.Clear();
        WithCapability<PatientGoogleChatCapability>();
    }
}

/// <summary>The Google chat capability with <see cref="RequestTimeout"/> on every request.</summary>
public sealed class PatientGoogleChatCapability(GoogleProvider provider) : GoogleChatCapability(provider)
{
    // Inside Umbraco Automate's 5-minute limit for a step, so a slow answer still arrives in time.
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(4);

    protected override async Task<IChatClient> CreateClientAsync(
        GoogleProviderSettings settings, string? modelId, CancellationToken cancellationToken = default)
    {
        // No model chosen: leave Umbraco.AI to pick its default, with the standard timeout.
        if (modelId is null)
        {
            return await base.CreateClientAsync(settings, modelId, cancellationToken);
        }

        var client = new Client(
            apiKey: settings.ApiKey,
            httpOptions: new HttpOptions { Timeout = (int)RequestTimeout.TotalMilliseconds });
        return client.AsIChatClient(modelId);
    }
}
