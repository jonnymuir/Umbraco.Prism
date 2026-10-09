using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

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
    private readonly BusinessAppTestHost _host = new();

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task ARequestWithNoCredentials_IsRefused_OnBothReadAndWrite()
    {
        using var client = _host.Anonymous();

        (await client.GetAsync("/api/backoffice/profile")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsJsonAsync("/api/backoffice/profile", new { phone = "", contactPreference = "email" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ThePersonRegisteredInTwoTenants_HasTwoIndependentRecords_AndEachTokenSeesOnlyItsOwn()
    {
        using var alpha = _host.As("pat@example.test", BusinessAppTestHost.AlphaTenant);
        using var beta = _host.As("pat@example.test", BusinessAppTestHost.BetaTenant);

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
        using var pat = _host.As("pat@example.test", BusinessAppTestHost.AlphaTenant);
        using var sam = _host.As("sam@example.test", BusinessAppTestHost.AlphaTenant);

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
        using var stranger = _host.As("stranger@example.test", BusinessAppTestHost.AlphaTenant);

        var read = await ReadProfileAsync(stranger);
        read["registered"]!.GetValue<bool>().Should().BeFalse();

        (await stranger.PutAsJsonAsync("/api/backoffice/profile", new { phone = "", contactPreference = "email" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a non-member must not be able to create a record");
    }

    [Fact]
    public async Task ATokenFromAnUnknownTenant_IsRefused_OnBothReadAndWrite()
    {
        // Token validation already rejects an untrusted issuer, so this is a token that names a tenant the
        // app does not know: refuse it rather than answer "not a member", which would imply it was understood.
        using var unknownTenant = _host.As("pat@example.test", "00000000-0000-0000-0000-0000000000ff");

        (await unknownTenant.GetAsync("/api/backoffice/profile")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await unknownTenant.PutAsJsonAsync("/api/backoffice/profile", new { phone = "", contactPreference = "email" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("not a number", "email", "telephone number")]
    [InlineData("01632 960 001", "carrier-pigeon", "how we should contact")]
    [InlineData("", "phone", "telephone number if you want")]
    public async Task InvalidDetails_AreRefusedWithAReasonTheMemberCanActOn_AndNothingIsSaved(string phone, string preference, string reasonFragment)
    {
        using var pat = _host.As("pat@example.test", BusinessAppTestHost.AlphaTenant);

        var response = await pat.PutAsJsonAsync("/api/backoffice/profile", new { phone, contactPreference = preference });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>().Should().Contain(reasonFragment);
        (await ReadProfileAsync(pat))["phone"]!.GetValue<string>().Should().BeEmpty();
    }

    private static async Task<JsonObject> ReadProfileAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/backoffice/profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }
}
