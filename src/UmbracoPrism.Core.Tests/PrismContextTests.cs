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

        var principal = CreatePrincipalWithTenant("tenant-a");
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var vault = new Mock<ISecretVaultService>();
        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object)
        {
            CurrentTenant = new PrismTenant
            {
                EntraTenantId = "tenant-a",
                EntraClientId = "client-a",
                SecretKeyName = "secret-a"
            }
        };

        var header = await prismContext.GetAuthorizationHeaderAsync();

        header.Should().NotBeNull();
        header!.Scheme.Should().Be("Bearer");
        header.Parameter.Should().Be("access-token");
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_ReturnsNull_WhenPrincipalTenantDoesNotMatchCurrentTenant()
    {
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = "access-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(10).ToString("o") }
        });

        var principal = CreatePrincipalWithTenant("tenant-a");
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var vault = new Mock<ISecretVaultService>();
        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object)
        {
            CurrentTenant = new PrismTenant
            {
                EntraTenantId = "tenant-b",
                EntraClientId = "client-b",
                SecretKeyName = "secret-b"
            }
        };

        var header = await prismContext.GetAuthorizationHeaderAsync();

        header.Should().BeNull();
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_DoesNotRefresh_WhenPrincipalTenantDoesNotMatchCurrentTenant()
    {
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = "expired-access-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o") }
        });

        var principal = CreatePrincipalWithTenant("tenant-a");
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var vault = new Mock<ISecretVaultService>();
        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object)
        {
            CurrentTenant = new PrismTenant
            {
                EntraTenantId = "tenant-b",
                EntraClientId = "client-b",
                SecretKeyName = "secret-b"
            }
        };

        var header = await prismContext.GetAuthorizationHeaderAsync();

        header.Should().BeNull();
        tokenRefreshService.Verify(
            t => t.RefreshAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_PassesRequestAbortedToken_ToRefreshService()
    {
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = "expired-access-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o") }
        });

        var principal = CreatePrincipalWithTenant("tenant-a");
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var cancellation = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.RequestAborted = cancellation.Token;

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var vault = new Mock<ISecretVaultService>();
        vault.Setup(v => v.GetSecretAsync("secret-a")).ReturnsAsync("secret-value");

        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        tokenRefreshService
            .Setup(t => t.RefreshAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                cancellation.Token))
            .ReturnsAsync(new TokenRefreshResult(true, "new-access-token", "new-refresh-token", 3600));

        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object)
        {
            CurrentTenant = new PrismTenant
            {
                EntraTenantId = "tenant-a",
                EntraClientId = "client-a",
                SecretKeyName = "secret-a"
            }
        };

        var header = await prismContext.GetAuthorizationHeaderAsync();

        header.Should().NotBeNull();
        header!.Parameter.Should().Be("new-access-token");
        tokenRefreshService.Verify(
            t => t.RefreshAsync(
                "https://tenant-a.ciamlogin.com/tenant-a/oauth2/v2.0/token",
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                cancellation.Token),
            Times.Once);
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_ReturnsBearer_WhenGenericOidcPrincipalMatchesCurrentTenant()
    {
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = "access-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(10).ToString("o") }
        });

        var principal = CreatePrincipalForGenericOidc("https://localhost:8443/realms/prism-dev", "prism-client");
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var vault = new Mock<ISecretVaultService>();
        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object)
        {
            CurrentTenant = new PrismTenant
            {
                OidcAuthority = "https://localhost:8443/realms/prism-dev",
                OidcClientId = "prism-client"
            }
        };

        var header = await prismContext.GetAuthorizationHeaderAsync();

        header.Should().NotBeNull();
        header!.Scheme.Should().Be("Bearer");
        header.Parameter.Should().Be("access-token");
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_ReturnsNull_WhenGenericOidcPrincipalDoesNotMatchCurrentTenant()
    {
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = "access-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(10).ToString("o") }
        });

        var principal = CreatePrincipalForGenericOidc("https://localhost:8443/realms/other", "prism-client");
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var vault = new Mock<ISecretVaultService>();
        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object)
        {
            CurrentTenant = new PrismTenant
            {
                OidcAuthority = "https://localhost:8443/realms/prism-dev",
                OidcClientId = "prism-client"
            }
        };

        var header = await prismContext.GetAuthorizationHeaderAsync();

        header.Should().BeNull();
    }

    private static ClaimsPrincipal CreatePrincipalWithTenant(string tenantId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("tid", tenantId));
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreatePrincipalForGenericOidc(string issuer, string clientId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("iss", issuer));
        identity.AddClaim(new Claim("aud", clientId));
        return new ClaimsPrincipal(identity);
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
