using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.IntegrationTests;

/// <summary>
/// The booted host with two tenants on two identity-provider realms: whatever tenant A's member can
/// present (their session cookie, their biometric token), tenant B's hostname refuses. Each case has
/// a same-tenant control, so a refusal cannot be a request that would have failed anyway.
/// The unit suites (<c>CrossTenantCookieTests</c>, the biometric tests) cover each check on its own;
/// this proves the real pipeline wires them together.
/// </summary>
[Collection(BootedTestSite.Name)]
public sealed class TwoTenantIsolationTests(TestSiteFactory factory)
{
    private sealed record Realm(string Host, string Authority, string ClientId);

    private static readonly Realm A = new("tenant-a.example.com", "https://idp.example.com/realms/tenant-a", "client-a");
    private static readonly Realm B = new("tenant-b.example.com", "https://idp.example.com/realms/tenant-b", "client-b");

    private const string RevokePath = "/umbraco/prism/mobile/biometric/revoke";
    private const string ExchangePath = "/umbraco/prism/mobile/biometric/exchange";

    private static readonly object SeedGate = new();
    private static Dictionary<string, int>? _tenantIds;

    private int TenantId(Realm realm)
    {
        lock (SeedGate)
        {
            _tenantIds ??= Seed();
            return _tenantIds[realm.Host];
        }
    }

    private Dictionary<string, int> Seed()
    {
        var databaseFactory = factory.Services.GetRequiredService<IUmbracoDatabaseFactory>();
        MemberSessionCookie.WaitForPrismTables(factory.Services);

        using var db = databaseFactory.CreateDatabase();
        var ids = new Dictionary<string, int>();
        foreach (var realm in new[] { A, B })
        {
            var tenant = new PrismTenantSchema
            {
                Name = realm.Host,
                Hostname = realm.Host,
                OidcAuthority = realm.Authority,
                OidcClientId = realm.ClientId,
                AllowBiometricLogin = true,
            };
            db.Insert(tenant);
            ids[realm.Host] = tenant.Id;
        }

        return ids;
    }

    private HttpClient ClientFor(Realm realm) => factory.CreateClient(new()
    {
        AllowAutoRedirect = false,
        HandleCookies = false,
        BaseAddress = new Uri($"https://{realm.Host}")
    });

    // A member session cookie as the host itself would issue it for someone signed in to this realm.
    private async Task<string> MemberCookieFor(Realm realm) => (await IssueSessionFor(realm)).Cookie;

    private Task<MemberSessionCookie.Issued> IssueSessionFor(Realm realm, DateTimeOffset? expires = null, string? refreshToken = null)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", realm.Authority),
            new Claim("aud", realm.ClientId),
            new Claim("sub", "member-1"),
            new Claim("oid", "member-1"),
            new Claim("name", "Alex Ju"),
        ], "PrismMemberCookie"));

        return MemberSessionCookie.IssueAsync(factory.Services, principal, expires ?? DateTimeOffset.UtcNow.AddHours(1), refreshToken);
    }

    private async Task<HttpStatusCode> RevokeAsync(Realm onHost, string cookie)
    {
        TenantId(onHost);
        using var client = ClientFor(onHost);
        using var request = new HttpRequestMessage(HttpMethod.Delete, RevokePath);
        request.Headers.Add("Cookie", cookie);
        return (await client.SendAsync(request)).StatusCode;
    }

    [Theory]
    [InlineData("A", "B")]
    [InlineData("B", "A")]
    public async Task A_member_session_is_accepted_on_its_own_tenant_and_refused_on_the_other(string own, string other)
    {
        var (ownRealm, otherRealm) = (own == "A" ? A : B, other == "A" ? A : B);
        var cookie = await MemberCookieFor(ownRealm);

        (await RevokeAsync(ownRealm, cookie)).Should().Be(HttpStatusCode.NoContent,
            "the session is valid on the tenant it was issued for, so the endpoint's own answer is reachable");
        (await RevokeAsync(otherRealm, cookie)).Should().BeOneOf(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Redirect],
            "the same cookie presented on another tenant's hostname is not that tenant's member");
    }

    [Fact]
    public async Task The_cookie_carries_a_reference_not_the_tokens()
    {
        var hugeRefreshToken = new string('r', 8000);

        var session = await IssueSessionFor(A, refreshToken: hugeRefreshToken);

        session.Cookie.Length.Should().BeLessThan(1000, "an 8 KB refresh token stays on the server and only a reference travels");
        session.Cookie.Should().StartWith("__Host-prism.sid=");
        (await RevokeAsync(A, session.Cookie)).Should().Be(HttpStatusCode.NoContent, "the reference resolves to the stored ticket");
    }

    [Fact]
    public async Task Deleting_the_stored_session_signs_the_member_out_at_once()
    {
        var session = await IssueSessionFor(A);
        (await RevokeAsync(A, session.Cookie)).Should().Be(HttpStatusCode.NoContent);

        await factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get("PrismMemberCookie").SessionStore!.RemoveAsync(session.SessionKey);

        (await RevokeAsync(A, session.Cookie)).Should().BeOneOf(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Redirect],
            "the cookie is still well formed, but the server no longer has the session it points at");
    }

    [Fact]
    public async Task An_expired_stored_session_is_refused_although_the_cookie_is_well_formed()
    {
        var session = await IssueSessionFor(A, expires: DateTimeOffset.UtcNow.AddHours(1));
        var databaseFactory = factory.Services.GetRequiredService<IUmbracoDatabaseFactory>();
        using (var db = databaseFactory.CreateDatabase())
        {
            db.Execute("UPDATE prismSessions SET ExpiresAt = @0 WHERE SessionKey = @1", DateTime.UtcNow.AddMinutes(-1), session.SessionKey);
        }

        (await RevokeAsync(A, session.Cookie)).Should().BeOneOf(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Redirect]);
    }

    [Fact]
    public async Task A_biometric_token_for_one_tenant_is_refused_on_the_other_by_name()
    {
        var token = factory.Services.GetRequiredService<IBiometricTokenService>()
            .IssueToken("device-1", TenantId(A).ToString(System.Globalization.CultureInfo.InvariantCulture), "member-1", TimeSpan.FromDays(1));

        var onB = await ExchangeAsync(B, token);
        onB.Status.Should().Be(HttpStatusCode.Unauthorized);
        onB.Body.Should().Contain("tenant_mismatch");

        var onA = await ExchangeAsync(A, token);
        onA.Body.Should().NotContain("tenant_mismatch",
            "on its own tenant the token passes the tenant check and is judged on its credential row instead");
    }

    private async Task<(HttpStatusCode Status, string Body)> ExchangeAsync(Realm onHost, string token)
    {
        TenantId(onHost);
        using var client = ClientFor(onHost);
        var response = await client.PostAsJsonAsync(ExchangePath, new { biometricToken = token });
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
