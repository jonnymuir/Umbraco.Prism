namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Waiting metadata authored on join gateways and projected into runtime waiting envelopes.
/// Touchpoint-level waiting is not supported — waiting copy lives on the gateway that gates the wait.
/// </summary>
public record WaitingMetadata
{
    /// <summary>Message displayed to the user while waiting.</summary>
    public string Content { get; init; } = "Please wait while we process your request.";

    /// <summary>Expected wait time in seconds (used for expectation management).</summary>
    public int ExpectedWaitSeconds { get; init; } = 30;

    /// <summary>Client poll interval in milliseconds.</summary>
    public int PollIntervalMs { get; init; } = 3000;

    /// <summary>Whether to offer the user a "return later" deferral option.</summary>
    public bool AllowDefer { get; init; } = true;

    /// <summary>Optional custom deferral message.</summary>
    public string? DeferMessage { get; init; }
}
