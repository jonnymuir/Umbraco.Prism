using System.Net;
using AwesomeAssertions;
using Moq;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Services;
using static UmbracoPrism.Core.Tests.ServiceDesign.Runtime.MemberJourneyHarness;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Runtime;

/// <summary>
/// "Register as a member" is the reference for the step after a person creates an account at the
/// identity provider: asking the business app, as them, to create their membership. These tests drive
/// the real engine with the real client over a stub business app, and pin down what an implementor
/// relies on: the person's own bearer token is what registers them, the form never supplies who they
/// are or where they belong, and a refusal comes back to the person with a reason they can act on.
/// </summary>
public class RegisterAsAMemberBlueprintTests
{
    private const string DefinitionKey = "register-as-a-member";

    static RegisterAsAMemberBlueprintTests() => TestSupportSystems.EnsureRegistered();

    [Fact]
    public void Definition_PassesAuthoringValidation()
    {
        var authoringService = new ServiceBlueprintAuthoringService(new Mock<IServiceBlueprintSourceStore>().Object);

        var outcome = authoringService.Validate(LoadDefinition(DefinitionKey));

        outcome.Diagnostics.Should().BeEmpty(string.Join("; ", outcome.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}")));
        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ANewcomer_IsAskedForTheirDetails_ButOnlyAfterTheBusinessAppHasBeenAskedAsThem()
    {
        var business = NotYetAMember();
        var journey = new Journey(DefinitionKey, business);

        var page = Begin(journey);

        page.StateDisplayName.Should().Be("Your details");
        business.Requests.Should().ContainSingle(r => r.Method == HttpMethod.Get)
            .Which.Authorization.Should().Be($"Bearer {MemberToken}");
    }

    [Fact]
    public void ARegistration_IsSentWithTheNewcomersOwnToken_AndOnlyTheDetailsTheyTypedIn()
    {
        var business = NotYetAMember();
        business.RegisterResponse = Reply(HttpStatusCode.Created, new
        {
            registered = true, created = true, tenant = "Prism Demo (Keycloak)", tenantCode = "PRISM-DEMO", backOfficeId = "MBR-1A2B3C4D", role = "Member",
        });
        var journey = new Journey(DefinitionKey, business);
        Begin(journey);

        var welcome = SubmitDetails(journey, name: "Robin Reed", phone: "01632 960 001", preference: "phone");

        var post = business.Requests.Single(r => r.Method == HttpMethod.Post);
        post.Authorization.Should().Be($"Bearer {MemberToken}", "the membership must be created as the person, not as the host");
        post.Body.Should().Contain("Robin Reed").And.Contain("01632 960 001").And.Contain("phone");
        post.Body.Should().NotContain("tenant", "which tenant they join comes from the token, never from the form")
            .And.NotContain("role").And.NotContain("email");
        welcome.StateDisplayName.Should().Be("Welcome, you are a member");
        welcome.Text.Should().Contain("MBR-1A2B3C4D").And.Contain("Member").And.Contain("Prism Demo (Keycloak)");
    }

    [Fact]
    public void SomeoneWhoIsAlreadyAMember_IsToldSo_AndIsNotAskedToRegisterAgain()
    {
        var business = new StubBusinessApp
        {
            ProfileResponse = Reply(HttpStatusCode.OK, new
            {
                registered = true, tenant = "Prism Demo (Keycloak)", tenantCode = "PRISM-DEMO", name = "demo",
                email = "demo@prism.local", role = "Admin", phone = "", contactPreference = "email",
            }),
        };
        var journey = new Journey(DefinitionKey, business);

        var page = Begin(journey);

        page.StateDisplayName.Should().Be("You are already a member");
        business.Requests.Should().NotContain(r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public void ARefusal_ReturnsThePersonToTheFormWithTheReason_AndNothingIsConfirmed()
    {
        var business = NotYetAMember();
        business.RegisterResponse = Reply(HttpStatusCode.Forbidden, new { error = "email-not-verified", message = "Verify your email address, then try again." });
        var journey = new Journey(DefinitionKey, business);
        Begin(journey);

        var page = SubmitDetails(journey, name: "Robin Reed", phone: "", preference: "email");

        page.StateDisplayName.Should().Be("We could not register you");
        page.Text.Should().Contain("Verify your email address, then try again.");
    }

    [Fact]
    public void WhenNoBearerTokenCanBeReleased_NothingIsSentToTheBusinessApp()
    {
        var business = new StubBusinessApp();
        var journey = new Journey(DefinitionKey, business, bearerToken: null);

        try { Begin(journey); } catch (Exception) { /* the stage failing to start is acceptable; sending anonymously is not */ }

        business.Requests.Should().BeEmpty("a request made without the person's token could be answered for nobody, or for the wrong person");
    }

    [Fact]
    public void TheBearerToken_NeverAppearsInAnythingThePersonIsShown()
    {
        var business = NotYetAMember();
        business.RegisterResponse = Reply(HttpStatusCode.Created, new { tenant = "Prism Demo (Keycloak)", backOfficeId = "MBR-1", role = "Member" });
        var journey = new Journey(DefinitionKey, business);
        var seen = new List<string> { Begin(journey).Text };

        seen.Add(SubmitDetails(journey, name: "Robin Reed", phone: "", preference: "email").Text);

        seen.Should().OnlyContain(text => !text.Contains(MemberToken));
    }

    private static StubBusinessApp NotYetAMember() => new()
    {
        ProfileResponse = Reply(HttpStatusCode.OK, new { registered = false, tenant = "Prism Demo (Keycloak)" }),
    };

    private static Page Begin(Journey journey)
    {
        journey.Start();
        return journey.Advance("continue");
    }

    private static Page SubmitDetails(Journey journey, string name, string phone, string preference)
    {
        journey.Advance("continue", new Dictionary<string, object?>
        {
            ["memberName"] = name, ["phone"] = phone, ["contactPreference"] = preference,
        });
        return journey.Advance("submit");
    }
}
