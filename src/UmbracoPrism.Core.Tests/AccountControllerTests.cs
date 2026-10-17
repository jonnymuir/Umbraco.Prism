using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Tests;

public class AccountControllerTests
{
    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    [InlineData("http://phishing.example.com/steal-tokens")]
    [InlineData("javascript:alert('xss')")]
    public async Task Login_NormalizesExternalReturnUrl_BeforeChallenge(string maliciousReturnUrl)
    {
        var controller = BuildController(isMember: false);

        var result = (await controller.Login(maliciousReturnUrl)).Should().BeOfType<ChallengeResult>().Subject;

        result.Properties?.RedirectUri.Should().Be("/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Login_NormalizesBlankReturnUrl_BeforeChallenge(string? returnUrl)
    {
        var controller = BuildController(isMember: false);

        var result = (await controller.Login(returnUrl!)).Should().BeOfType<ChallengeResult>().Subject;

        result.Properties?.RedirectUri.Should().Be("/");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    [InlineData("/content/page")]
    [InlineData("~/dashboard")]
    public async Task Login_PreservesSafeLocalReturnUrl_BeforeChallenge(string safeReturnUrl)
    {
        var controller = BuildController(isMember: false);

        var result = (await controller.Login(safeReturnUrl)).Should().BeOfType<ChallengeResult>().Subject;

        result.Properties?.RedirectUri.Should().Be(safeReturnUrl);
    }

    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    public async Task Login_FallsBackToRoot_ForAuthenticatedUsers_WhenReturnUrlIsExternal(string maliciousReturnUrl)
    {
        var controller = BuildController(isMember: true);

        var result = (await controller.Login(maliciousReturnUrl)).Should().BeOfType<LocalRedirectResult>().Subject;

        result.Url.Should().Be("/");
    }

    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    public async Task Register_NormalizesExternalReturnUrl_BeforeChallenge(string maliciousReturnUrl)
    {
        var controller = BuildController(isMember: false);

        var result = (await controller.Register(maliciousReturnUrl)).Should().BeOfType<ChallengeResult>().Subject;

        result.Properties?.RedirectUri.Should().Be("/");
        result.Properties?.Items.Should().ContainKey("PrismPrompt").WhoseValue.Should().Be("create");
    }

    private static readonly PrismTenant SigningInTenant = new()
    {
        Hostname = "members.example.test",
        OidcAuthority = "https://idp.example.test/realms/members",
        OidcClientId = "members",
    };

    /// <summary>
    /// A controller on <paramref name="tenant"/>'s hostname (a signing-in tenant by default).
    /// <paramref name="isMember"/> is whether Prism's own cookie holds a member session;
    /// <paramref name="hostSession"/> puts a host-application login in HttpContext.User, which is
    /// what the application's default scheme yields on a hostname without Prism sign-in.
    /// </summary>
    private static AccountController BuildController(
        bool isMember, PrismTenant? tenant = null, bool useDefaultTenant = true, bool hostSession = false)
    {
        var prismContext = new Mock<IPrismContext>();
        prismContext.SetupGet(c => c.CurrentTenant).Returns(useDefaultTenant ? tenant ?? SigningInTenant : tenant);

        var member = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "PrismMemberCookie"));
        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), "PrismMemberCookie"))
            .ReturnsAsync(isMember
                ? AuthenticateResult.Success(new AuthenticationTicket(member, "PrismMemberCookie"))
                : AuthenticateResult.NoResult());

        var services = new ServiceCollection()
            .AddSingleton(authentication.Object)
            .BuildServiceProvider();

        var controller = new AccountController(NullLogger<AccountController>.Instance, prismContext.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services,
                    User = hostSession
                        ? new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "admin@host.example")], "HostCookie"))
                        : new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        // Lets a test add claims to the member session after building.
        controller.ControllerContext.HttpContext.Items["member"] = member;
        return controller;
    }

    private static ClaimsIdentity MemberIdentity(AccountController controller) =>
        (ClaimsIdentity)((ClaimsPrincipal)controller.ControllerContext.HttpContext.Items["member"]!).Identity!;

    // ── Prism sign-in exists only where a tenant has configured it ─────────

    public static TheoryData<string, PrismTenant?> HostnamesWithoutSignIn() => new()
    {
        { "no tenant for this hostname", null },
        { "a tenant with no identity provider", new PrismTenant { Hostname = "plain.example.test" } },
        { "a generic OIDC authority without a client ID", new PrismTenant { Hostname = "half.example.test", OidcAuthority = "https://idp.example.test" } },
        { "an Entra tenant ID without a client ID", new PrismTenant { Hostname = "half.example.test", EntraTenantId = "00000000-0000-0000-0000-000000000001" } },
    };

    [Theory]
    [MemberData(nameof(HostnamesWithoutSignIn))]
    public async Task Login_IsNotFound_AndNeverChallenges_OnAHostnameWithoutSignIn(string because, PrismTenant? tenant)
    {
        var controller = BuildController(isMember: false, tenant, useDefaultTenant: false);

        (await controller.Login("/")).Should().BeOfType<NotFoundResult>(because);
    }

    [Theory]
    [MemberData(nameof(HostnamesWithoutSignIn))]
    public async Task Register_IsNotFound_AndNeverChallenges_OnAHostnameWithoutSignIn(string because, PrismTenant? tenant)
    {
        var controller = BuildController(isMember: false, tenant, useDefaultTenant: false);

        (await controller.Register("/")).Should().BeOfType<NotFoundResult>(because);
    }

    [Fact]
    public async Task Login_TreatsAHostApplicationSession_AsNotSignedIn()
    {
        // HttpContext.User holds the host's own login; Prism's cookie holds nothing. That is not
        // a Prism member, so the visitor is sent to the tenant's identity provider, not waved on.
        var controller = BuildController(isMember: false, hostSession: true);

        (await controller.Login("/dashboard")).Should().BeOfType<ChallengeResult>()
            .Which.AuthenticationSchemes.Should().Equal("PrismEntraID");
    }

    [Theory]
    [MemberData(nameof(HostnamesWithoutSignIn))]
    public async Task Logout_SignsOutOfTheLocalCookieOnly_OnAHostnameWithoutSignIn(string because, PrismTenant? tenant)
    {
        var controller = BuildController(isMember: true, tenant, useDefaultTenant: false);

        (await controller.Logout()).Should().BeOfType<SignOutResult>()
            .Which.AuthenticationSchemes.Should().BeEquivalentTo(["PrismMemberCookie"], because);
    }

    // ── SEC-PT2-003 regression: logout must be POST-only + antiforgery ─────

    [Fact]
    public void Logout_HasHttpPostAttribute()
    {
        var method = typeof(AccountController).GetMethod(nameof(AccountController.Logout));

        method.Should().NotBeNull();
        method!.GetCustomAttributes<HttpPostAttribute>().Should().HaveCount(1,
            "logout must be POST-only to prevent logout-CSRF via GET (SEC-PT2-003)");
    }

    [Fact]
    public void Logout_HasValidateAntiForgeryTokenAttribute()
    {
        var method = typeof(AccountController).GetMethod(nameof(AccountController.Logout));

        method.Should().NotBeNull();
        method!.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>().Should().HaveCount(1,
            "logout must validate the antiforgery token to prevent logout-CSRF (SEC-PT2-003)");
    }

    [Fact]
    public void Logout_DoesNotHaveHttpGetAttribute()
    {
        var method = typeof(AccountController).GetMethod(nameof(AccountController.Logout));

        method.Should().NotBeNull();
        method!.GetCustomAttributes<HttpGetAttribute>().Should().BeEmpty(
            "logout must not accept GET — any GET-based logout is CSRF-able (SEC-PT2-003)");
    }

    // ── Skip the Entra redirect for a session that never went through it ───

    [Fact]
    public async Task Logout_SignsOutOfBothSchemes_ForAnOrdinaryInteractiveSession()
    {
        var controller = BuildController(isMember: true);

        var result = (await controller.Logout()).Should().BeOfType<SignOutResult>().Subject;

        result.AuthenticationSchemes.Should().BeEquivalentTo("PrismMemberCookie", "PrismEntraID");
    }

    [Fact]
    public async Task Logout_SignsOutOfTheLocalCookieOnly_ForABiometricSession()
    {
        // A biometric session was established entirely via BiometricController's own backchannel
        // refresh_token grant — "PrismEntraID" never ran its interactive OIDC challenge in this
        // WebView, so it has no session there for Entra to end and no id_token_hint to offer.
        // Signing out of it anyway just shows Entra's own account-chooser for nothing (see
        // BiometricController's own remarks on the "prism_auth_method" claim this checks).
        var controller = BuildController(isMember: true);
        MemberIdentity(controller).AddClaim(new Claim("prism_auth_method", "biometric"));

        var result = (await controller.Logout()).Should().BeOfType<SignOutResult>().Subject;

        result.AuthenticationSchemes.Should().BeEquivalentTo("PrismMemberCookie");
    }
}
