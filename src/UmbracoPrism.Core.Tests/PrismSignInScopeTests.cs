using System.Net;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Moq;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Middleware;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// SECURITY REGRESSION: Prism's member sign-in applies only on the hostname of a tenant that has
/// sign-in configured. Everywhere else the host application's own authentication is left exactly
/// as it was, and neither side's session is honoured on the other's hostnames.
///
/// Exercised through a real ASP.NET Core pipeline: the host application's own cookie login, plus
/// exactly what Prism registers (<see cref="PrismMemberSignInExtensions.AddPrismMemberSignIn"/>,
/// <see cref="PrismOidcConfiguration"/>, <see cref="PrismTenantMiddleware"/>), with a stub tenant
/// store and a fixed identity-provider configuration so nothing touches the network.
/// </summary>
public sealed class PrismSignInScopeTests
{
    private const string HostLogin = "/host-login";

    // Tenants, by hostname.
    private const string SigningInHost = "members.example.test";
    private const string NoSignInHost = "plain.example.test";
    private const string AuthorityOnlyHost = "half.example.test";
    private const string NoTenantHost = "www.host.example.test";

    private const string Authority = "https://idp.example.test/realms/members";
    private const string ClientId = "members-app";

    private static readonly Dictionary<string, PrismTenant> Tenants = new(StringComparer.OrdinalIgnoreCase)
    {
        [SigningInHost] = new() { Hostname = SigningInHost, OidcAuthority = Authority, OidcClientId = ClientId },
        [NoSignInHost] = new() { Hostname = NoSignInHost },
        // Binding accepts an issuer match alone when no client ID is set, so only the "sign-in
        // configured" rule keeps a session minted elsewhere from being honoured here.
        [AuthorityOnlyHost] = new() { Hostname = AuthorityOnlyHost, OidcAuthority = Authority },
    };

    public enum HostSetup
    {
        /// <summary>The host's own login is the default, configured before Prism's composer runs.</summary>
        DefaultsBeforePrism,
        /// <summary>The same, configured after Prism (a host's Program.cs often runs later).</summary>
        DefaultsAfterPrism,
        /// <summary>The host sets no authentication defaults at all.</summary>
        NoDefaults,
    }

    // ---- Where Prism's sign-in doesn't apply, the host's own login does ----

    [Theory]
    [InlineData(NoTenantHost, HostSetup.DefaultsBeforePrism)]
    [InlineData(NoTenantHost, HostSetup.DefaultsAfterPrism)]
    [InlineData(NoSignInHost, HostSetup.DefaultsBeforePrism)]
    [InlineData(NoSignInHost, HostSetup.DefaultsAfterPrism)]
    [InlineData(AuthorityOnlyHost, HostSetup.DefaultsBeforePrism)]
    public async Task WithoutPrismSignIn_AProtectedHostPage_SendsAnonymousVisitorsToTheHostsOwnLogin(string hostname, HostSetup setup)
    {
        using var host = await StartAsync(setup);

        var response = await SendAsync(host, hostname, "/protected");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be(HostLogin,
            "Prism must not take over sign-in on a hostname with no signing-in tenant");
    }

    [Theory]
    [InlineData(NoTenantHost)]
    [InlineData(NoSignInHost)]
    public async Task WithoutPrismSignIn_AndNoHostDefaults_AProtectedPageIsUnauthorised_NotSentToPrism(string hostname)
    {
        using var host = await StartAsync(HostSetup.NoDefaults);

        var response = await SendAsync(host, hostname, "/protected");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(HostSetup.DefaultsBeforePrism)]
    [InlineData(HostSetup.DefaultsAfterPrism)]
    [InlineData(HostSetup.NoDefaults)]
    public async Task OnATenantWithSignIn_AProtectedPage_SendsAnonymousVisitorsToThatTenantsIdentityProvider(HostSetup setup)
    {
        using var host = await StartAsync(setup);

        var response = await SendAsync(host, SigningInHost, "/protected");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).Should().Be($"{Authority}/protocol/openid-connect/auth");
        location.Query.Should().Contain($"client_id={ClientId}");
    }

    [Fact]
    public async Task OnAHostnameWithoutPrismSignIn_EvenAnExplicitPrismChallengeIsNotFound()
    {
        // Behind AccountController's own check: whatever asks for Prism's OIDC flow on a
        // non-signing-in hostname, it never starts with placeholder settings.
        using var host = await StartAsync(HostSetup.DefaultsBeforePrism);

        var challenge = await SendAsync(host, NoTenantHost, "/prism-challenge");

        challenge.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "with no tenant to resolve, Prism must never redirect to its placeholder authority");
        challenge.Headers.Location.Should().BeNull();
    }

    // ---- Sessions are honoured only on their own side ----

    [Theory]
    [InlineData(NoTenantHost)]
    [InlineData(NoSignInHost)]
    public async Task TheHostsOwnSession_IsHonouredWherePrismSignInDoesNotApply(string hostname)
    {
        using var host = await StartAsync(HostSetup.DefaultsBeforePrism);
        var hostCookie = await SignInAsync(host, hostname, "/host-signin");

        var response = await SendAsync(host, hostname, "/protected", hostCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("host-member");
    }

    [Fact]
    public async Task TheHostsOwnSession_IsNotHonouredOnATenantWithSignIn()
    {
        using var host = await StartAsync(HostSetup.DefaultsBeforePrism);
        var hostCookie = await SignInAsync(host, NoTenantHost, "/host-signin");

        var response = await SendAsync(host, SigningInHost, "/protected", hostCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            "a host session must not pass for a Prism member on the tenant's hostname");
        response.Headers.Location!.GetLeftPart(UriPartial.Authority).Should().Be(new Uri(Authority).GetLeftPart(UriPartial.Authority));
    }

    [Fact]
    public async Task APrismMemberSession_IsHonouredOnItsOwnTenant()
    {
        using var host = await StartAsync(HostSetup.DefaultsBeforePrism);
        var memberCookie = await SignInAsync(host, SigningInHost, "/prism-signin");

        var response = await SendAsync(host, SigningInHost, "/protected", memberCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("prism-member");
    }

    [Theory]
    [InlineData(NoTenantHost)]
    [InlineData(NoSignInHost)]
    public async Task APrismMemberSession_IsNotHonouredWherePrismSignInDoesNotApply(string hostname)
    {
        using var host = await StartAsync(HostSetup.DefaultsBeforePrism);
        var memberCookie = await SignInAsync(host, SigningInHost, "/prism-signin");

        var response = await SendAsync(host, hostname, "/protected", memberCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be(HostLogin);
    }

    [Theory]
    [InlineData(NoTenantHost)]
    [InlineData(NoSignInHost)]
    [InlineData(AuthorityOnlyHost)]
    public async Task APrismMemberSession_IsRejectedByPrismsOwnCookie_WhereSignInIsNotConfigured(string hostname)
    {
        // Asked of Prism's cookie by name, as Prism's own code does: even then a session is only
        // ever honoured on a tenant that has sign-in configured.
        using var host = await StartAsync(HostSetup.DefaultsBeforePrism);
        var memberCookie = await SignInAsync(host, SigningInHost, "/prism-signin");

        var response = await SendAsync(host, hostname, "/prism-member", memberCookie);

        (await response.Content.ReadAsStringAsync()).Should().Be("anonymous");
    }

    // ---- A Prism sign-in is only ever a Prism session ----

    [Fact]
    public async Task APrismSignIn_IsWrittenToPrismsOwnCookie_EvenWhenTheHostDefaultsSignInToItsOwn()
    {
        using var host = await StartAsync(HostSetup.DefaultsBeforePrism);

        var oidc = host.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(PrismAuthenticationSchemes.Oidc);
        var authentication = host.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        authentication.DefaultSignInScheme.Should().Be("HostCookie", "the host's own sign-in default is left alone");
        oidc.SignInScheme.Should().Be(PrismAuthenticationSchemes.MemberCookie);
    }

    // ------------------------------------------------------------------

    private static async Task<IHost> StartAsync(HostSetup setup)
    {
        var tenants = new Mock<ITenantService>();
        tenants.Setup(t => t.GetByDomainAsync(It.IsAny<string>()))
            .ReturnsAsync((string domain) => Tenants.GetValueOrDefault(domain));

        void HostAuthentication(IServiceCollection services)
        {
            var builder = setup == HostSetup.NoDefaults
                ? services.AddAuthentication()
                : services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "HostCookie";
                    options.DefaultSignInScheme = "HostCookie";
                });
            builder.AddCookie("HostCookie", options =>
            {
                options.Cookie.Name = "HostCookie";
                options.LoginPath = HostLogin;
            });
        }

        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthorization();
                    services.AddHttpClient();

                    if (setup != HostSetup.DefaultsAfterPrism)
                    {
                        HostAuthentication(services);
                    }

                    services.AddSingleton(tenants.Object);
                    services.AddSingleton(Mock.Of<IPrismSigningKeyCache>());
                    services.AddSingleton<IPrismTenantBindingValidator, PrismTenantBindingValidator>();
                    services.AddScoped<IPrismContext, RequestPrismContext>();
                    services.AddHttpContextAccessor();
                    services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, PrismOidcConfiguration>();
                    services.AddPrismMemberSignIn();

                    // A fixed provider configuration, so a challenge never fetches discovery.
                    services.PostConfigure<OpenIdConnectOptions>(PrismAuthenticationSchemes.Oidc, options =>
                        options.Configuration = new OpenIdConnectConfiguration
                        {
                            AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                            EndSessionEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/logout",
                        });

                    if (setup == HostSetup.DefaultsAfterPrism)
                    {
                        HostAuthentication(services);
                    }
                })
                .Configure(app =>
                {
                    // Umbraco runs PrismTenantMiddleware ahead of authentication; so does this.
                    app.UseMiddleware<PrismTenantMiddleware>();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/protected", (HttpContext c) => c.User.Identity!.AuthenticationType!)
                            .RequireAuthorization();

                        endpoints.MapGet("/prism-member", async (HttpContext c) =>
                            (await c.AuthenticateAsync(PrismAuthenticationSchemes.MemberCookie)).Succeeded ? "prism-member" : "anonymous");

                        endpoints.MapGet("/prism-challenge", () =>
                            Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [PrismAuthenticationSchemes.Oidc]));

                        endpoints.MapGet("/host-signin", async (HttpContext c) =>
                        {
                            var hostMember = new ClaimsIdentity(
                                [new Claim("email", "admin@host.example"), new Claim("email_verified", "true")], "host-member");
                            await c.SignInAsync("HostCookie", new ClaimsPrincipal(hostMember));
                        });

                        // What PrismOidcConfiguration writes after a real OIDC callback: a member
                        // bound to the tenant by its issuer and audience.
                        endpoints.MapGet("/prism-signin", async (HttpContext c) =>
                        {
                            var member = new ClaimsIdentity(
                                [new Claim("iss", Authority), new Claim("aud", ClientId), new Claim("sub", "member-1")], "prism-member");
                            await c.SignInAsync(PrismAuthenticationSchemes.MemberCookie, new ClaimsPrincipal(member));
                        });
                    });
                }))
            .StartAsync();

        return host;
    }

    private static async Task<HttpResponseMessage> SendAsync(IHost host, string hostname, string path, string? cookie = null)
    {
        var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{hostname}{path}"));
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request);
    }

    // Signs in on hostname and returns the session cookie as a Cookie header value.
    private static async Task<string> SignInAsync(IHost host, string hostname, string path)
    {
        var response = await SendAsync(host, hostname, path);
        response.EnsureSuccessStatusCode();
        return string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
    }

    private sealed class RequestPrismContext : IPrismContext
    {
        public PrismTenant? CurrentTenant { get; set; }
        public string? LastAuthorizationFailureReason => null;

        public Task<System.Net.Http.Headers.AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(bool forceRefresh = false) =>
            Task.FromResult<System.Net.Http.Headers.AuthenticationHeaderValue?>(null);
    }
}
