using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Resolves a waiting Wayfinder support-system invocation in process, shared by every custom
/// Automate action in this host that does so.
/// </summary>
internal static class SupportSystemOutcomeResolver
{
    internal sealed record Resolution(string ResponseState, string? Error);

    internal static Resolution Resolve(
        UmbracoProcessManagerEngine engine,
        ILogger logger,
        string invocationId,
        string outcomeKey,
        JsonObject? payload)
    {
        var result = engine.ResolveSupportSystemOutcome(invocationId, outcomeKey, payload);
        if (result.ResponseState != "error")
        {
            return new Resolution(result.ResponseState, null);
        }

        var problem = result.Problems.Count > 0 ? result.Problems[0] : null;

        // An unknown / already-resolved invocation is a safe no-op, not a run failure.
        if (problem?.Code == "SUPPORT_SYSTEM_INVOCATION_NOT_FOUND")
        {
            logger.LogInformation(
                "Wayfinder support-system invocation {InvocationId} was already resolved or not found; no-op.",
                invocationId);
            return new Resolution("no-op", null);
        }

        return new Resolution("error", problem?.Message ?? "Failed to resolve the outcome.");
    }
}
