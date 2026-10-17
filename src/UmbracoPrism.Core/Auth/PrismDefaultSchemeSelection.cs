using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// The host application's own default authenticate and challenge schemes, captured before Prism
/// puts its per-request selectors in their place.
/// </summary>
internal sealed class PrismHostAuthenticationDefaults
{
    public string? Authenticate { get; set; }
    public string? Challenge { get; set; }
}

/// <summary>
/// Makes Prism's sign-in apply only where a tenant has configured it. Runs after every
/// <c>Configure&lt;AuthenticationOptions&gt;</c>, so whatever defaults the host application (and
/// Umbraco) set are known: those are kept for every other hostname, and the application defaults
/// become <see cref="PrismAuthenticationSchemes.DefaultAuthenticate"/> and
/// <see cref="PrismAuthenticationSchemes.DefaultChallenge"/>, which pick per request.
/// Forbid, sign-in and sign-out defaults are left exactly as the host set them.
/// </summary>
internal sealed class PrismAuthenticationDefaultsSetup(PrismHostAuthenticationDefaults hostDefaults)
    : IPostConfigureOptions<AuthenticationOptions>
{
    private static readonly HashSet<string> PrismSchemes =
    [
        PrismAuthenticationSchemes.MemberCookie,
        PrismAuthenticationSchemes.Oidc,
        PrismAuthenticationSchemes.DefaultAuthenticate,
        PrismAuthenticationSchemes.DefaultChallenge,
        PrismAuthenticationSchemes.NoHostDefault,
    ];

    public void PostConfigure(string? name, AuthenticationOptions options)
    {
        if (options.DefaultAuthenticateScheme != PrismAuthenticationSchemes.DefaultAuthenticate)
        {
            hostDefaults.Authenticate = HostScheme(options.DefaultAuthenticateScheme ?? options.DefaultScheme);
            options.DefaultAuthenticateScheme = PrismAuthenticationSchemes.DefaultAuthenticate;
        }

        if (options.DefaultChallengeScheme != PrismAuthenticationSchemes.DefaultChallenge)
        {
            hostDefaults.Challenge = HostScheme(options.DefaultChallengeScheme ?? options.DefaultScheme);
            options.DefaultChallengeScheme = PrismAuthenticationSchemes.DefaultChallenge;
        }
    }

    // A default naming one of Prism's own schemes was not the host's choice; treating it as
    // the host's would hand non-tenant hostnames straight back to Prism.
    private static string? HostScheme(string? scheme) =>
        scheme is null || PrismSchemes.Contains(scheme) ? null : scheme;
}

/// <summary>
/// The per-request choice behind <see cref="PrismAuthenticationSchemes.DefaultAuthenticate"/> and
/// <see cref="PrismAuthenticationSchemes.DefaultChallenge"/>.
/// </summary>
internal static class PrismDefaultSchemeSelector
{
    public static string Authenticate(HttpContext context) =>
        SignInAppliesTo(context)
            ? PrismAuthenticationSchemes.MemberCookie
            : HostDefaults(context).Authenticate ?? PrismAuthenticationSchemes.NoHostDefault;

    public static string Challenge(HttpContext context) =>
        SignInAppliesTo(context)
            ? PrismAuthenticationSchemes.Oidc
            : HostDefaults(context).Challenge ?? PrismAuthenticationSchemes.NoHostDefault;

    // Resolved from the hostname itself rather than IPrismContext.CurrentTenant: a host may run
    // UseAuthentication before Umbraco's pipeline (and so before PrismTenantMiddleware). The
    // lookup is the same cached one that middleware uses, and it never throws (a database error
    // reads as "no tenant"), so a failure leaves the host in charge rather than Prism.
    private static bool SignInAppliesTo(HttpContext context)
    {
        var tenants = context.RequestServices.GetRequiredService<ITenantService>();
        var tenant = tenants.GetByDomainAsync(context.Request.Host.Host).GetAwaiter().GetResult();
        return tenant?.HasSignInConfigured() == true;
    }

    private static PrismHostAuthenticationDefaults HostDefaults(HttpContext context) =>
        context.RequestServices.GetRequiredService<PrismHostAuthenticationDefaults>();
}

/// <summary>
/// The handler behind <see cref="PrismAuthenticationSchemes.NoHostDefault"/>: authenticates nobody;
/// the base class answers a challenge with 401 and a forbid with 403.
/// </summary>
internal sealed class PrismNoHostDefaultHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
