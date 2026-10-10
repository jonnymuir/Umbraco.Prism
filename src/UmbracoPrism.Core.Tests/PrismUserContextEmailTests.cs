using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using UmbracoPrism.TestSite.Services.ServiceDesign;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Security regression checks for who Prism believes a signed-in person is. The email decides which
/// service requests are theirs and whether they are on a team roster, so at a tenant with its own
/// identity provider (where people register themselves) it must be an email the provider has
/// verified, never the login name a person chose, which can be set to someone else's address.
/// </summary>
public class PrismUserContextEmailTests
{
    private const string Roster = "njf-caseworker@prism.local";

    private static PrismUserContext ContextFor(PrismTenant? tenant, params Claim[] claims)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
        var prism = new Mock<IPrismContext>();
        prism.SetupGet(c => c.CurrentTenant).Returns(tenant);
        return new PrismUserContext(new HttpContextAccessor { HttpContext = http }, prism.Object);
    }

    private static readonly PrismTenant OwnIdentityProvider = new() { OidcAuthority = "https://idp.example/realms/prod" };
    private static readonly PrismTenant ManagedEntra = new() { EntraTenantId = "00000000-0000-0000-0000-000000000009" };

    [Fact]
    public void AtATenantWithItsOwnProvider_AVerifiedEmailIsTheIdentity()
    {
        var user = ContextFor(OwnIdentityProvider,
            new Claim("email", "robin@example.test"), new Claim("email_verified", "true"));

        user.Email.Should().Be("robin@example.test");
    }

    [Fact]
    public void AtATenantWithItsOwnProvider_ALoginNameChosenToLookLikeARosterEmail_IsNotTheIdentity()
    {
        var user = ContextFor(OwnIdentityProvider,
            new Claim("preferred_username", Roster),
            new Claim("email", "attacker@example.test"), new Claim("email_verified", "true"));

        user.Email.Should().Be("attacker@example.test");
        NjfContributionsTeam.IsMember(user.Email).Should().BeFalse("choosing a login name must not put anyone on the caseworker roster");
    }

    [Fact]
    public void AtATenantWithItsOwnProvider_AnEmailTheProviderHasNotVerified_IsNotAnIdentity()
    {
        var unverified = ContextFor(OwnIdentityProvider,
            new Claim("email", Roster), new Claim("email_verified", "false"));
        var unstated = ContextFor(OwnIdentityProvider, new Claim("email", Roster));

        unverified.Email.Should().BeNull();
        unstated.Email.Should().BeNull("a provider that does not say the email is verified has not vouched for it");
    }

    [Fact]
    public void AtATenantWithItsOwnProvider_ALoginNameAlone_IsNotAnIdentity()
    {
        var user = ContextFor(OwnIdentityProvider, new Claim("preferred_username", Roster));

        user.Email.Should().BeNull();
    }

    [Fact]
    public void TheEmailClaimIsRecognisedUnderItsMappedName()
    {
        var user = ContextFor(OwnIdentityProvider,
            new Claim(ClaimTypes.Email, "robin@example.test"), new Claim("email_verified", "true"));

        user.Email.Should().Be("robin@example.test");
    }

    [Fact]
    public void AtAManagedEntraDirectory_TheSignInNameIsStillTheIdentity()
    {
        var user = ContextFor(ManagedEntra, new Claim("preferred_username", "pat@contoso.test"));

        user.Email.Should().Be("pat@contoso.test", "an Entra directory issues sign-in names, people do not choose them");
    }
}
