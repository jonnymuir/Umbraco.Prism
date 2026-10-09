using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmbracoPrism.MockBusinessApp.Services.Profile;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Security regression checks for the business-app half of "update my details": who is asking, and
/// for which tenant, comes from the bearer token's claims and nothing else. A caller must not be
/// able to read or write anyone else's record, or the same person's record under another tenant.
/// The endpoints are mounted through their public <c>MapProfile()</c> seam on a small test host
/// with a stand-in authentication scheme that turns two request headers into the claims a
/// validated JWT would carry; real JWT validation is covered by the booted-stack tests.
/// </summary>
public sealed class MockBusinessAppProfileEndpointsTests : IAsyncDisposable
{
    private const string AlphaTenant = "00000000-0000-0000-0000-000000000001";
    private const string BetaTenant = "00000000-0000-0000-0000-000000000003";

    private readonly WebApplication _app;

    public MockBusinessAppProfileEndpointsTests()
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
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderClaimsAuthHandler>("Test", null);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapProfile();
        _app.StartAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ARequestWithNoCredentials_IsRefused_OnBothReadAndWrite()
    {
        using var client = _app.GetTestClient();

        (await client.GetAsync("/api/backoffice/profile")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsJsonAsync("/api/backoffice/profile", new { phone = "", contactPreference = "email" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ThePersonRegisteredInTwoTenants_HasTwoIndependentRecords_AndEachTokenSeesOnlyItsOwn()
    {
        using var alpha = As("pat@example.test", AlphaTenant);
        using var beta = As("pat@example.test", BetaTenant);

        (await alpha.PutAsJsonAsync("/api/backoffice/profile", new { phone = "01632 960 001", contactPreference = "phone" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var seenInAlpha = await ReadProfileAsync(alpha);
        var seenInBeta = await ReadProfileAsync(beta);
        seenInAlpha["phone"]!.GetValue<string>().Should().Be("01632 960 001");
        seenInAlpha["role"]!.GetValue<string>().Should().Be("Admin");
        seenInBeta["phone"]!.GetValue<string>().Should().BeEmpty("a write under one tenant must not be visible under another");
        seenInBeta["role"]!.GetValue<string>().Should().Be("Viewer");
        seenInBeta["tenant"]!.GetValue<string>().Should().Be("Beta Services");
    }

    [Fact]
    public async Task AWriteCannotBeAimedAtAnotherMember_ByNamingThemInTheRequest()
    {
        using var pat = As("pat@example.test", AlphaTenant);
        using var sam = As("sam@example.test", AlphaTenant);

        var aimedAtSam = new JsonObject
        {
            ["phone"] = "01632 960 999", ["contactPreference"] = "phone",
            ["email"] = "sam@example.test", ["tenantCode"] = "ALPHA-CORP",
        };
        (await pat.PutAsJsonAsync("/api/backoffice/profile", aimedAtSam)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadProfileAsync(sam))["phone"]!.GetValue<string>().Should().BeEmpty("the record written is the token holder's, whatever the body names");
        (await ReadProfileAsync(pat))["phone"]!.GetValue<string>().Should().Be("01632 960 999");
    }

    [Fact]
    public async Task ASignedInPersonWhoIsNotAMemberOfTheTenant_IsToldSoOnRead_AndRefusedOnWrite()
    {
        using var stranger = As("stranger@example.test", AlphaTenant);

        var read = await ReadProfileAsync(stranger);
        read["registered"]!.GetValue<bool>().Should().BeFalse();

        (await stranger.PutAsJsonAsync("/api/backoffice/profile", new { phone = "", contactPreference = "email" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a non-member must not be able to create a record");
    }

    [Fact]
    public async Task ATokenFromAnUnknownTenant_GetsNoRecord()
    {
        using var unknownTenant = As("pat@example.test", "00000000-0000-0000-0000-0000000000ff");

        (await unknownTenant.PutAsJsonAsync("/api/backoffice/profile", new { phone = "", contactPreference = "email" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadProfileAsync(unknownTenant))["registered"]!.GetValue<bool>().Should().BeFalse();
    }

    [Theory]
    [InlineData("not a number", "email", "telephone number")]
    [InlineData("01632 960 001", "carrier-pigeon", "how we should contact")]
    [InlineData("", "phone", "telephone number if you want")]
    public async Task InvalidDetails_AreRefusedWithAReasonTheMemberCanActOn_AndNothingIsSaved(string phone, string preference, string reasonFragment)
    {
        using var pat = As("pat@example.test", AlphaTenant);

        var response = await pat.PutAsJsonAsync("/api/backoffice/profile", new { phone, contactPreference = preference });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>().Should().Contain(reasonFragment);
        (await ReadProfileAsync(pat))["phone"]!.GetValue<string>().Should().BeEmpty();
    }

    private HttpClient As(string email, string tenantId)
    {
        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(HeaderClaimsAuthHandler.EmailHeader, email);
        client.DefaultRequestHeaders.Add(HeaderClaimsAuthHandler.TenantHeader, tenantId);
        return client;
    }

    private static async Task<JsonObject> ReadProfileAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/backoffice/profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    /// <summary>Stands in for validated JWT bearer authentication: the claims a real token would carry, taken from headers.</summary>
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
