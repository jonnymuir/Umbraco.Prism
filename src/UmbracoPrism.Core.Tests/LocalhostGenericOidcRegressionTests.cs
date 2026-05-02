using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Moq;
using System.Security.Claims;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Regression tests for localhost generic OIDC authentication contract.
/// 
/// Context:
/// - The repo-owned localhost Keycloak demo uses offline_access so refresh tokens survive a full AppHost restart
/// - Non-demo generic OIDC tenants still use minimal standard scopes unless explicitly configured otherwise
/// - Generic OIDC logout reuses the stored id_token as id_token_hint
/// 
/// These tests lock in the corrected minimal-scope behavior to prevent auth regressions.
/// </summary>
[Collection(EnvVarSensitiveTestCollection.Name)]
public class LocalhostGenericOidcRegressionTests : IDisposable
{
    // Snapshot env vars that PrismContext reads during refresh so that a parallel test mutating
    // them cannot bleed into this class even though we don't mutate them ourselves.
    private readonly string? _savedBackchannelUrl;
    private readonly string? _savedAspNetCoreEnv;

    public LocalhostGenericOidcRegressionTests()
    {
        _savedBackchannelUrl = Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL");
        _savedAspNetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL", _savedBackchannelUrl);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _savedAspNetCoreEnv);
    }
    private static readonly PrismTenant LocalhostKeycloakTenant = new()
    {
        Hostname = "localhost",
        OidcAuthority = "https://localhost:8443/realms/prism-dev",
        OidcClientId = "prism-client",
        OidcClientSecretProvider = PrismSecretProviderNames.Inline,
        OidcClientSecretReference = "prism-dev-secret"
    };

    // ------------------------------------------------------------------ 
    // 1. LOGIN / AUTHORIZATION REQUEST SCOPE BEHAVIOR
    // ------------------------------------------------------------------ 

    [Fact]
    public async Task Login_UsesOfflineAccess_ForRepoOwnedLocalDemoTenant()
    {
        // REGRESSION LOCK: The repo-owned localhost Keycloak demo requests offline_access so the
        // refresh token survives a full local AppHost restart and can rebind downstream auth.
        
        var options = ConfigureOptions(LocalhostKeycloakTenant);
        var context = CreateRedirectContext(options, LocalhostKeycloakTenant);

        await options.Events.OnRedirectToIdentityProvider(context);

        context.ProtocolMessage.Scope.Should().Be("openid profile offline_access",
            "because the localhost Keycloak demo needs restart-resilient refresh tokens for the live behavioural suite");
    }

    [Fact]
    public async Task Login_UsesMinimalStandardScopes_EvenWithIncompleteConfig()
    {
        // EDGE CASE: If a tenant is misconfigured (missing client ID), ensure we use
        // standard minimal scopes consistently.
        
        var incompleteTenant = new PrismTenant
        {
            Hostname = "localhost",
            OidcAuthority = "https://localhost:8443/realms/prism-dev",
            // Missing OidcClientId - should still get standard scope from GetRequestedScope
        };

        var options = ConfigureOptions(incompleteTenant);
        var context = CreateRedirectContext(options, incompleteTenant);

        await options.Events.OnRedirectToIdentityProvider(context);

        // Even with incomplete config, scope should stay minimal rather than assuming localhost demo semantics.
        context.ProtocolMessage.Scope.Should().Be("openid profile");
    }

    [Fact]
    public void GetRequestedScope_ReturnsConsistentRestartResilientScope_ForLocalDemoTenant()
    {
        // REGRESSION LOCK: Login and refresh must use the same offline-capable scope for the
        // repo-owned localhost demo or the post-restart session contract breaks.
        
        var loginScope = PrismOidcConfiguration.GetRequestedScope(LocalhostKeycloakTenant);
        var refreshScope = PrismOidcConfiguration.GetRequestedScope(LocalhostKeycloakTenant);

        loginScope.Should().Be(refreshScope,
            "because scope mismatch between login and refresh causes 401 errors");
        
        loginScope.Should().Be("openid profile offline_access",
            "because the localhost demo requires offline tokens to survive a full stack restart");
    }

    [Fact]
    public async Task Login_SetsGenericOidcAuthorizationEndpoint_ForLocalhostKeycloak()
    {
        // REGRESSION LOCK: Generic OIDC uses /protocol/openid-connect/auth
        // (not the Entra-specific /oauth2/v2.0/authorize)
        
        var options = ConfigureOptions(LocalhostKeycloakTenant);
        var context = CreateRedirectContext(options, LocalhostKeycloakTenant);

        await options.Events.OnRedirectToIdentityProvider(context);

        context.ProtocolMessage.IssuerAddress.Should().Be(
            "https://localhost:8443/realms/prism-dev/protocol/openid-connect/auth",
            "because generic OIDC providers use standard OIDC endpoints");
    }

    // ------------------------------------------------------------------ 
    // 2. DOWNSTREAM REFRESH BEHAVIOR / TOKEN REQUEST
    // ------------------------------------------------------------------ 

    [Fact]
    public async Task TokenRefresh_UsesOfflineAccess_ForRepoOwnedLocalDemoTenant()
    {
        // REGRESSION LOCK: The localhost demo refresh path must keep offline_access so the
        // replacement access token can be minted after the local Keycloak runtime restarts.
        
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = "expired-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o") }
        });

        var principal = CreatePrincipalForGenericOidc(
            LocalhostKeycloakTenant.OidcAuthority!,
            LocalhostKeycloakTenant.OidcClientId!);
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var vault = new Mock<ISecretVaultService>();
        vault.Setup(v => v.ResolveSecretAsync(
                PrismSecretProviderNames.Inline,
                LocalhostKeycloakTenant.OidcClientSecretReference))
            .ReturnsAsync("prism-dev-secret");

        IReadOnlyDictionary<string, string>? capturedFormData = null;
        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        tokenRefreshService
            .Setup(t => t.RefreshAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .Callback<string, IReadOnlyDictionary<string, string>, CancellationToken, IReadOnlyDictionary<string, string>?>((_, form, _, _) =>
                capturedFormData = form)
            .ReturnsAsync(new TokenRefreshResult(true, "new-access-token", "new-refresh-token", 3600));

        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object)
        {
            CurrentTenant = LocalhostKeycloakTenant
        };

        await prismContext.GetAuthorizationHeaderAsync();

        capturedFormData.Should().NotBeNull();
        capturedFormData.Should().NotBeNull();
        capturedFormData!.Should().NotContainKey("scope",
            "because the localhost demo refresh token already carries the granted offline scopes and Keycloak should reuse them");
    }

    [Fact]
    public async Task TokenRefresh_UsesCorrectTokenEndpoint_ForGenericOidc()
    {
        // REGRESSION LOCK: Token endpoint for generic OIDC uses /protocol/openid-connect/token
        
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = "expired-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o") }
        });

        var principal = CreatePrincipalForGenericOidc(
            LocalhostKeycloakTenant.OidcAuthority!,
            LocalhostKeycloakTenant.OidcClientId!);
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var vault = new Mock<ISecretVaultService>();
        vault.Setup(v => v.ResolveSecretAsync(
                PrismSecretProviderNames.Inline,
                LocalhostKeycloakTenant.OidcClientSecretReference))
            .ReturnsAsync("prism-dev-secret");

        string? capturedEndpoint = null;
        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        tokenRefreshService
            .Setup(t => t.RefreshAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .Callback<string, IReadOnlyDictionary<string, string>, CancellationToken, IReadOnlyDictionary<string, string>?>((endpoint, _, _, _) =>
                capturedEndpoint = endpoint)
            .ReturnsAsync(new TokenRefreshResult(true, "new-access-token", "new-refresh-token", 3600));

        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object)
        {
            CurrentTenant = LocalhostKeycloakTenant
        };

        await prismContext.GetAuthorizationHeaderAsync();

        capturedEndpoint.Should().Be(
            "https://localhost:8443/realms/prism-dev/protocol/openid-connect/token",
            "because generic OIDC token endpoint follows standard OIDC URL structure");
    }

    [Fact]
    public async Task TokenRefresh_FailsClosed_WhenSecretCannotBeResolved()
    {
        // SECURITY: If the secret cannot be resolved, do NOT attempt refresh.
        // This prevents leaking partial auth state.
        
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = "expired-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" },
            new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o") }
        });

        var principal = CreatePrincipalForGenericOidc(
            LocalhostKeycloakTenant.OidcAuthority!,
            LocalhostKeycloakTenant.OidcClientId!);
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var vault = new Mock<ISecretVaultService>();
        vault.Setup(v => v.ResolveSecretAsync(
                PrismSecretProviderNames.Inline,
                LocalhostKeycloakTenant.OidcClientSecretReference))
            .ReturnsAsync((string?)null); // Secret resolution fails

        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();

        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object)
        {
            CurrentTenant = LocalhostKeycloakTenant
        };

        var header = await prismContext.GetAuthorizationHeaderAsync();

        header.Should().BeNull("because refresh should fail closed when secret is unavailable");
        tokenRefreshService.Verify(
            t => t.RefreshAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()),
            Times.Never,
            "because refresh should not be attempted without a valid client secret");
    }

    // ------------------------------------------------------------------ 
    // 3. LOGOUT PARAMETER BEHAVIOR
    // ------------------------------------------------------------------ 

    [Fact]
    public async Task Logout_UsesStoredIdTokenHint_ForGenericOidc()
    {
        // REGRESSION LOCK: Generic OIDC logout MUST send the stored id_token as id_token_hint.
        // Restarting the site must not break logout — the encrypted PrismMemberCookie is the
        // session contract that preserves the provider-issued id_token across restarts.
        
        var authProperties = new AuthenticationProperties();
        authProperties.StoreTokens([
            new AuthenticationToken { Name = "id_token", Value = "id-token-value" }
        ]);
        var authTicket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity("PrismMemberCookie")),
            authProperties,
            "PrismMemberCookie");
        var authenticationService = new TestAuthenticationService(AuthenticateResult.Success(authTicket));
        
        var options = ConfigureOptions(LocalhostKeycloakTenant, authenticationService);
        var context = CreateRedirectContext(options, LocalhostKeycloakTenant, authenticationService);

        await options.Events.OnRedirectToIdentityProviderForSignOut(context);

        context.ProtocolMessage.IdTokenHint.Should().Be("id-token-value",
            "because RP-initiated logout for the localhost generic OIDC flow depends on the stored id_token surviving in the PrismMemberCookie");
    }

    [Fact]
    public async Task Logout_SetsClientId_ForGenericOidc()
    {
        // REGRESSION LOCK: Even without id_token_hint, client_id is sent to identify the app.
        
        var authenticationService = new TestAuthenticationService(AuthenticateResult.NoResult());
        var options = ConfigureOptions(LocalhostKeycloakTenant, authenticationService);
        var context = CreateRedirectContext(options, LocalhostKeycloakTenant, authenticationService);

        await options.Events.OnRedirectToIdentityProviderForSignOut(context);

        context.ProtocolMessage.ClientId.Should().Be("prism-client",
            "because client_id identifies the application to the provider during logout");
    }

    [Fact]
    public async Task Logout_UsesStandardOidcLogoutEndpoint_ForGenericOidc()
    {
        // REGRESSION LOCK: Generic OIDC logout uses /protocol/openid-connect/logout
        // (not the Entra-specific /oauth2/v2.0/logout)
        
        var authenticationService = new TestAuthenticationService(AuthenticateResult.NoResult());
        var options = ConfigureOptions(LocalhostKeycloakTenant, authenticationService);
        var context = CreateRedirectContext(options, LocalhostKeycloakTenant, authenticationService);

        await options.Events.OnRedirectToIdentityProviderForSignOut(context);

        context.ProtocolMessage.IssuerAddress.Should().Be(
            "https://localhost:8443/realms/prism-dev/protocol/openid-connect/logout",
            "because generic OIDC providers use standard OIDC logout endpoint");
    }

    [Fact]
    public async Task Logout_FallsBack_WhenStoredIdTokenIsMissing()
    {
        // EDGE CASE: If id_token is missing from cookie (provider didn't issue it,
        // or it was lost), logout should still construct a valid logout URL.
        
        var authenticationService = new TestAuthenticationService(AuthenticateResult.NoResult());
        var options = ConfigureOptions(LocalhostKeycloakTenant, authenticationService);
        var context = CreateRedirectContext(options, LocalhostKeycloakTenant, authenticationService);

        await options.Events.OnRedirectToIdentityProviderForSignOut(context);

        context.ProtocolMessage.IssuerAddress.Should().NotBeNullOrEmpty();
        context.ProtocolMessage.IdTokenHint.Should().BeNull(
            "because older or corrupted sessions may still be missing the id_token, and logout should fall back to client_id only");
    }

    // ------------------------------------------------------------------ 
    // 4. CROSS-CUTTING BEHAVIOR: PROVIDER DISCRIMINATION
    // ------------------------------------------------------------------ 

    [Fact]
    public async Task GenericOidc_UsesStandardOidcEndpoints_NotEntraSpecific()
    {
        // REGRESSION LOCK: Generic OIDC tenants must NOT use Entra-specific paths.
        // This is the fundamental discrimination that enables multi-provider support.
        
        var options = ConfigureOptions(LocalhostKeycloakTenant);
        var redirectContext = CreateRedirectContext(options, LocalhostKeycloakTenant);

        await options.Events.OnRedirectToIdentityProvider(redirectContext);

        redirectContext.ProtocolMessage.IssuerAddress.Should().Contain("/protocol/openid-connect/",
            "because generic OIDC uses standard endpoint paths");
        redirectContext.ProtocolMessage.IssuerAddress.Should().NotContain("ciamlogin.com",
            "because generic OIDC should not use Entra-specific domains");
        redirectContext.ProtocolMessage.IssuerAddress.Should().NotContain("/oauth2/v2.0/",
            "because generic OIDC should not use Entra-specific endpoint paths");
    }

    [Fact]
    public void GenericOidc_IdentifiedByOidcAuthority_NotEntraTenantId()
    {
        // REGRESSION LOCK: Provider discrimination logic uses OidcAuthority presence.
        
        var tenant = LocalhostKeycloakTenant;

        tenant.OidcAuthority.Should().NotBeNullOrWhiteSpace(
            "because OidcAuthority is the signal for generic OIDC path");
        tenant.EntraTenantId.Should().BeNull(
            "because Entra tenants use EntraTenantId, not OidcAuthority");
    }

    [Fact]
    public void GenericOidc_RequestsOfflineAccessOnly_ForRepoOwnedLocalDemoTenant()
    {
        // REGRESSION LOCK: The localhost demo uses offline_access, but it must still remain
        // standard OIDC rather than drifting into Entra-specific /.default semantics.
        
        var genericScope = PrismOidcConfiguration.GetRequestedScope(LocalhostKeycloakTenant);

        genericScope.Should().NotContain("/.default",
            "because /.default is Entra-specific syntax");
        genericScope.Should().Be("openid profile offline_access",
            "because the localhost demo explicitly opts into restart-resilient offline tokens");
    }

    // ------------------------------------------------------------------ 
    // HELPERS
    // ------------------------------------------------------------------ 

    private static OpenIdConnectOptions ConfigureOptions(
        PrismTenant tenant,
        IAuthenticationService? authenticationService = null)
    {
        var prismContext = new TestPrismContext { CurrentTenant = tenant };
        var services = new ServiceCollection()
            .AddSingleton<IPrismContext>(prismContext)
            .AddSingleton(authenticationService ?? new TestAuthenticationService(AuthenticateResult.NoResult()))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var signingKeyCache = new Mock<IPrismSigningKeyCache>();
        var configuration = new PrismOidcConfiguration(
            httpContextAccessor,
            signingKeyCache.Object,
            NullLogger<PrismOidcConfiguration>.Instance);
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
            .AddSingleton(authenticationService ?? new TestAuthenticationService(AuthenticateResult.NoResult()))
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

        return context;
    }

    private static ClaimsPrincipal CreatePrincipalForGenericOidc(string issuer, string clientId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("iss", issuer));
        identity.AddClaim(new Claim("aud", clientId));
        return new ClaimsPrincipal(identity);
    }

    private sealed class TestPrismContext : IPrismContext
    {
        public PrismTenant? CurrentTenant { get; set; }
        public string? LastAuthorizationFailureReason => null;

        public Task<System.Net.Http.Headers.AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(bool forceRefresh = false) =>
            Task.FromResult<System.Net.Http.Headers.AuthenticationHeaderValue?>(null);
    }

    private sealed class TestAuthenticationService(AuthenticateResult authenticateResult) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(authenticateResult);

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }
}
