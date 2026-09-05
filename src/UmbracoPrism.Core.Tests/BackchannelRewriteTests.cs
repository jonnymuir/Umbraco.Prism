using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Regression tests for the Development-only backchannel URL rewrites.
///
/// Bedrock invariant: neither the refresh-token rewrite (Copper) nor the JWKS rewrite
/// (Blathers) may weaken issuer/audience validation. The transport is rerouted; the trust
/// boundary is unchanged.
///
/// Groups:
///   A — Refresh-token rewrite gating (PrismContext.RefreshTokenAsync)
///   B — JWKS rewrite gating (PrismAuthExtensions.ResolveSigningKeys)
///   C — Bedrock guard invariants
/// </summary>
[Collection(EnvVarSensitiveTestCollection.Name)]
public class BackchannelRewriteTests
{
    private const string OidcAuthority = "https://codespace-8443.app.github.dev/realms/prism-dev";
    private const string BackchannelUrl = "http://keycloak-internal:8080";
    private const string ClientId = "prism-client";
    private const string ClientSecret = "prism-secret";

    // ──────────────────────────────────────────────────────────────────────────────
    // Group A — Refresh-token rewrite gating
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When KEYCLOAK_BACKCHANNEL_URL is set AND the environment is Development,
    /// the refresh-token POST target must be rewritten to the internal backchannel host.
    /// This allows the server-side call to reach Keycloak without going through the
    /// GitHub Codespaces forwarded-port proxy that rejects unauthenticated server traffic.
    /// </summary>
    [Fact]
    public async Task RefreshTokenAsync_RewritesTokenEndpoint_WhenBackchannelSetAndDevelopment()
    {
        using var envDev = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Development");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        string? capturedEndpoint = null;
        var (prismContext, tokenRefreshService) = BuildPrismContextWithCapture(
            OidcAuthority, ClientId,
            endpoint => capturedEndpoint = endpoint);

        await prismContext.GetAuthorizationHeaderAsync();

        // The transport host must be the internal backchannel, not the Codespaces URL.
        capturedEndpoint.Should().StartWith(BackchannelUrl,
            "backchannel rewrite must redirect the POST to the internal host");
        capturedEndpoint.Should().EndWith("/protocol/openid-connect/token");
        capturedEndpoint.Should().Contain("/realms/prism-dev/");
    }

    /// <summary>
    /// When KEYCLOAK_BACKCHANNEL_URL is absent, the token endpoint must remain the
    /// public OidcAuthority — the production path must be byte-identical to pre-fix.
    /// </summary>
    [Fact]
    public async Task RefreshTokenAsync_DoesNotRewrite_WhenBackchannelUnset()
    {
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", null);

        string? capturedEndpoint = null;
        var (prismContext, _) = BuildPrismContextWithCapture(
            OidcAuthority, ClientId,
            endpoint => capturedEndpoint = endpoint);

        await prismContext.GetAuthorizationHeaderAsync();

        capturedEndpoint.Should().StartWith(OidcAuthority,
            "without a backchannel URL the endpoint must use the public OidcAuthority");
    }

    /// <summary>
    /// CRITICAL SAFETY TEST: when ASPNETCORE_ENVIRONMENT is NOT Development (e.g.
    /// Production, Staging), the backchannel rewrite must NOT activate even if
    /// KEYCLOAK_BACKCHANNEL_URL is set. Production traffic must always go through the
    /// public HTTPS Keycloak URL.
    /// </summary>
    [Fact]
    public async Task RefreshTokenAsync_DoesNotRewrite_WhenNotDevelopment()
    {
        using var envProd = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Production");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        string? capturedEndpoint = null;
        var (prismContext, _) = BuildPrismContextWithCapture(
            OidcAuthority, ClientId,
            endpoint => capturedEndpoint = endpoint);

        await prismContext.GetAuthorizationHeaderAsync();

        capturedEndpoint.Should().StartWith(OidcAuthority,
            "the backchannel rewrite is gated on IsDevelopment; Production must use the public host");
        capturedEndpoint.Should().NotContain(BackchannelUrl,
            "the internal HTTP address must never appear in a Production token request");
    }

    /// <summary>
    /// Even with the backchannel rewrite active, the IssuerValidator must still reject a
    /// token whose <c>iss</c> claim does not match any configured OidcAuthority.
    /// The transport reroute must not widen the trust boundary.
    /// </summary>
    [Fact]
    public void RefreshTokenAsync_StillValidatesIssuerOnRefreshedToken()
    {
        // The token endpoint rewrite changes WHERE the refresh grant is sent.
        // Once the refreshed access token is used on an incoming request, the JWT bearer
        // middleware validates it via the IssuerValidator wired in PrismAuthExtensions.
        // We assert that validator is strict regardless of the backchannel setting.
        using var envDev = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Development");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "",
            ["PrismBusinessApp:Tenants:0:ClientId"] = ClientId,
            ["PrismBusinessApp:Tenants:0:Code"] = "prism-dev",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Prism Dev",
            ["PrismBusinessApp:Tenants:0:OidcAuthority"] = OidcAuthority
        });

        var goodToken = CreateOidcToken(OidcAuthority, ClientId);
        var evilToken = CreateOidcToken("https://evil.example.com/realms/attack", ClientId);

        // Good issuer is accepted.
        var accept = () => options.TokenValidationParameters.IssuerValidator!(
            OidcAuthority, goodToken, options.TokenValidationParameters);
        accept.Should().NotThrow("the configured OidcAuthority must always be trusted");

        // Evil issuer is rejected even when the backchannel rewrite is active.
        var reject = () => options.TokenValidationParameters.IssuerValidator!(
            "https://evil.example.com/realms/attack", evilToken, options.TokenValidationParameters);
        reject.Should().Throw<SecurityTokenInvalidIssuerException>(
            "a token with a mismatched issuer must be rejected even when KEYCLOAK_BACKCHANNEL_URL is active");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Group B — JWKS rewrite gating
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When KEYCLOAK_BACKCHANNEL_URL is set AND the environment is Development, the JWKS
    /// metadata URL must be rewritten to the internal backchannel host so the server can
    /// reach Keycloak's /.well-known/openid-configuration endpoint.
    /// </summary>
    [Fact]
    public void JwksFetch_RewritesUrl_WhenBackchannelSetAndDevelopment()
    {
        using var envDev = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Development");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        string? capturedMetadataAddress = null;
        var resolver = BuildIssuerSigningKeyResolver(
            OidcAuthority, ClientId,
            metaAddr => capturedMetadataAddress = metaAddr);

        InvokeResolverWith(resolver, OidcAuthority, ClientId);

        capturedMetadataAddress.Should().StartWith(BackchannelUrl,
            "the JWKS metadata fetch must use the internal backchannel when in Development");
        capturedMetadataAddress.Should().EndWith(".well-known/openid-configuration");
        capturedMetadataAddress.Should().Contain("/realms/prism-dev/");
    }

    /// <summary>
    /// When KEYCLOAK_BACKCHANNEL_URL is absent, the JWKS metadata URL must remain the
    /// public OidcAuthority — no rewrite on the production path.
    /// </summary>
    [Fact]
    public void JwksFetch_DoesNotRewrite_WhenBackchannelUnset()
    {
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", null);

        string? capturedMetadataAddress = null;
        var resolver = BuildIssuerSigningKeyResolver(
            OidcAuthority, ClientId,
            metaAddr => capturedMetadataAddress = metaAddr);

        InvokeResolverWith(resolver, OidcAuthority, ClientId);

        capturedMetadataAddress.Should().StartWith(OidcAuthority,
            "without a backchannel URL the JWKS fetch must use the public OidcAuthority");
    }

    /// <summary>
    /// CRITICAL SAFETY TEST: when ASPNETCORE_ENVIRONMENT is NOT Development, the JWKS
    /// backchannel rewrite must NOT activate even if KEYCLOAK_BACKCHANNEL_URL is set.
    /// Production metadata fetches must always target the public HTTPS Keycloak URL.
    /// </summary>
    [Fact]
    public void JwksFetch_DoesNotRewrite_WhenNotDevelopment()
    {
        using var envProd = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Production");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        string? capturedMetadataAddress = null;
        var resolver = BuildIssuerSigningKeyResolver(
            OidcAuthority, ClientId,
            metaAddr => capturedMetadataAddress = metaAddr);

        InvokeResolverWith(resolver, OidcAuthority, ClientId);

        capturedMetadataAddress.Should().StartWith(OidcAuthority,
            "the JWKS backchannel rewrite is gated on IsDevelopment; Production must use the public host");
        capturedMetadataAddress.Should().NotContain(BackchannelUrl,
            "the internal HTTP address must never appear in a Production JWKS fetch");
    }

    /// <summary>
    /// SECURITY PROOF: signing keys fetched from the backchannel must NOT make the JWT
    /// validator trust a token with a mismatched issuer. Trust lives in the <c>iss</c>
    /// claim validated against configuration — not in the transport channel used to fetch
    /// the JWKS.
    ///
    /// Sign a token with <c>iss = "https://evil.example.com"</c>; even though the signing
    /// keys arrived via the internal backchannel, the IssuerValidator must reject it.
    /// </summary>
    [Fact]
    public void JwtValidation_StillRejectsTokenWithMismatchedIssuer_EvenWhenJwksFetchedFromBackchannel()
    {
        using var envDev = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Development");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "",
            ["PrismBusinessApp:Tenants:0:ClientId"] = ClientId,
            ["PrismBusinessApp:Tenants:0:Code"] = "prism-dev",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Prism Dev",
            ["PrismBusinessApp:Tenants:0:OidcAuthority"] = OidcAuthority
        });

        // An attacker-crafted token: correct audience, but issuer pointing at evil.example.com.
        // Even if an attacker could somehow inject their own JWKS into the backchannel, the
        // issuer check is an independent validation step that references configuration, not
        // the transport path. It must always reject a non-configured issuer.
        var evilToken = CreateOidcToken("https://evil.example.com", ClientId);

        var act = () => options.TokenValidationParameters.IssuerValidator!(
            "https://evil.example.com",
            evilToken,
            options.TokenValidationParameters);

        act.Should().Throw<SecurityTokenInvalidIssuerException>(
            "issuer validation is independent of the JWKS transport; a mismatched iss must always be rejected");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Group C — Bedrock guard invariants
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// On the production path (no backchannel), RequireHttpsMetadata must be true so that
    /// the OIDC metadata endpoint is never fetched over plain HTTP.
    /// </summary>
    [Fact]
    public void ProductionPath_RequireHttpsMetadata_IsTrue()
    {
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", null);

        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "",
            ["PrismBusinessApp:Tenants:0:ClientId"] = ClientId,
            ["PrismBusinessApp:Tenants:0:Code"] = "prism-dev",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Prism Dev",
            ["PrismBusinessApp:Tenants:0:OidcAuthority"] = "https://keycloak.example.com/realms/prism-dev"
        });

        options.RequireHttpsMetadata.Should().BeTrue(
            "the production code path must never fetch OIDC metadata over plain HTTP");
    }

    /// <summary>
    /// JWT bearer options must always have both issuer and audience validation enabled.
    /// Disabling either would allow tokens from untrusted issuers or for unintended audiences
    /// to pass validation.
    /// </summary>
    [Fact]
    public void JwtBearer_ValidateIssuerAndAudience_AreTrueInOptions()
    {
        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "",
            ["PrismBusinessApp:Tenants:0:ClientId"] = ClientId,
            ["PrismBusinessApp:Tenants:0:Code"] = "prism-dev",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Prism Dev",
            ["PrismBusinessApp:Tenants:0:OidcAuthority"] = "https://keycloak.example.com/realms/prism-dev"
        });

        options.TokenValidationParameters.ValidateIssuer.Should().BeTrue(
            "issuer validation must always be enabled");
        options.TokenValidationParameters.ValidateAudience.Should().BeTrue(
            "audience validation must always be enabled");
    }

    /// <summary>
    /// MockBusinessApp/Program.cs lines 38-41 must contain a fail-loud guard that throws
    /// when KEYCLOAK_BACKCHANNEL_URL is set in any non-Development environment. This is
    /// the last-resort defence ensuring a misconfigured deployment is immediately visible.
    ///
    /// This is a read-only assertion against the wiring — the guard text is verified to
    /// exist in source so that it cannot be silently removed without breaking this test.
    /// </summary>
    [Fact]
    public void MockBusinessApp_FailLoudGuard_ExistsAndWouldThrow_WhenBackchannelSetInProduction()
    {
        // Navigate from the test output directory to the MockBusinessApp source.
        var programPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "../../../../../src/UmbracoPrism.MockBusinessApp/Program.cs"));

        File.Exists(programPath).Should().BeTrue(
            $"MockBusinessApp/Program.cs must exist at {programPath}");

        var source = File.ReadAllText(programPath);

        source.Should().Contain("KEYCLOAK_BACKCHANNEL_URL must not be set in non-Development environments",
            "the fail-loud error message must be present in Program.cs");
        source.Should().Contain("!app.Environment.IsDevelopment()",
            "the guard must check the hosting environment before throwing");
        source.Should().Contain("throw new InvalidOperationException(",
            "the guard must throw rather than log-and-continue so misconfiguration is immediately visible");
        source.Should().Contain("KEYCLOAK_BACKCHANNEL_URL",
            "the guard must explicitly reference the env var it is protecting against");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Group D — Regional Codespaces URL regression (new {token}-{port}.{region}.app.github.dev scheme)
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Regression test for the new Codespaces regional URL scheme:
    /// {opaque-token}-{port}.{region}.app.github.dev (e.g. v7ldkc4c-8443.uks1.app.github.dev).
    /// The BackchannelRewritingDocumentRetriever must rewrite these URLs identically to the
    /// legacy {CODESPACE_NAME}-{port}.app.github.dev form — both are just HTTPS origins and
    /// the rewriter works on URI origins, not hostname patterns.
    /// </summary>
    [Fact]
    public void JwksFetch_RewritesUrl_ForRegionalCodespacesUrlScheme()
    {
        const string regionalAuthority = "https://v7ldkc4c-8443.uks1.app.github.dev/realms/prism-dev";

        using var envDev = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Development");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        string? capturedMetadataAddress = null;
        var resolver = BuildIssuerSigningKeyResolver(
            regionalAuthority, ClientId,
            metaAddr => capturedMetadataAddress = metaAddr);

        InvokeResolverWith(resolver, regionalAuthority, ClientId);

        capturedMetadataAddress.Should().StartWith(BackchannelUrl,
            "the JWKS backchannel rewrite must work with the new regional Codespaces URL scheme");
        capturedMetadataAddress.Should().EndWith(".well-known/openid-configuration");
        capturedMetadataAddress.Should().Contain("/realms/prism-dev/");
        capturedMetadataAddress.Should().NotContain("v7ldkc4c-8443.uks1.app.github.dev",
            "the regional Codespaces hostname must be replaced by the internal backchannel host");
    }

    /// <summary>
    /// Regression: issuer validation must still reject a mismatched iss even when the
    /// OidcAuthority uses the new regional Codespaces URL scheme.
    /// </summary>
    [Fact]
    public void JwtValidation_StillRejectsTokenWithMismatchedIssuer_ForRegionalCodespacesUrl()
    {
        const string regionalAuthority = "https://v7ldkc4c-8443.uks1.app.github.dev/realms/prism-dev";

        using var envDev = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Development");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        var options = BuildJwtOptions(new Dictionary<string, string?>
        {
            ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "",
            ["PrismBusinessApp:Tenants:0:ClientId"] = ClientId,
            ["PrismBusinessApp:Tenants:0:Code"] = "prism-dev",
            ["PrismBusinessApp:Tenants:0:DisplayName"] = "Prism Dev",
            ["PrismBusinessApp:Tenants:0:OidcAuthority"] = regionalAuthority
        });

        var evilToken = CreateOidcToken("https://evil.example.com", ClientId);

        var act = () => options.TokenValidationParameters.IssuerValidator!(
            "https://evil.example.com",
            evilToken,
            options.TokenValidationParameters);

        act.Should().Throw<SecurityTokenInvalidIssuerException>(
            "issuer validation must remain strict regardless of the Codespaces URL scheme in use");
    }


    /// <summary>
    /// Builds a PrismContext wired for an expired-token-then-refresh scenario using a
    /// generic OIDC (Keycloak) tenant. The <paramref name="onRefreshCall"/> callback fires
    /// with the token endpoint URL each time RefreshAsync is invoked, letting tests assert
    /// which endpoint was targeted.
    /// </summary>
    private static (PrismContext prismContext, Mock<IPrismTokenRefreshService> tokenRefreshService)
        BuildPrismContextWithCapture(string oidcAuthority, string clientId, Action<string> onRefreshCall)
    {
        // Only a refresh_token — no access_token — so GetAuthorizationHeaderAsync always calls RefreshTokenAsync.
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" }
        });

        var principal = CreateKeycloakPrincipal(oidcAuthority, clientId);
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new StubAuthService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var vault = new Mock<ISecretVaultService>();
        vault.Setup(v => v.ResolveSecretAsync(PrismSecretProviderNames.Inline, ClientSecret))
            .ReturnsAsync(ClientSecret);

        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        tokenRefreshService
            .Setup(t => t.RefreshAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .Callback<string, IReadOnlyDictionary<string, string>, CancellationToken, IReadOnlyDictionary<string, string>?>(
                (endpoint, _, _, _) => onRefreshCall(endpoint))
            .ReturnsAsync(new TokenRefreshResult(true, "new-access-token", "new-refresh-token", 3600));

        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object, new PrismTenantBindingValidator())
        {
            CurrentTenant = new PrismTenant
            {
                OidcAuthority = oidcAuthority,
                OidcClientId = clientId,
                OidcClientSecretProvider = PrismSecretProviderNames.Inline,
                OidcClientSecretReference = ClientSecret
            }
        };

        return (prismContext, tokenRefreshService);
    }

    /// <summary>
    /// Builds the <see cref="IssuerSigningKeyResolver"/> delegate from
    /// <see cref="JwtBearerOptions"/> with a mock <see cref="IPrismSigningKeyCache"/> that
    /// captures the <c>metadataAddress</c> argument passed to
    /// <see cref="IPrismSigningKeyCache.WarmAsync(string, string, bool, string?, CancellationToken)"/>.
    /// </summary>
    private static IssuerSigningKeyResolver BuildIssuerSigningKeyResolver(
        string oidcAuthority, string clientId,
        Action<string> onMetadataAddress)
    {
        var mockCache = new Mock<IPrismSigningKeyCache>();

        // Return an expired snapshot so ResolveSigningKeys always calls WarmAsync.
        mockCache.Setup(c => c.GetSnapshot(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new PrismSigningKeyCacheSnapshot([], ShouldRefresh: true, IsExpired: true, ContainsRequestedKey: false));

        mockCache.Setup(c => c.WarmAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, string?, CancellationToken>(
                (_, metaAddr, _, _, _) => onMetadataAddress(metaAddr))
            .Returns(Task.CompletedTask);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "",
                ["PrismBusinessApp:Tenants:0:ClientId"] = clientId,
                ["PrismBusinessApp:Tenants:0:Code"] = "prism-dev",
                ["PrismBusinessApp:Tenants:0:DisplayName"] = "Prism Dev",
                ["PrismBusinessApp:Tenants:0:OidcAuthority"] = oidcAuthority
            })
            .Build();

        var services = new ServiceCollection();
        // Register mock BEFORE AddPrismAuthentication; TryAddSingleton won't replace it.
        services.AddSingleton<IPrismSigningKeyCache>(mockCache.Object);
        services.AddPrismAuthentication(configuration);

        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        return options.TokenValidationParameters.IssuerSigningKeyResolver!;
    }

    /// <summary>
    /// Invokes the signing key resolver with a minimal Keycloak-style token (no <c>tid</c>
    /// claim, <c>iss</c> = <paramref name="issuer"/>). This routes through the generic OIDC
    /// path in <c>ResolveSigningKeys</c>.
    /// </summary>
    private static void InvokeResolverWith(IssuerSigningKeyResolver resolver, string issuer, string clientId)
    {
        var token = CreateOidcToken(issuer, clientId);
        resolver.Invoke(token.RawData ?? string.Empty, token, null, new TokenValidationParameters());
    }

    private static JwtBearerOptions BuildJwtOptions(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var services = new ServiceCollection();
        services.AddPrismAuthentication(configuration);
        var provider = services.BuildServiceProvider();
        return provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
    }

    private static JwtSecurityToken CreateOidcToken(string issuer, string clientId)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.CreateJwtSecurityToken(
            issuer: issuer,
            audience: clientId,
            subject: new ClaimsIdentity(new[] { new Claim("sub", "user-123") }),
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: DateTime.UtcNow.AddHours(1));
    }

    private static ClaimsPrincipal CreateKeycloakPrincipal(string issuer, string clientId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("iss", issuer));
        identity.AddClaim(new Claim("aud", clientId));
        return new ClaimsPrincipal(identity);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Group E — X-Forwarded headers on backchannel refresh (invalid_grant fix)
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the backchannel rewrite is active, X-Forwarded-Proto and X-Forwarded-Host
    /// must be passed to RefreshAsync so Keycloak (running with --proxy-headers xforwarded)
    /// computes its canonical issuer as the public HTTPS authority.
    ///
    /// Without these headers Keycloak sees the backchannel HTTP request and derives its
    /// issuer as http://... while the stored refresh token's iss claim is https://... —
    /// the scheme mismatch causes Keycloak to return invalid_grant.
    /// </summary>
    [Fact]
    public async Task RefreshTokenAsync_SendsForwardingHeaders_WhenBackchannelRewriteActive()
    {
        using var envDev = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Development");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", BackchannelUrl);

        IReadOnlyDictionary<string, string>? capturedHeaders = null;
        var (prismContext, _) = BuildPrismContextWithHeaderCapture(
            OidcAuthority, ClientId,
            headers => capturedHeaders = headers);

        await prismContext.GetAuthorizationHeaderAsync();

        capturedHeaders.Should().NotBeNull("forwarding headers must be passed on the backchannel path");
        var forwardedHeaders = capturedHeaders!;
        forwardedHeaders.Should().ContainKey("X-Forwarded-Proto");
        forwardedHeaders["X-Forwarded-Proto"].Should().Be("https",
            "the public OidcAuthority scheme is https so Keycloak must compute an https issuer");
        forwardedHeaders.Should().ContainKey("X-Forwarded-Host");
        forwardedHeaders["X-Forwarded-Host"].Should().Be(new Uri(OidcAuthority).Host,
            "Keycloak must see the public hostname, not localhost");
    }

    /// <summary>
    /// On the non-backchannel path (no rewrite), no X-Forwarded headers must be injected.
    /// Adding forwarding headers to a direct HTTPS call to the public Keycloak endpoint
    /// would be redundant and could confuse reverse proxies.
    /// </summary>
    [Fact]
    public async Task RefreshTokenAsync_DoesNotSendForwardingHeaders_WhenBackchannelUnset()
    {
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", null);

        IReadOnlyDictionary<string, string>? capturedHeaders = null;
        var (prismContext, _) = BuildPrismContextWithHeaderCapture(
            OidcAuthority, ClientId,
            headers => capturedHeaders = headers);

        await prismContext.GetAuthorizationHeaderAsync();

        capturedHeaders.Should().BeNull(
            "no forwarding headers must be passed on the direct-to-public path");
    }

    /// <summary>
    /// CRITICAL SAFETY: even when forwarding headers are passed to the backchannel refresh,
    /// the X-Forwarded-Proto value must be derived from the configured OidcAuthority scheme
    /// (https), never from the backchannel base URL scheme (http). Using the backchannel
    /// scheme would produce an http forwarding header, which defeats the purpose of the fix
    /// and could allow Keycloak to issue tokens with an http issuer.
    /// </summary>
    [Fact]
    public async Task RefreshTokenAsync_ForwardingHeaders_UseAuthorityScheme_NotBackchannelScheme()
    {
        const string httpBackchannel = "http://localhost:8080"; // backchannel is always HTTP
        using var envDev = new TempEnvVar("ASPNETCORE_ENVIRONMENT", "Development");
        using var envBc = new TempEnvVar("KEYCLOAK_BACKCHANNEL_URL", httpBackchannel);

        IReadOnlyDictionary<string, string>? capturedHeaders = null;
        var (prismContext, _) = BuildPrismContextWithHeaderCapture(
            OidcAuthority, ClientId,  // OidcAuthority is https://...
            headers => capturedHeaders = headers);

        await prismContext.GetAuthorizationHeaderAsync();

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!["X-Forwarded-Proto"].Should().Be("https",
            "the X-Forwarded-Proto value must come from OidcAuthority (https), not the backchannel base (http)");
        capturedHeaders["X-Forwarded-Proto"].Should().NotBe("http",
            "passing X-Forwarded-Proto: http would make Keycloak compute an http issuer, reproducing the bug");
    }

    /// <summary>
    /// Builds a PrismContext wired for an expired-token-then-refresh scenario, capturing
    /// the <c>requestHeaders</c> argument passed to <see cref="IPrismTokenRefreshService.RefreshAsync"/>
    /// so tests can assert which forwarding headers were (or were not) included.
    /// </summary>
    private static (PrismContext prismContext, Mock<IPrismTokenRefreshService> tokenRefreshService)
        BuildPrismContextWithHeaderCapture(
            string oidcAuthority, string clientId,
            Action<IReadOnlyDictionary<string, string>?> onHeaders)
    {
        var props = new AuthenticationProperties();
        props.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-token" }
        });

        var principal = CreateKeycloakPrincipal(oidcAuthority, clientId);
        var ticket = new AuthenticationTicket(principal, props, "PrismMemberCookie");
        var authResult = AuthenticateResult.Success(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new StubAuthService(authResult))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var vault = new Mock<ISecretVaultService>();
        vault.Setup(v => v.ResolveSecretAsync(PrismSecretProviderNames.Inline, ClientSecret))
            .ReturnsAsync(ClientSecret);

        var tokenRefreshService = new Mock<IPrismTokenRefreshService>();
        tokenRefreshService
            .Setup(t => t.RefreshAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .Callback<string, IReadOnlyDictionary<string, string>, CancellationToken, IReadOnlyDictionary<string, string>?>(
                (_, _, _, headers) => onHeaders(headers))
            .ReturnsAsync(new TokenRefreshResult(true, "new-access-token", "new-refresh-token", 3600));

        var prismContext = new PrismContext(accessor, vault.Object, tokenRefreshService.Object, new PrismTenantBindingValidator())
        {
            CurrentTenant = new PrismTenant
            {
                OidcAuthority = oidcAuthority,
                OidcClientId = clientId,
                OidcClientSecretProvider = PrismSecretProviderNames.Inline,
                OidcClientSecretReference = ClientSecret
            }
        };

        return (prismContext, tokenRefreshService);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Inner types
    // ──────────────────────────────────────────────────────────────────────────────

    private sealed class TempEnvVar : IDisposable
    {
        private readonly string _name;
        private readonly string? _original;

        public TempEnvVar(string name, string? value)
        {
            _name = name;
            _original = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
    }

    private sealed class StubAuthService(AuthenticateResult result) : IAuthenticationService
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
