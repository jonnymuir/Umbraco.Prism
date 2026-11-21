using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using UmbracoPrism.TestSite;
using UmbracoPrism.TestSite.Services;
using UmbracoPrism.TestSite.Services.ServiceDesign;
using Wayfinder.Engine.Models;
using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// A signed-in member's requests and registered devices belong to their stable subject, never to
/// their email: an address can change or be reissued to someone else, and the request must stay
/// with the person who made it.
/// </summary>
public class PrismSubjectIdentityTests
{
    private static readonly PrismTenant Tenant = new() { Id = 1, Name = "Keycloak", Hostname = "localhost", OidcAuthority = "https://idp.example.test/realms/prism" };

    private static Claim[] Member(string email, string sub = "member-sub-1") =>
    [
        new("iss", "https://idp.example.test/realms/prism"),
        new("sub", sub),
        new("email", email),
        new("email_verified", "true"),
        new("name", "Alex Ju"),
    ];

    private static (PrismUserContext User, DefaultHttpContext Http) SignedIn(params Claim[] claims)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "PrismMemberCookie"));
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), "PrismMemberCookie"))
            .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(principal, "PrismMemberCookie")));

        var prism = new Mock<IPrismContext>();
        prism.SetupGet(c => c.CurrentTenant).Returns(Tenant);

        var http = new DefaultHttpContext { User = principal };
        var accessor = new HttpContextAccessor { HttpContext = http };
        var user = new PrismUserContext(accessor, prism.Object);
        http.RequestServices = new ServiceCollection()
            .AddSingleton(authentication.Object)
            .AddSingleton<IPrismUserContext>(user)
            .BuildServiceProvider();
        return (user, http);
    }

    [Fact]
    public void TheSubject_IsTheIssuerAndSubject_AndSurvivesAnEmailChange()
    {
        // Read each subject straight away: the accessor's HttpContext is shared ambient state.
        var before = SignedIn(Member("alex@old.example.test")).User.Subject;
        var after = SignedIn(Member("alex@new.example.test")).User.Subject;

        before.Should().Be("https://idp.example.test/realms/prism|member-sub-1");
        after.Should().Be(before);
    }

    [Fact]
    public void TheSubject_IsNotTheEmail_WhenTwoPeopleShareAnAddressOverTime()
    {
        var first = SignedIn(Member("shared@example.test", sub: "person-a")).User.Subject;
        var second = SignedIn(Member("shared@example.test", sub: "person-b")).User.Subject;

        first.Should().NotBe(second);
    }

    [Fact]
    public void ARequestIsOwnedByTheSubject_NotTheEmail()
    {
        var (user, http) = SignedIn(Member("alex@example.test"));
        var resolver = new PublicVisitorIdentityResolver(user, new HttpContextAccessor { HttpContext = http });

        var (tenantId, userId, isAuthenticated) = resolver.Resolve();

        isAuthenticated.Should().BeTrue();
        tenantId.Should().Be("localhost");
        userId.Should().Be("https://idp.example.test/realms/prism|member-sub-1");
        userId.Should().NotContain("alex@example.test");
    }

    [Fact]
    public void AMemberWithNoSubject_IsNotGivenTheirEmailAsAnIdentity()
    {
        var (user, http) = SignedIn(new Claim("email", "alex@example.test"), new Claim("email_verified", "true"));
        var resolver = new PublicVisitorIdentityResolver(user, new HttpContextAccessor { HttpContext = http });

        var (_, userId, isAuthenticated) = resolver.Resolve();

        isAuthenticated.Should().BeFalse("a request must never be owned by an address");
        userId.Should().NotContain("alex@example.test");
    }

    [Fact]
    public void TheDemoSystemsOfRecordAreStillLookedUpByTheVerifiedEmail()
    {
        var (_, http) = SignedIn(Member("demo@prism.local"));
        var resolver = new MemberServiceInputsResolver(
            new JugglingSocietyMembershipClient(),
            Mock.Of<IMemberSavingsRecordService>(),
            new HttpContextAccessor { HttpContext = http });

        var inputs = resolver.Resolve(
            new ServiceRequest { InstanceId = "r1", UserId = "https://idp.example.test/realms/prism|member-sub-1" },
            new ServiceBlueprint { DefinitionKey = TestSiteSeedContract.JugglingLicenceBlueprintSlug },
            new StageDefinition());

        var member = (Dictionary<string, object?>)inputs!["member"]!;
        member["email"].Should().Be("demo@prism.local");
        member["tier"].Should().Be("Competitive");
    }

    [Fact]
    public void WithNoLiveRequest_ThereIsNoEmailToSuggest()
    {
        var resolver = new MemberServiceInputsResolver(
            new JugglingSocietyMembershipClient(),
            Mock.Of<IMemberSavingsRecordService>(),
            new HttpContextAccessor());

        var inputs = resolver.Resolve(
            new ServiceRequest { InstanceId = "r1", UserId = "https://idp.example.test/realms/prism|member-sub-1" },
            new ServiceBlueprint { DefinitionKey = TestSiteSeedContract.JugglingLicenceBlueprintSlug },
            new StageDefinition());

        var member = (Dictionary<string, object?>)inputs!["member"]!;
        member["email"].Should().BeNull();
        member["tier"].Should().Be(string.Empty);
    }
}
