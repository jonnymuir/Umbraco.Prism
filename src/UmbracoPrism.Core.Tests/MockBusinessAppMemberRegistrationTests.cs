using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Security regression checks for registering a membership after creating an account at the identity
/// provider. The token alone says who is registering and where; a provider that has not verified the
/// email, a tenant that has not opened registration, and a login name chosen to look like someone
/// else's email must all fail to produce or claim a membership. Mounted through the public
/// <c>MapMembers()</c> and <c>MapProfile()</c> seams, as in <see cref="MockBusinessAppProfileEndpointsTests"/>.
/// </summary>
public sealed class MockBusinessAppMemberRegistrationTests : IAsyncDisposable
{
    private const string Members = "/api/backoffice/members";
    private const string Profile = "/api/backoffice/profile";

    private readonly BusinessAppTestHost _host = new();

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private static object Details(string name = "Robin Reed", string phone = "01632 960 001", string preference = "phone") =>
        new { name, phone, contactPreference = preference };

    private HttpClient Newcomer(string subject = "sub-robin", string email = "robin@example.test", bool verified = true, string? username = null) =>
        _host.AsOidc(BusinessAppTestHost.OpenIssuer, new OidcPerson(subject, email, verified, username));

    [Fact]
    public async Task ARequestWithNoCredentials_CannotRegister()
    {
        using var client = _host.Anonymous();

        (await client.PostAsJsonAsync(Members, Details())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AVerifiedNewcomer_BecomesAMember_AndThenSeesTheirOwnRecord()
    {
        using var robin = Newcomer();

        var response = await robin.PostAsJsonAsync(Members, Details());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonObject>();
        created!["tenantCode"]!.GetValue<string>().Should().Be("OPEN-IDP");
        created["role"]!.GetValue<string>().Should().Be("Member");

        var profile = (await robin.GetFromJsonAsync<JsonObject>(Profile))!;
        profile["registered"]!.GetValue<bool>().Should().BeTrue();
        profile["name"]!.GetValue<string>().Should().Be("Robin Reed");
        profile["email"]!.GetValue<string>().Should().Be("robin@example.test");
        profile["phone"]!.GetValue<string>().Should().Be("01632 960 001");
        profile["contactPreference"]!.GetValue<string>().Should().Be("phone");
        profile["backOfficeId"]?.ToString().Should().BeNull("the profile read does not need to expose it");
    }

    [Fact]
    public async Task Registering_IsSafeToRepeat_AndNeverOverwritesWhatIsAlreadyHeld()
    {
        using var robin = Newcomer();
        var first = await (await robin.PostAsJsonAsync(Members, Details())).Content.ReadFromJsonAsync<JsonObject>();

        var again = await robin.PostAsJsonAsync(Members, Details(name: "Someone Else", phone: "", preference: "post"));

        again.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await again.Content.ReadFromJsonAsync<JsonObject>();
        second!["created"]!.GetValue<bool>().Should().BeFalse();
        second["backOfficeId"]!.GetValue<string>().Should().Be(first!["backOfficeId"]!.GetValue<string>());
        var profile = (await robin.GetFromJsonAsync<JsonObject>(Profile))!;
        profile["name"]!.GetValue<string>().Should().Be("Robin Reed");
        profile["contactPreference"]!.GetValue<string>().Should().Be("phone");
    }

    [Fact]
    public async Task AnEmailTheProviderHasNotVerified_CannotRegister_AndNothingIsCreated()
    {
        using var robin = Newcomer(verified: false);

        var response = await robin.PostAsJsonAsync(Members, Details());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>().Should().Be("email-not-verified");
        (await robin.GetFromJsonAsync<JsonObject>(Profile))!["registered"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ATenantThatHasNotOpenedRegistration_RefusesIt()
    {
        using var robin = _host.AsOidc(BusinessAppTestHost.ClosedIssuer, new OidcPerson("sub-robin", "robin@example.test"));

        var response = await robin.PostAsJsonAsync(Members, Details());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>().Should().Be("self-registration-closed");
    }

    [Fact]
    public async Task AnEntraTenant_CannotBeSelfRegisteredInto()
    {
        using var stranger = _host.As("stranger@example.test", BusinessAppTestHost.AlphaTenant);

        (await stranger.PostAsJsonAsync(Members, Details())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheBody_CannotChooseTheTenant_TheRole_OrTheMembershipId()
    {
        using var robin = Newcomer();

        var response = await robin.PostAsJsonAsync(Members, new JsonObject
        {
            ["name"] = "Robin Reed", ["phone"] = "", ["contactPreference"] = "email",
            ["tenantCode"] = "ALPHA-CORP", ["role"] = "Admin", ["backOfficeId"] = "A-1", ["email"] = "pat@example.test",
        });

        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        body["tenantCode"]!.GetValue<string>().Should().Be("OPEN-IDP");
        body["role"]!.GetValue<string>().Should().Be("Member");
        body["backOfficeId"]!.GetValue<string>().Should().NotBe("A-1");
        (await robin.GetFromJsonAsync<JsonObject>(Profile))!["email"]!.GetValue<string>().Should().Be("robin@example.test");
    }

    [Theory]
    [InlineData("", "01632 960 001", "phone")]
    [InlineData("Robin Reed", "not a number", "phone")]
    [InlineData("Robin Reed", "", "phone")]
    [InlineData("Robin Reed", "", "carrier-pigeon")]
    public async Task InvalidDetails_AreRejected_AndNoMembershipIsCreated(string name, string phone, string preference)
    {
        using var robin = Newcomer();

        var response = await robin.PostAsJsonAsync(Members, Details(name, phone, preference));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await robin.GetFromJsonAsync<JsonObject>(Profile))!["registered"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ALoginName_ChosenToLookLikeAnotherMembersEmail_GrantsNothing()
    {
        // The account's login name is typed in at registration. The business app must read the email
        // claim, not the login name, or this person would be matched to the pre-provisioned member.
        using var impostor = Newcomer(subject: "sub-impostor", email: "impostor@example.test", username: "lee@example.test");

        (await impostor.GetFromJsonAsync<JsonObject>(Profile))!["registered"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task AnUnverifiedClaimToAPreProvisionedMembersEmail_GrantsNothing_ButTheVerifiedOwnerIsRecognised()
    {
        using var impostor = Newcomer(subject: "sub-impostor", email: "lee@example.test", verified: false);
        using var lee = Newcomer(subject: "sub-lee", email: "lee@example.test", verified: true);

        (await impostor.GetFromJsonAsync<JsonObject>(Profile))!["registered"]!.GetValue<bool>().Should().BeFalse();
        var real = (await lee.GetFromJsonAsync<JsonObject>(Profile))!;
        real["registered"]!.GetValue<bool>().Should().BeTrue();
        real["role"]!.GetValue<string>().Should().Be("Admin");
        (await impostor.PutAsJsonAsync(Profile, new { phone = "", contactPreference = "email" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "an unverified claim to a member's email must not let anyone edit their record");
    }

    [Fact]
    public async Task APreProvisionedMember_IsToldTheyAreAlreadyRegistered_NotGivenASecondMembership()
    {
        using var lee = Newcomer(subject: "sub-lee", email: "lee@example.test");

        var response = await lee.PostAsJsonAsync(Members, Details());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        body["created"]!.GetValue<bool>().Should().BeFalse();
        body["backOfficeId"]!.GetValue<string>().Should().Be("O-1");
    }

    [Fact]
    public async Task TwoPeopleRegistering_GetSeparateRecords_EvenIfOneLaterTakesTheOthersOldEmail()
    {
        using var first = Newcomer(subject: "sub-one", email: "shared@example.test");
        await first.PostAsJsonAsync(Members, Details(name: "First Person", phone: "01632 960 111", preference: "phone"));

        using var second = Newcomer(subject: "sub-two", email: "shared@example.test");
        await second.PostAsJsonAsync(Members, Details(name: "Second Person", phone: "", preference: "email"));

        (await first.GetFromJsonAsync<JsonObject>(Profile))!["phone"]!.GetValue<string>().Should().Be("01632 960 111");
        var other = (await second.GetFromJsonAsync<JsonObject>(Profile))!;
        other["name"]!.GetValue<string>().Should().Be("Second Person");
        other["phone"]!.GetValue<string>().Should().BeEmpty();
    }
}
