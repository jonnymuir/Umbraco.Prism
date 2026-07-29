using UmbracoPrism.Core.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Generates and validates workflow step nonces used to bind form submissions
/// to their server-authoritative field definitions, preventing field injection
/// and constraint bypass attacks.
/// </summary>
public interface IStageNonceService
{
    /// <summary>
    /// Creates a nonce, caches the step's field definitions under it, and returns the nonce string.
    /// </summary>
    Task<string> CreateAsync(IReadOnlyList<FieldRenderPayload> fields, CancellationToken ct = default);

    /// <summary>
    /// Resolves a nonce back to its field definitions. Returns null if the nonce has
    /// expired or never existed — the caller should redirect to GET in this case.
    /// </summary>
    Task<IReadOnlyList<FieldRenderPayload>?> ResolveAsync(string nonce, CancellationToken ct = default);
}
