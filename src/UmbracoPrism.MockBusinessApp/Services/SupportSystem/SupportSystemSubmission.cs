using System.Text.Json;

namespace UmbracoPrism.MockBusinessApp.Services.SupportSystem;

/// <summary>
/// One submission to this mock support system — MockBusinessApp's sole remaining purpose (see
/// docs/guides/support-systems.md in the core Wayfinder repo: a genuinely separate downstream
/// backend a Wayfinder-hosted engine calls out to for real business decisioning, via an
/// <c>ISupportSystemClient</c> implementation living on the *calling* host, not here). This
/// contract is entirely this app's own business — nothing in Wayfinder prescribes its shape.
/// </summary>
public sealed record SupportSystemSubmission
{
    public required string Id { get; init; }
    public required IReadOnlyDictionary<string, JsonElement> Fields { get; init; }
    public string? CallbackUrl { get; init; }
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool Decided { get; init; }
    public string? OutcomeKey { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
}
