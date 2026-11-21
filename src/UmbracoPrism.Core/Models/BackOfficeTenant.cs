namespace UmbracoPrism.Core.Models;

/// <summary>
/// A tenant a business API trusts tokens from. <paramref name="Audience"/> is required for a generic OIDC
/// tenant (one with an <paramref name="OidcAuthority"/>): the API accepts only access tokens whose
/// <c>aud</c> contains it (the RFC 9068 resource-server rule), never a token merely issued to
/// <paramref name="ClientId"/> (<c>azp</c>) or an ID token for that client. The identity provider must
/// add it to access tokens (an audience mapper). It is ignored for an Entra tenant.
/// </summary>
public record BackOfficeTenant(string EntraTenantId, string ClientId, string Code, string DisplayName, string? OidcAuthority = null, string? Audience = null);
