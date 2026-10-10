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
using UmbracoPrism.MockBusinessApp.Services.Profile;
using UmbracoPrism.MockBusinessApp.Services.SupportSystem;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// A small host mounting the Mock Business App's routes through their public <c>Map...</c> seams, with a
/// stand-in authentication scheme that turns two request headers into the claims a validated JWT would
/// carry (real JWT validation is covered by <see cref="MockBusinessAppHostSecurityTests"/> and the
/// validator tests). Two tenants and three members are configured: the same person in both tenants, and
/// a second member of the first.
/// </summary>
internal sealed class BusinessAppTestHost : IAsyncDisposable
{
    public const string AlphaTenant = "00000000-0000-0000-0000-000000000001";
    public const string BetaTenant = "00000000-0000-0000-0000-000000000003";

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
        builder.Services.AddSingleton<ContributionsStore>();
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderClaimsAuthHandler>("Test", null);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapProfile();
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

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private sealed class HeaderClaimsAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string EmailHeader = "X-Test-Email";
        public const string TenantHeader = "X-Test-Tid";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(EmailHeader, out var email) || !Request.Headers.TryGetValue(TenantHeader, out var tid))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim("preferred_username", email.ToString()), new Claim("tid", tid.ToString())], "Test");
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}
