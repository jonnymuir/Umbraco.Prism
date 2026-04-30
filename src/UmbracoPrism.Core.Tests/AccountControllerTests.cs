using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;
using System.Security.Claims;
using UmbracoPrism.Core.Controllers;

namespace UmbracoPrism.Core.Tests;

public class AccountControllerTests
{
    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    [InlineData("http://phishing.example.com/steal-tokens")]
    [InlineData("javascript:alert('xss')")]
    public void Login_NormalizesExternalReturnUrl_BeforeChallenge(string maliciousReturnUrl)
    {
        var controller = BuildController(isAuthenticated: false);

        var result = controller.Login(maliciousReturnUrl).Should().BeOfType<ChallengeResult>().Subject;

        result.Properties?.RedirectUri.Should().Be("/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Login_NormalizesBlankReturnUrl_BeforeChallenge(string? returnUrl)
    {
        var controller = BuildController(isAuthenticated: false);

        var result = controller.Login(returnUrl!).Should().BeOfType<ChallengeResult>().Subject;

        result.Properties?.RedirectUri.Should().Be("/");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    [InlineData("/content/page")]
    [InlineData("~/dashboard")]
    public void Login_PreservesSafeLocalReturnUrl_BeforeChallenge(string safeReturnUrl)
    {
        var controller = BuildController(isAuthenticated: false);

        var result = controller.Login(safeReturnUrl).Should().BeOfType<ChallengeResult>().Subject;

        result.Properties?.RedirectUri.Should().Be(safeReturnUrl);
    }

    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    public void Login_FallsBackToRoot_ForAuthenticatedUsers_WhenReturnUrlIsExternal(string maliciousReturnUrl)
    {
        var controller = BuildController(isAuthenticated: true);

        var result = controller.Login(maliciousReturnUrl).Should().BeOfType<LocalRedirectResult>().Subject;

        result.Url.Should().Be("/");
    }

    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    public void Register_NormalizesExternalReturnUrl_BeforeChallenge(string maliciousReturnUrl)
    {
        var controller = BuildController(isAuthenticated: false);

        var result = controller.Register(maliciousReturnUrl).Should().BeOfType<ChallengeResult>().Subject;

        result.Properties?.RedirectUri.Should().Be("/");
        result.Properties?.Items.Should().ContainKey("PrismPrompt").WhoseValue.Should().Be("create");
    }

    private static AccountController BuildController(bool isAuthenticated)
    {
        var controller = new AccountController();
        var identity = isAuthenticated
            ? new ClaimsIdentity(authenticationType: "PrismMemberCookie")
            : new ClaimsIdentity();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
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
}
