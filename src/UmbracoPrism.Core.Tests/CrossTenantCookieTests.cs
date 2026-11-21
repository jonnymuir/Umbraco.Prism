using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// The trust chain's negative cases through the real member cookie validation: a session issued for
/// one tenant must never be honoured on another tenant's hostname, whichever identity provider either
/// uses. These run the same <c>OnValidatePrincipal</c> the booted host installs.
/// </summary>
public class CrossTenantCookieTests
{
    private static readonly PrismTenant EntraA = new() { Id = 1, Name = "A", Hostname = "a.example", EntraTenantId = "tenant-a", EntraClientId = "client-a" };
    private static readonly PrismTenant EntraB = new() { Id = 2, Name = "B", Hostname = "b.example", EntraTenantId = "tenant-b", EntraClientId = "client-b" };
    private static readonly PrismTenant RealmA = new() { Id = 3, Name = "RA", Hostname = "ra.example", OidcAuthority = "https://idp.example/realms/a", OidcClientId = "web-a" };
    private static readonly PrismTenant RealmB = new() { Id = 4, Name = "RB", Hostname = "rb.example", OidcAuthority = "https://idp.example/realms/b", OidcClientId = "web-b" };
    private static readonly PrismTenant NoSignIn = new() { Id = 5, Name = "None", Hostname = "none.example" };

    public static TheoryData<PrismTenant, ClaimsPrincipal, PrismTenant?> Cases => new()
    {
        // session issued for X, request arriving on Y
        { EntraA, EntraPrincipal("tenant-a"), EntraB },
        { EntraB, EntraPrincipal("tenant-b"), EntraA },
        { RealmA, RealmPrincipal("https://idp.example/realms/a", "web-a"), RealmB },
        { RealmB, RealmPrincipal("https://idp.example/realms/b", "web-b"), RealmA },
        { EntraA, EntraPrincipal("tenant-a"), RealmA },
        { RealmA, RealmPrincipal("https://idp.example/realms/a", "web-a"), EntraA },
        { RealmA, RealmPrincipal("https://idp.example/realms/a", "other-client"), RealmA },
        { EntraA, EntraPrincipal("tenant-a"), NoSignIn },
        { EntraA, EntraPrincipal("tenant-a"), null },
        { EntraA, EntraPrincipal(null), EntraA },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ASessionIsRefused_OnAnyTenantOtherThanItsOwn(PrismTenant issuedFor, ClaimsPrincipal principal, PrismTenant? requestTenant)
    {
        _ = issuedFor;
        var context = CreateContext(principal, requestTenant);

        await PrismSessionValidator.OnValidatePrincipalAsync(context);

        context.Principal.Should().BeNull("a cookie for one tenant must not sign anyone in on another");
    }

    [Theory]
    [InlineData("entra")]
    [InlineData("realm")]
    public async Task ASessionIsHonoured_OnItsOwnTenant(string kind)
    {
        var (principal, tenant) = kind == "entra"
            ? (EntraPrincipal("tenant-a"), EntraA)
            : (RealmPrincipal("https://idp.example/realms/a", "web-a"), RealmA);
        var context = CreateContext(principal, tenant);

        await PrismSessionValidator.OnValidatePrincipalAsync(context);

        context.Principal.Should().NotBeNull();
    }

    private static ClaimsPrincipal EntraPrincipal(string? tenantId) =>
        new(new ClaimsIdentity(tenantId is null ? [] : [new Claim("tid", tenantId)], "test"));

    private static ClaimsPrincipal RealmPrincipal(string issuer, string clientId) =>
        new(new ClaimsIdentity([new Claim("iss", issuer), new Claim("azp", clientId), new Claim("aud", clientId)], "test"));

    private static CookieValidatePrincipalContext CreateContext(ClaimsPrincipal principal, PrismTenant? requestTenant)
    {
        var prism = new RequestTenantContext { CurrentTenant = requestTenant };
        var services = new ServiceCollection()
            .AddSingleton<IPrismContext>(prism)
            .AddSingleton<IPrismTenantBindingValidator>(new PrismTenantBindingValidator())
            .AddSingleton(Options.Create(new PrismSessionOptions { RevalidationIntervalMinutes = 0 }))
            .AddLogging()
            .BuildServiceProvider();
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), PrismAuthenticationSchemes.MemberCookie);
        return new CookieValidatePrincipalContext(
            new DefaultHttpContext { RequestServices = services },
            new AuthenticationScheme(PrismAuthenticationSchemes.MemberCookie, null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            ticket);
    }

    private sealed class RequestTenantContext : IPrismContext
    {
        public PrismTenant? CurrentTenant { get; set; }

        public string? LastAuthorizationFailureReason => null;

        public Task<System.Net.Http.Headers.AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(bool forceRefresh = false) =>
            Task.FromResult<System.Net.Http.Headers.AuthenticationHeaderValue?>(null);
    }
}
