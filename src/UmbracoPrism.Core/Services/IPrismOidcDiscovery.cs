namespace UmbracoPrism.Core.Services;

/// <summary>The endpoints a generic OIDC provider advertises in its discovery document.</summary>
/// <param name="Issuer">The issuer the provider declares.</param>
/// <param name="AuthorizationEndpoint">Where the browser is sent to sign in.</param>
/// <param name="TokenEndpoint">Where the server redeems a code or refresh token.</param>
/// <param name="EndSessionEndpoint">Where the browser is sent to end the provider session, when the provider has one.</param>
public sealed record PrismOidcEndpoints(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string? EndSessionEndpoint);

/// <summary>
/// Reads a generic OIDC provider's endpoints from its validated discovery document, so no code
/// assumes one vendor's URL layout.
/// </summary>
public interface IPrismOidcDiscovery
{
    /// <summary>
    /// Gets the endpoints for <paramref name="authority"/>. Throws when the document cannot be
    /// read, lacks a required endpoint, or declares an issuer other than the configured authority.
    /// </summary>
    Task<PrismOidcEndpoints> GetAsync(string authority, CancellationToken cancellationToken = default);
}
