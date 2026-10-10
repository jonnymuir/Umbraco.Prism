using System.Net;
using AwesomeAssertions;
using Moq;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Services;

using static UmbracoPrism.Core.Tests.ServiceDesign.Runtime.MemberJourneyHarness;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// "Update my details" is the reference for a front-stage journey that calls a business app AS the
/// signed-in member. These tests drive the real engine with the real
/// <see cref="MockBusinessAppProfileClient"/> over a stub HTTP handler, so what they pin down is
/// what an implementor relies on: whose token reaches the business app, what the member sees
/// because of the answer, and that the token goes nowhere else.
/// </summary>
public class UpdateMyDetailsBlueprintTests
{
    private const string DefinitionKey = "update-my-details";

    static UpdateMyDetailsBlueprintTests() => TestSupportSystems.EnsureRegistered();

    [Fact]
    public void Definition_PassesAuthoringValidation()
    {
        var authoringService = new ServiceBlueprintAuthoringService(new Mock<IServiceBlueprintSourceStore>().Object);

        var outcome = authoringService.Validate(LoadDefinition(DefinitionKey));

        outcome.Diagnostics.Should().BeEmpty(string.Join("; ", outcome.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}")));
        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisteredMember_IsLookedUpWithTheirOwnBearerToken_AndSeesTheirRecordPrefilled()
    {
        var business = new StubBusinessApp { ProfileResponse = Reply(HttpStatusCode.OK, new
        {
            registered = true, tenant = "Prism Demo (Keycloak)", tenantCode = "PRISM-DEMO", name = "demo",
            email = "demo@prism.local", role = "Admin", phone = "01632 960 001", contactPreference = "phone",
        }) };
        var journey = NewJourney(business);

        var form = StartAndLookUp(journey);

        business.Requests.Should().ContainSingle();
        business.Requests[0].Method.Should().Be(HttpMethod.Get);
        business.Requests[0].Authorization.Should().Be($"Bearer {MemberToken}", "the business app must be asked as the member, not as the host");
        form.StateDisplayName.Should().Be("Your contact details");
        form.Text.Should().Contain("demo@prism.local").And.Contain("Prism Demo (Keycloak)")
            .And.Contain("01632 960 001", "the member's current phone number prefills the form");
    }

    [Fact]
    public void SignedInPersonWhoIsNotAMemberOfTheTenant_IsToldSo_NotShownAnEditForm()
    {
        var business = new StubBusinessApp { ProfileResponse = Reply(HttpStatusCode.OK, new { registered = false, tenant = "Beta Services" }) };
        var journey = NewJourney(business);

        var page = StartAndLookUp(journey);

        page.StateDisplayName.Should().Be("We could not find your membership");
        page.Text.Should().Contain("Beta Services");
    }

    [Fact]
    public void WhenNoBearerTokenCanBeReleased_NothingIsSentToTheBusinessApp()
    {
        var business = new StubBusinessApp();
        var journey = NewJourney(business, bearerToken: null);

        try { StartAndLookUp(journey); } catch (Exception) { /* the stage failing to start is acceptable; sending anonymously is not */ }

        business.Requests.Should().BeEmpty("a call made without the member's token would be answered for nobody, or for the wrong person");
    }

    [Fact]
    public void AcceptedChange_IsWrittenWithTheMembersToken_AndConfirmedWithTheBusinessAppsReference()
    {
        var business = RegisteredMember();
        business.SaveResponse = Reply(HttpStatusCode.OK, new { reference = "UPD-ABC12345", tenantCode = "PRISM-DEMO" });
        var journey = NewJourney(business);
        StartAndLookUp(journey);

        var confirmation = SubmitDetails(journey, phone: "01632 960 002", preference: "email");

        var write = business.Requests.Single(r => r.Method == HttpMethod.Put);
        write.Authorization.Should().Be($"Bearer {MemberToken}");
        write.Body.Should().Contain("01632 960 002").And.Contain("email");
        confirmation.StateDisplayName.Should().Be("Details updated");
        confirmation.Text.Should().Contain("UPD-ABC12345");
    }

    [Fact]
    public void ChangeTheBusinessAppRefuses_ReturnsTheMemberToTheFormWithTheReason_AndNothingIsConfirmed()
    {
        var business = RegisteredMember();
        business.SaveResponse = Reply(HttpStatusCode.UnprocessableEntity, new { error = "Enter a telephone number in the correct format, like 01632 960 001." });
        var journey = NewJourney(business);
        StartAndLookUp(journey);

        var page = SubmitDetails(journey, phone: "not a number", preference: "phone");

        page.StateDisplayName.Should().Be("We could not update your details");
        page.Text.Should().Contain("Enter a telephone number in the correct format");
    }

    [Fact]
    public void TheBearerToken_NeverAppearsInAnythingTheMemberIsShown()
    {
        var business = RegisteredMember();
        business.SaveResponse = Reply(HttpStatusCode.OK, new { reference = "UPD-ABC12345", tenantCode = "PRISM-DEMO" });
        var journey = NewJourney(business);
        var seen = new List<string> { StartAndLookUp(journey).Text };

        seen.Add(SubmitDetails(journey, phone: "01632 960 002", preference: "email").Text);

        seen.Should().OnlyContain(text => !text.Contains(MemberToken),
            "the token is attached to the outgoing request only, never stored as a field the journey can render");
    }

    private static Journey NewJourney(StubBusinessApp business, string? bearerToken = MemberToken) =>
        new(DefinitionKey, business, bearerToken);

    private static Page StartAndLookUp(Journey journey)
    {
        journey.Start();
        return journey.Advance("continue");
    }

    private static Page SubmitDetails(Journey journey, string phone, string preference)
    {
        journey.Advance("continue", new Dictionary<string, object?> { ["phone"] = phone, ["contactPreference"] = preference });
        return journey.Advance("submit");
    }

    private static StubBusinessApp RegisteredMember() => new()
    {
        ProfileResponse = Reply(HttpStatusCode.OK, new
        {
            registered = true, tenant = "Prism Demo (Keycloak)", tenantCode = "PRISM-DEMO", name = "demo",
            email = "demo@prism.local", role = "Admin", phone = "", contactPreference = "email",
        }),
    };
}
