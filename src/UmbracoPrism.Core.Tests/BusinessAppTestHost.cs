using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoPrism.MockBusinessApp.Services.Members;
using UmbracoPrism.MockBusinessApp.Services.Profile;
using UmbracoPrism.MockBusinessApp.Services.SupportSystem;

namespace UmbracoPrism.Core.Tests;

/// <param name="Username">
/// The account's login name (<c>preferred_username</c>), which a person chooses at registration and so
/// must never be trusted as an email. Defaults to the email.
/// </param>
internal sealed record OidcPerson(string Subject, string Email, bool EmailVerified = true, string? Username = null);

/// <summary>
/// A small host mounting the Mock Business App's routes through their public <c>Map...</c> seams, with a
/// stand-in authentication scheme that turns two request headers into the claims a validated JWT would
/// carry (real JWT validation is covered by <see cref="MockBusinessAppHostSecurityTests"/> and the
/// validator tests). Two Entra tenants and three members are configured: the same person in both tenants,
/// and a second member of the first. A third tenant has its own identity provider and accepts online
/// registration; a fourth has one but does not.
/// </summary>
internal sealed class BusinessAppTestHost : IAsyncDisposable
{
    public const string AlphaTenant = "00000000-0000-0000-0000-000000000001";
    public const string BetaTenant = "00000000-0000-0000-0000-000000000003";
    public const string OpenIssuer = "https://idp.example.test/realms/open";
    public const string ClosedIssuer = "https://idp.example.test/realms/closed";

    private readonly WebApplication _app;

    public BusinessAppTestHost()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = AlphaTenant,
            ["PrismBusinessApp:Tenants:0:ClientId"] = "alpha-client",
            ["PrismBusinessApp:Tenants:0:Code"] = "ALPHA-CORP",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Alpha Corporation",
            ["PrismBusinessApp:Tenants:1:EntraTenantId"] = BetaTenant,
            ["PrismBusinessApp:Tenants:1:ClientId"] = "beta-client",
            ["PrismBusinessApp:Tenants:1:Code"] = "BETA-LLC",
            ["PrismBusinessApp:Tenants:1:DisplayName"] = "Beta Services",
            ["PrismBusinessApp:Tenants:2:EntraTenantId"] = "",
            ["PrismBusinessApp:Tenants:2:ClientId"] = "open-client",
            ["PrismBusinessApp:Tenants:2:OidcAuthority"] = OpenIssuer,
            ["PrismBusinessApp:Tenants:2:Code"] = "OPEN-IDP",
            ["PrismBusinessApp:Tenants:2:DisplayName"] = "Open Registration",
            ["PrismBusinessApp:Tenants:3:EntraTenantId"] = "",
            ["PrismBusinessApp:Tenants:3:ClientId"] = "closed-client",
            ["PrismBusinessApp:Tenants:3:OidcAuthority"] = ClosedIssuer,
            ["PrismBusinessApp:Tenants:3:Code"] = "CLOSED-IDP",
            ["PrismBusinessApp:Tenants:3:DisplayName"] = "Closed Registration",
            ["PrismBusinessApp:SelfRegistration:Tenants:0"] = "OPEN-IDP",
            ["PrismBusinessApp:Members:3:Email"] = "lee@example.test",
            ["PrismBusinessApp:Members:3:TenantCode"] = "OPEN-IDP",
            ["PrismBusinessApp:Members:3:BackOfficeId"] = "O-1",
            ["PrismBusinessApp:Members:3:Role"] = "Admin",
            ["PrismBusinessApp:Members:0:Email"] = "pat@example.test",
            ["PrismBusinessApp:Members:0:TenantCode"] = "ALPHA-CORP",
            ["PrismBusinessApp:Members:0:BackOfficeId"] = "A-1",
            ["PrismBusinessApp:Members:0:Role"] = "Admin",
            ["PrismBusinessApp:Members:1:Email"] = "pat@example.test",
            ["PrismBusinessApp:Members:1:TenantCode"] = "BETA-LLC",
            ["PrismBusinessApp:Members:1:BackOfficeId"] = "B-1",
            ["PrismBusinessApp:Members:1:Role"] = "Viewer",
            ["PrismBusinessApp:Members:2:Email"] = "sam@example.test",
            ["PrismBusinessApp:Members:2:TenantCode"] = "ALPHA-CORP",
            ["PrismBusinessApp:Members:2:BackOfficeId"] = "A-2",
            ["PrismBusinessApp:Members:2:Role"] = "Editor",
        });
        builder.Services.AddSingleton<ProfileStore>();
        builder.Services.AddSingleton<MemberRegistry>();
        builder.Services.AddSingleton<MemberDirectory>();
        builder.Services.AddSingleton<MemberRegistrar>();
        builder.Services.AddSingleton<ContributionsStore>();
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderClaimsAuthHandler>("Test", null);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapProfile();
        _app.MapMembers();
        _app.MapContributions();
        _app.StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>A client with no credentials.</summary>
    public HttpClient Anonymous() => _app.GetTestClient();

    /// <summary>A client presenting a token for <paramref name="email"/> in <paramref name="tenantId"/>.</summary>
    public HttpClient As(string email, string tenantId)
    {
        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(HeaderClaimsAuthHandler.EmailHeader, email);
        client.DefaultRequestHeaders.Add(HeaderClaimsAuthHandler.TenantHeader, tenantId);
        return client;
    }

    /// <summary>A client presenting the claims a validated token from a tenant's own identity provider carries.</summary>
    public HttpClient AsOidc(string issuer, OidcPerson person)
    {
        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(HeaderClaimsAuthHandler.IssuerHeader, issuer);
        client.DefaultRequestHeaders.Add(HeaderClaimsAuthHandler.SubjectHeader, person.Subject);
        client.DefaultRequestHeaders.Add(HeaderClaimsAuthHandler.EmailHeader, person.Email);
        client.DefaultRequestHeaders.Add(HeaderClaimsAuthHandler.VerifiedHeader, person.EmailVerified ? "true" : "false");
        client.DefaultRequestHeaders.Add(HeaderClaimsAuthHandler.UsernameHeader, person.Username ?? person.Email);
        return client;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private sealed class HeaderClaimsAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string EmailHeader = "X-Test-Email";
        public const string TenantHeader = "X-Test-Tid";
        public const string IssuerHeader = "X-Test-Iss";
        public const string SubjectHeader = "X-Test-Sub";
        public const string VerifiedHeader = "X-Test-Email-Verified";
        public const string UsernameHeader = "X-Test-Username";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(EmailHeader, out var email))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Claim[] claims;
            if (Request.Headers.TryGetValue(IssuerHeader, out var issuer))
            {
                claims =
                [
                    new Claim("iss", issuer.ToString()),
                    new Claim("sub", Request.Headers[SubjectHeader].ToString()),
                    new Claim("email", email.ToString()),
                    new Claim("email_verified", Request.Headers[VerifiedHeader].ToString()),
                    new Claim("preferred_username", Request.Headers[UsernameHeader].ToString()),
                ];
            }
            else if (Request.Headers.TryGetValue(TenantHeader, out var tid))
            {
                claims = [new Claim("preferred_username", email.ToString()), new Claim("tid", tid.ToString())];
            }
            else
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")));
        }
    }
}
