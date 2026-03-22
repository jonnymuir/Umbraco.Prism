using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismContextTests
{
    [Fact]
    public async Task GetAuthorizationHeaderAsync_ReturnsNull_WhenHttpContextMissing()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var vault = new Mock<ISecretVaultService>();
        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        var context = new PrismContext(accessor, vault.Object, tokenRefreshService.Object);

        var header = await context.GetAuthorizationHeaderAsync();

        header.Should().BeNull();
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_ReturnsBearer_WhenAccessTokenValid()
    {
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = "access-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(10).ToString("o") }
        });

        var principal = new ClaimsPrincipal(new ClaimsIdentity("Test"));
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var vault = new Mock<ISecretVaultService>();
        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object);

        var header = await prismContext.GetAuthorizationHeaderAsync();

        header.Should().NotBeNull();
        header!.Scheme.Should().Be("Bearer");
        header.Parameter.Should().Be("access-token");
    }

    private sealed class TestAuthenticationService(AuthenticateResult result) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(result);

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }
}
