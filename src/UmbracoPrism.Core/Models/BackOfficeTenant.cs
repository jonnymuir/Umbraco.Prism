namespace UmbracoPrism.Core.Models;

/// <summary>
/// A tenant a business API trusts tokens from. <paramref name="Audience"/> is optional and only
/// meaningful for a generic OIDC tenant (one with an <paramref name="OidcAuthority"/>): when set,
/// the API accepts only access tokens whose <c>aud</c> contains it (the RFC 9068 resource-server
/// rule), and no longer accepts a token merely because it was issued to <paramref name="ClientId"/>
/// (<c>azp</c>) or is an ID token for that client. Set it for any API reachable beyond a
/// development machine; the identity provider must add it to access tokens (an audience mapper).
/// </summary>
public record BackOfficeTenant(string EntraTenantId, string ClientId, string Code, string DisplayName, string? OidcAuthority = null, string? Audience = null);
