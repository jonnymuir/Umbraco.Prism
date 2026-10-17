namespace UmbracoPrism.Core.Auth;

/// <summary>
/// The authentication schemes Prism registers.
/// </summary>
public static class PrismAuthenticationSchemes
{
    /// <summary>The member session cookie, bound to the tenant that signed the member in.</summary>
    public const string MemberCookie = "PrismMemberCookie";

    /// <summary>Per-tenant OIDC sign-in, against Entra ID or any generic OIDC provider.</summary>
    public const string Oidc = "PrismEntraID";

    /// <summary>
    /// The application's default authenticate scheme once Prism is installed:
    /// <see cref="MemberCookie"/> on the hostname of a tenant with sign-in configured, and the host
    /// application's own default everywhere else.
    /// </summary>
    public const string DefaultAuthenticate = "PrismDefaultAuthenticate";

    /// <summary>
    /// The application's default challenge scheme once Prism is installed: <see cref="Oidc"/> on the
    /// hostname of a tenant with sign-in configured, and the host application's own default
    /// everywhere else.
    /// </summary>
    public const string DefaultChallenge = "PrismDefaultChallenge";

    /// <summary>
    /// Stands in, on hostnames where Prism's sign-in doesn't apply, for a default the host
    /// application never set: it authenticates nobody, answers a challenge with 401 and a forbid
    /// with 403.
    /// </summary>
    public const string NoHostDefault = "PrismNoHostDefault";
}
