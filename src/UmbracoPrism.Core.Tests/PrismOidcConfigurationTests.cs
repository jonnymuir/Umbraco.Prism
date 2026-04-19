using FluentAssertions;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismOidcConfigurationTests
{
    [Fact]
    public void GetRequestedScope_ReturnsStandardOidcScopes_ForGenericProvider()
    {
        var tenant = new PrismTenant
        {
            OidcAuthority = "https://localhost:8443/realms/prism-dev",
            OidcClientId = "prism-client"
        };

        PrismOidcConfiguration.GetRequestedScope(tenant).Should().Be("openid profile");
    }

    [Fact]
    public void GetRequestedScope_RetainsOfflineAccess_ForEntraTenants()
    {
        var tenant = new PrismTenant
        {
            EntraTenantId = "tenant-a",
            EntraClientId = "client-a"
        };

        PrismOidcConfiguration.GetRequestedScope(tenant)
            .Should()
            .Be("openid profile offline_access client-a/.default");
    }

    [Fact]
    public async Task ResolveClientSecretAsync_UsesInlineSecret_ForRepoOwnedLocalDemoTenant()
    {
        var tenant = new PrismTenant
        {
            Hostname = "localhost",
            OidcAuthority = "https://localhost:8443/realms/prism-dev",
            OidcClientId = "prism-client",
            OidcClientSecretProvider = PrismSecretProviderNames.Inline,
            OidcClientSecretReference = "prism-dev-secret"
        };
        var vault = new Mock<ISecretVaultService>();
        vault.Setup(service => service.ResolveSecretAsync(PrismSecretProviderNames.Inline, "prism-dev-secret"))
            .ReturnsAsync("prism-dev-secret");

        var secret = await PrismOidcConfiguration.ResolveClientSecretAsync(tenant, vault.Object);

        secret.Should().Be("prism-dev-secret");
        vault.Verify(service => service.ResolveSecretAsync(PrismSecretProviderNames.Inline, "prism-dev-secret"), Times.Once);
    }

    [Fact]
    public async Task ResolveClientSecretAsync_UsesInlineSecret_ForCodespacesRepoDemoTenant()
    {
        // DemoTenantSeeder seeds a second tenant for Codespaces whose hostname and
        // Keycloak authority both use the *.app.github.dev domain. The inline secret
        // is the same well-known demo credential committed in this repo.
        var tenant = new PrismTenant
        {
            Hostname = "turbo-space-giggle-xrjwx5649xcpx9w-44345.app.github.dev",
            OidcAuthority = "https://turbo-space-giggle-xrjwx5649xcpx9w-8443.app.github.dev/realms/prism-dev",
            OidcClientId = "prism-client",
            OidcClientSecretProvider = PrismSecretProviderNames.Inline,
            OidcClientSecretReference = "prism-dev-secret"
        };
        var vault = new Mock<ISecretVaultService>();
        vault.Setup(service => service.ResolveSecretAsync(PrismSecretProviderNames.Inline, "prism-dev-secret"))
            .ReturnsAsync("prism-dev-secret");

        var secret = await PrismOidcConfiguration.ResolveClientSecretAsync(tenant, vault.Object);

        secret.Should().Be("prism-dev-secret");
        vault.Verify(service => service.ResolveSecretAsync(PrismSecretProviderNames.Inline, "prism-dev-secret"), Times.Once);
    }

    [Fact]
    public async Task ResolveClientSecretAsync_UsesVaultReference_ForGenericOidcTenantsOutsideLocalDemoPath()
    {
        var tenant = new PrismTenant
        {
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal",
            OidcClientSecretProvider = PrismSecretProviderNames.AzureKeyVault,
            OidcClientSecretReference = "northwind-oidc-secret"
        };
        var vault = new Mock<ISecretVaultService>();
        vault.Setup(service => service.ResolveSecretAsync(PrismSecretProviderNames.AzureKeyVault, "northwind-oidc-secret"))
            .ReturnsAsync("vault-backed-secret");

        var secret = await PrismOidcConfiguration.ResolveClientSecretAsync(tenant, vault.Object);

        secret.Should().Be("vault-backed-secret");
        vault.Verify(service => service.ResolveSecretAsync(PrismSecretProviderNames.AzureKeyVault, "northwind-oidc-secret"), Times.Once);
    }

    [Fact]
    public async Task ResolveClientSecretAsync_FailsClosed_ForInlineSecretsOutsideRepoOwnedLocalDemoPath()
    {
        var tenant = new PrismTenant
        {
            Hostname = "northwind.example",
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal",
            OidcClientSecretProvider = PrismSecretProviderNames.Inline,
            OidcClientSecretReference = "should-not-resolve"
        };
        var vault = new Mock<ISecretVaultService>();

        var secret = await PrismOidcConfiguration.ResolveClientSecretAsync(tenant, vault.Object);

        secret.Should().BeEmpty();
        vault.Verify(service => service.ResolveSecretAsync(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ResolveClientSecretAsync_FailsClosed_ForGenericOidcTenantsWithoutSecretReference()
    {
        var tenant = new PrismTenant
        {
            OidcAuthority = "https://auth.example.com/realms/northwind",
            OidcClientId = "northwind-portal"
        };
        var vault = new Mock<ISecretVaultService>();

        var secret = await PrismOidcConfiguration.ResolveClientSecretAsync(tenant, vault.Object);

        secret.Should().BeEmpty();
        vault.Verify(service => service.ResolveSecretAsync(It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task PostConfigure_TriggersBackgroundRefreshWithoutBlocking_WhenKidIsMissingFromCache()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signingKeyCache = new Mock<IPrismSigningKeyCache>();

        signingKeyCache
            .Setup(cache => cache.GetSnapshot("tenant-a", "new-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([CreateSigningKey("old-key")], true, false, false));

        signingKeyCache
            .Setup(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                refreshStarted.TrySetResult();
                await refreshRelease.Task;
            });

        var options = ConfigureOptions(signingKeyCache.Object, new PrismTenant
        {
            EntraTenantId = "tenant-a",
            EntraClientId = "client-a"
        });

        var resolvedKeys = options.TokenValidationParameters.IssuerSigningKeyResolver!(
                "token",
                new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(),
                "new-key",
                options.TokenValidationParameters)
            .ToArray();

        resolvedKeys.Should().BeEmpty();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        signingKeyCache.Verify(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()), Times.Once);

        refreshRelease.TrySetResult();
    }

    [Fact]
    public async Task PostConfigure_TriggersBackgroundRefreshWithoutBlocking_WhenCacheIsCold()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signingKeyCache = new Mock<IPrismSigningKeyCache>();

        signingKeyCache
            .Setup(cache => cache.GetSnapshot("tenant-a", "new-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([], true, true, false));

        signingKeyCache
            .Setup(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                refreshStarted.TrySetResult();
                await refreshRelease.Task;
            });

        var options = ConfigureOptions(signingKeyCache.Object, new PrismTenant
        {
            EntraTenantId = "tenant-a",
            EntraClientId = "client-a"
        });

        var resolvedKeys = options.TokenValidationParameters.IssuerSigningKeyResolver!(
                "token",
                new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(),
                "new-key",
                options.TokenValidationParameters)
            .ToArray();

        resolvedKeys.Should().BeEmpty();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        signingKeyCache.Verify(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()), Times.Once);

        refreshRelease.TrySetResult();
    }

    [Fact]
    public void PostConfigure_DoesNotRefresh_WhenCachedKeyAlreadyMatchesKid()
    {
        var cachedKey = CreateSigningKey("current-key");
        var signingKeyCache = new Mock<IPrismSigningKeyCache>();

        signingKeyCache
            .Setup(cache => cache.GetSnapshot("tenant-a", "current-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([cachedKey], false, false, true));

        var options = ConfigureOptions(signingKeyCache.Object, new PrismTenant
        {
            EntraTenantId = "tenant-a",
            EntraClientId = "client-a"
        });

        var resolvedKeys = options.TokenValidationParameters.IssuerSigningKeyResolver!(
                "token",
                new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(),
                "current-key",
                options.TokenValidationParameters)
            .ToArray();

        resolvedKeys.Should().ContainSingle().Which.KeyId.Should().Be("current-key");
        signingKeyCache.Verify(cache => cache.WarmAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PostConfigure_TriggersBackgroundRefreshWithoutBlocking_WhenCachedKeysAreExpired()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signingKeyCache = new Mock<IPrismSigningKeyCache>();

        signingKeyCache
            .Setup(cache => cache.GetSnapshot("tenant-a", "current-key"))
            .Returns(new PrismSigningKeyCacheSnapshot([CreateSigningKey("current-key")], true, true, true));

        signingKeyCache
            .Setup(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                refreshStarted.TrySetResult();
                await refreshRelease.Task;
            });

        var options = ConfigureOptions(signingKeyCache.Object, new PrismTenant
        {
            EntraTenantId = "tenant-a",
            EntraClientId = "client-a"
        });

        var resolvedKeys = options.TokenValidationParameters.IssuerSigningKeyResolver!(
                "token",
                new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(),
                "current-key",
                options.TokenValidationParameters)
            .ToArray();

        resolvedKeys.Should().BeEmpty();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        signingKeyCache.Verify(cache => cache.WarmAsync("tenant-a", true, It.IsAny<CancellationToken>()), Times.Once);

        refreshRelease.TrySetResult();
    }

    [Fact]
    public async Task PostConfigure_GenericOidcRedirect_RequestsStandardOidcScopesOnly()
    {
        var tenant = new PrismTenant
        {
            OidcAuthority = "https://localhost:8443/realms/prism-dev",
            OidcClientId = "prism-client"
        };
        var options = ConfigureOptions(new Mock<IPrismSigningKeyCache>().Object, tenant);

        var context = CreateRedirectContext(options, tenant);

        await options.Events.OnRedirectToIdentityProvider(context);

        context.ProtocolMessage.Scope.Should().Be("openid profile");
    }

    [Fact]
    public void CreateAuthenticationTokens_PersistsIdToken_ForLaterLogout()
    {
        using var payload = JsonDocument.Parse("""
            {
              "access_token": "access-token",
              "id_token": "id-token",
              "refresh_token": "refresh-token",
              "expires_in": 300
            }
            """);

        var tokens = PrismOidcConfiguration.CreateAuthenticationTokens(
            payload.RootElement,
            new DateTimeOffset(2026, 4, 12, 0, 0, 0, TimeSpan.Zero));

        tokens.Should().Contain(t => t.Name == "id_token" && t.Value == "id-token");
    }

    [Fact]
    public async Task PostConfigure_GenericOidcLogout_RestoresIdTokenHint_FromCookieTokens()
    {
        var tenant = new PrismTenant
        {
            OidcAuthority = "https://localhost:8443/realms/prism-dev",
            OidcClientId = "prism-client"
        };

        var authProperties = new AuthenticationProperties();
        authProperties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = "id-token" }]);
        var authTicket = new AuthenticationTicket(
            new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("PrismMemberCookie")),
            authProperties,
            "PrismMemberCookie");
        var authenticationService = new StubAuthenticationService(AuthenticateResult.Success(authTicket));
        var options = ConfigureOptions(new Mock<IPrismSigningKeyCache>().Object, tenant, authenticationService);

        var context = CreateRedirectContext(options, tenant, authenticationService);

        await options.Events.OnRedirectToIdentityProviderForSignOut(context);

        context.ProtocolMessage.IssuerAddress.Should().Be("https://localhost:8443/realms/prism-dev/protocol/openid-connect/logout");
        context.ProtocolMessage.ClientId.Should().Be("prism-client");
        context.ProtocolMessage.IdTokenHint.Should().Be("id-token");
    }

    [Fact]
    public async Task PostConfigure_GenericOidcLogout_FallsBackToClientId_WhenIdTokenMissing()
    {
        var tenant = new PrismTenant
        {
            OidcAuthority = "https://localhost:8443/realms/prism-dev",
            OidcClientId = "prism-client"
        };

        // No id_token stored in cookie (common if provider doesn't issue it or it was lost)
        var authenticationService = new StubAuthenticationService(AuthenticateResult.NoResult());
        var options = ConfigureOptions(new Mock<IPrismSigningKeyCache>().Object, tenant, authenticationService);

        var context = CreateRedirectContext(options, tenant, authenticationService);

        await options.Events.OnRedirectToIdentityProviderForSignOut(context);

        context.ProtocolMessage.IssuerAddress.Should().Be("https://localhost:8443/realms/prism-dev/protocol/openid-connect/logout");
        context.ProtocolMessage.ClientId.Should().Be("prism-client");
        context.ProtocolMessage.IdTokenHint.Should().BeNull();
    }

    private static OpenIdConnectOptions ConfigureOptions(
        IPrismSigningKeyCache signingKeyCache,
        PrismTenant tenant,
        IAuthenticationService? authenticationService = null)
    {
        var prismContext = new TestPrismContext { CurrentTenant = tenant };
        var services = new ServiceCollection()
            .AddSingleton<IPrismContext>(prismContext)
            .AddSingleton(authenticationService ?? new StubAuthenticationService(AuthenticateResult.NoResult()))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var configuration = new PrismOidcConfiguration(httpContextAccessor, signingKeyCache, NullLogger<PrismOidcConfiguration>.Instance);
        var options = new OpenIdConnectOptions();
        options.Events.OnRedirectToIdentityProvider = _ => Task.CompletedTask;
        options.Events.OnRedirectToIdentityProviderForSignOut = _ => Task.CompletedTask;

        configuration.PostConfigure("PrismEntraID", options);

        return options;
    }

    private static RedirectContext CreateRedirectContext(
        OpenIdConnectOptions options,
        PrismTenant tenant,
        IAuthenticationService? authenticationService = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IPrismContext>(new TestPrismContext { CurrentTenant = tenant })
            .AddSingleton(authenticationService ?? new StubAuthenticationService(AuthenticateResult.NoResult()))
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var scheme = new Microsoft.AspNetCore.Authentication.AuthenticationScheme(
            "PrismEntraID",
            "PrismEntraID",
            typeof(OpenIdConnectHandler));

        var context = new RedirectContext(
            httpContext,
            scheme,
            options,
            new AuthenticationProperties())
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        context.HttpContext.RequestServices = services;

        return context;
    }

    private static SecurityKey CreateSigningKey(string keyId)
    {
        return new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"))
        {
            KeyId = keyId
        };
    }

    private sealed class TestPrismContext : IPrismContext
    {
        public PrismTenant? CurrentTenant { get; set; }
        public string? LastAuthorizationFailureReason => null;

        public Task<System.Net.Http.Headers.AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(bool forceRefresh = false) =>
            Task.FromResult<System.Net.Http.Headers.AuthenticationHeaderValue?>(null);
    }

    private sealed class StubAuthenticationService(AuthenticateResult authenticateResult) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(authenticateResult);

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }
}
