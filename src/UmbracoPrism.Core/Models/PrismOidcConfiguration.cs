using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using Microsoft.Identity.Web;
using Microsoft.Identity.Client;
using System.Security.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web.TokenCacheProviders;
using System.Security.Claims;
using System.Text.Json;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Post-configures Prism OpenID Connect options with per-request tenant authority, audience, and signing keys.
/// </summary>
/// <param name="httpContextAccessor">Provides request context used to resolve the current Prism tenant.</param>
/// <param name="signingKeyCache">Provides cached tenant signing keys for token validation.</param>
/// <param name="logger">Logger for structured diagnostics and security event recording.</param>
public class PrismOidcConfiguration(IHttpContextAccessor httpContextAccessor, IPrismSigningKeyCache signingKeyCache, ILogger<PrismOidcConfiguration> logger) : IPostConfigureOptions<OpenIdConnectOptions>
{

    private const string PrismNoncePropertiesKey = ".prism_nonce";
    private const string GenericOidcBrowserScopes = "openid profile";
    private const string LocalDemoOfflineScopes = "openid profile offline_access";
    private const string LocalDemoHostname = "localhost";
    private const string LocalDemoClientId = "prism-client";

    internal static string GetRequestedScope(PrismTenant tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant.OidcAuthority))
        {
            return IsRepoOwnedLocalDemoTenant(tenant)
                ? LocalDemoOfflineScopes
                : GenericOidcBrowserScopes;
        }

        return $"openid profile offline_access {tenant.EntraClientId}/.default";
    }

    internal static string? GetRefreshScope(PrismTenant tenant)
    {
        if (IsRepoOwnedLocalDemoTenant(tenant))
        {
            return null;
        }

        return GetRequestedScope(tenant);
    }

    internal static async Task<string> ResolveClientSecretAsync(PrismTenant tenant, ISecretVaultService vault)
    {
        if (string.IsNullOrWhiteSpace(tenant.OidcAuthority))
        {
            return await vault.GetSecretAsync(tenant.SecretKeyName ?? string.Empty);
        }

        if (string.Equals(tenant.OidcClientSecretProvider, PrismSecretProviderNames.Inline, StringComparison.OrdinalIgnoreCase)
            && !IsRepoOwnedLocalDemoTenant(tenant))
        {
            return string.Empty;
        }

        return await vault.ResolveSecretAsync(tenant.OidcClientSecretProvider, tenant.OidcClientSecretReference)
            ?? string.Empty;
    }

    internal static bool IsRepoOwnedLocalDemoTenant(PrismTenant tenant)
    {
        // The incoming-request hostname is the tenant routing key.
        // Accept localhost (local Aspire dev) or *.app.github.dev (GitHub Codespaces).
        var hostname = tenant.Hostname;
        var isRepoDemoHost =
            string.Equals(hostname, LocalDemoHostname, StringComparison.OrdinalIgnoreCase)
            || (hostname?.EndsWith(".app.github.dev", StringComparison.OrdinalIgnoreCase) == true);

        if (!isRepoDemoHost)
        {
            return false;
        }

        if (!string.Equals(tenant.OidcClientId, LocalDemoClientId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(tenant.OidcAuthority, UriKind.Absolute, out var authority))
        {
            return false;
        }

        // The Keycloak authority must also be repo-owned demo infrastructure.
        var authorityHost = authority.Host;
        return string.Equals(authorityHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || authorityHost.EndsWith(".app.github.dev", StringComparison.OrdinalIgnoreCase);
    }

    internal static List<AuthenticationToken> CreateAuthenticationTokens(JsonElement payload, DateTimeOffset issuedAtUtc)
    {
        var tokens = new List<AuthenticationToken>
        {
            new() { Name = "access_token", Value = payload.GetProperty("access_token").GetString() ?? string.Empty },
            new() { Name = "expires_at", Value = issuedAtUtc.AddSeconds(payload.GetProperty("expires_in").GetInt32()).ToString("o") }
        };

        var idToken = payload.GetProperty("id_token").GetString();
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            tokens.Add(new AuthenticationToken { Name = "id_token", Value = idToken });
        }

        if (payload.TryGetProperty("refresh_token", out var refreshTokenElement))
        {
            var refreshToken = refreshTokenElement.GetString();
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                tokens.Add(new AuthenticationToken { Name = "refresh_token", Value = refreshToken });
            }
        }

        return tokens;
    }

    /// <summary>
    /// Applies Prism dynamic OIDC settings for the named authentication scheme.
    /// </summary>
    /// <param name="name">The authentication scheme name being configured.</param>
    /// <param name="options">The OIDC options instance to mutate.</param>
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        if (name != "PrismEntraID") return;

        logger.LogDebug("Prism OIDC configuration started for scheme {SchemeName}", name);

        // Basic Defaults
        options.ClientId = "DYNAMIC_TENANT_PLACEHOLDER";
        options.Authority = "https://login.microsoftonline.com/common/v2.0";
        options.MapInboundClaims = false;

        options.TokenValidationParameters.ValidateIssuer = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.Query;
        options.SaveTokens = true;
        options.CallbackPath = "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";
        options.SignedOutRedirectUri = "/";

        // Add scopes cooperatively
        if (!options.Scope.Contains("openid")) options.Scope.Add("openid");
        if (!options.Scope.Contains("profile")) options.Scope.Add("profile");

        options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
        {
            var httpContext = httpContextAccessor.HttpContext;
            var prismContext = httpContext?.RequestServices.GetRequiredService<IPrismContext>();
            var tenant = prismContext?.CurrentTenant;

            if (tenant == null) return [];

            // Handle generic OIDC providers (Keycloak, Okta, etc.)
            if (!string.IsNullOrEmpty(tenant.OidcAuthority))
            {
                validationParameters.ValidAudience = tenant.OidcClientId;
                validationParameters.ValidIssuer = tenant.OidcAuthority;
                validationParameters.ValidateAudience = true;
                validationParameters.ValidateIssuer = true;

                // For generic OIDC, rely on standard OIDC discovery and key validation
                // The middleware will fetch keys from {authority}/.well-known/openid-configuration
                return [];
            }

            // Handle Entra-specific path (existing behavior)
            if (string.IsNullOrEmpty(tenant.EntraTenantId)) return [];

            validationParameters.ValidAudience = tenant.EntraClientId;
            validationParameters.ValidIssuer = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/v2.0";
            validationParameters.ValidateAudience = true;
            validationParameters.ValidateIssuer = true;

            var snapshot = signingKeyCache.GetSnapshot(tenant.EntraTenantId, kid);
            if (snapshot.ShouldRefresh)
            {
                _ = signingKeyCache.WarmAsync(
                    tenant.EntraTenantId,
                    forceRefresh: true,
                    cancellationToken: CancellationToken.None);
            }

            if (snapshot.IsExpired || !snapshot.ContainsRequestedKey)
            {
                return [];
            }

            return snapshot.Keys;
        };

        // --- Event Wrapping Logic ---
        // We capture the handlers registered by Microsoft.Identity.Web and wrap them.

        var onRedirectToIdentityProvider = options.Events.OnRedirectToIdentityProvider;
        options.Events.OnRedirectToIdentityProvider = async context =>
        {
            var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
            var tenant = prismContext?.CurrentTenant;

            if (tenant != null)
            {
                // Support both generic OIDC (Keycloak, Okta, etc.) and Entra-specific paths
                if (!string.IsNullOrEmpty(tenant.OidcAuthority))
                {
                    // Generic OIDC provider path (e.g., Keycloak)
                    context.ProtocolMessage.IssuerAddress = $"{tenant.OidcAuthority}/protocol/openid-connect/auth";
                    context.ProtocolMessage.ClientId = tenant.OidcClientId;
                    context.Options.Authority = tenant.OidcAuthority;
                }
                else if (!string.IsNullOrEmpty(tenant.EntraTenantId))
                {
                    // Entra-specific path (existing behavior)
                    var baseUri = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}";
                    context.ProtocolMessage.IssuerAddress = $"{baseUri}/oauth2/v2.0/authorize";
                    context.ProtocolMessage.ClientId = tenant.EntraClientId;
                    context.Options.Authority = $"{baseUri}/v2.0";
                }
            }

            logger.LogDebug("Prism OIDC redirecting to {IssuerAddress} for client {ClientId}", context.ProtocolMessage.IssuerAddress, context.ProtocolMessage.ClientId);


            // 2. Now call Microsoft's internal logic
            // It will see the message already has a ClientId and IssuerAddress and should respect them
            await onRedirectToIdentityProvider(context);

            // 3. Post-processing: Respect prompt from challenge properties (e.g. "create" for
            //    registration), otherwise default to "select_account".
            if (tenant != null)
            {
                if (!string.IsNullOrEmpty(tenant.OidcAuthority))
                {
                    // The repo-owned localhost demo opts into offline tokens so downstream calls can
                    // survive a full local AppHost restart. Other generic OIDC tenants keep the
                    // standard minimal browser scopes unless product requirements say otherwise.
                    context.ProtocolMessage.Scope = GetRequestedScope(tenant);
                }

                var promptOverride = context.Properties.Items.TryGetValue(
                    "PrismPrompt", out var p) ? p : null;
                context.ProtocolMessage.Prompt = !string.IsNullOrEmpty(promptOverride)
                    ? promptOverride
                    : "select_account";
            }

            context.Properties.Items["Prism_PKCE_Verifier"] = context.Properties.Items.TryGetValue("code_verifier", out var verifier)
                ? verifier
                : "";

            // Capture the nonce generated by the OIDC middleware so we can validate it on callback
            if (!string.IsNullOrEmpty(context.ProtocolMessage.Nonce))
            {
                context.Properties.Items[PrismNoncePropertiesKey] = context.ProtocolMessage.Nonce;
            }
        };

        options.Events.OnAuthorizationCodeReceived = async context =>
        {
            var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
            var tenant = prismContext?.CurrentTenant;

            if (tenant != null && (!string.IsNullOrEmpty(tenant.OidcAuthority) || !string.IsNullOrEmpty(tenant.EntraTenantId)))
            {
                string authority;
                string clientId;
                string secret;
                string scope;
                var vault = context.HttpContext.RequestServices.GetRequiredService<ISecretVaultService>();

                // Determine which OIDC provider we're using
                if (!string.IsNullOrEmpty(tenant.OidcAuthority))
                {
                    // Generic OIDC provider (Keycloak, Okta, etc.)
                    authority = $"{tenant.OidcAuthority}/protocol/openid-connect/token";
                    clientId = tenant.OidcClientId ?? string.Empty;
                    secret = await ResolveClientSecretAsync(tenant, vault);
                    scope = GetRequestedScope(tenant);

                    if (string.IsNullOrWhiteSpace(secret))
                    {
                        throw new AuthenticationException(
                            "Generic OIDC client secret is not configured. Use SecretKeyName for managed secrets, or keep inline secrets limited to the repo-owned localhost demo tenant.");
                    }
                }
                else
                {
                    // Entra-specific path (existing behavior with Key Vault)
                    secret = await ResolveClientSecretAsync(tenant, vault);
                    authority = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/oauth2/v2.0/token";
                    clientId = tenant.EntraClientId ?? string.Empty;
                    scope = GetRequestedScope(tenant);
                }

                var redirectUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{options.CallbackPath}";

                string? verifier = null;
                context.Properties?.Items.TryGetValue("Prism_PKCE_Verifier", out verifier);

                // 1. Manually exchange the code for tokens via simple HTTP
                using var client = new HttpClient();
                var response = await client.PostAsync(authority, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "client_secret", secret },
                    { "grant_type", "authorization_code" },
                    { "code", context.ProtocolMessage.Code },
                    { "redirect_uri", redirectUri },
                    { "code_verifier", verifier ?? "" },
                    { "scope", scope }
                }));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new AuthenticationException($"Token exchange failed: {error}");
                }

                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var payload = json.RootElement;

                var tokens = CreateAuthenticationTokens(payload, DateTimeOffset.UtcNow);

                // Use the existing properties from the context (which contain the PKCE verifier, etc.)
                var props = context.Properties ?? new AuthenticationProperties();
                props.IssuedUtc = DateTimeOffset.UtcNow;
                props.StoreTokens(tokens);

                // Validate the ID token — parse header first to get kid for cache lookup (no claim trust at this stage)
                var idToken = payload.GetProperty("id_token").GetString();
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var parsedHeader = handler.ReadJwtToken(idToken);
                var kid = parsedHeader.Header.Kid;

                // Hard nonce validation — fail closed if nonce is absent from authentication properties
                if (!props.Items.TryGetValue(PrismNoncePropertiesKey, out var expectedNonce) || string.IsNullOrEmpty(expectedNonce))
                {
                    throw new AuthenticationException("Nonce is missing from authentication properties.");
                }

                System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt;
                
                if (!string.IsNullOrEmpty(tenant.OidcAuthority))
                {
                    // Generic OIDC provider - use standard OIDC discovery
                    try
                    {
                        var metadataAddress = $"{tenant.OidcAuthority}/.well-known/openid-configuration";
                        var requireHttps = !metadataAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                        var configurationManager = new Microsoft.IdentityModel.Protocols.ConfigurationManager<OpenIdConnectConfiguration>(
                            metadataAddress,
                            new OpenIdConnectConfigurationRetriever(),
                            new HttpDocumentRetriever(new System.Net.Http.HttpClient()) { RequireHttps = requireHttps });

                        var oidcConfig = await configurationManager.GetConfigurationAsync(CancellationToken.None);

                        var validationParameters = new TokenValidationParameters
                        {
                            ValidIssuer = tenant.OidcAuthority,
                            ValidAudience = tenant.OidcClientId,
                            IssuerSigningKeys = oidcConfig.SigningKeys,
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromMinutes(5),
                            RequireSignedTokens = true,
                            RequireExpirationTime = true
                        };

                        handler.ValidateToken(idToken, validationParameters, out var validatedToken);
                        jwt = (System.IdentityModel.Tokens.Jwt.JwtSecurityToken)validatedToken;
                    }
                    catch (SecurityTokenException ex)
                    {
                        logger.LogError(ex, "ID token signature/claims validation failed for OIDC tenant {Authority}", tenant.OidcAuthority);
                        context.HandleResponse();
                        context.Response.Redirect("/error?reason=token_validation_failed");
                        return;
                    }
                }
                else
                {
                    // Entra-specific validation using cached signing keys
                    if (string.IsNullOrEmpty(tenant.EntraTenantId))
                    {
                        logger.LogError("ID token validation failed: no OIDC authority or Entra tenant ID configured");
                        context.HandleResponse();
                        context.Response.Redirect("/error?reason=token_validation_failed");
                        return;
                    }

                    var snapshot = signingKeyCache.GetSnapshot(tenant.EntraTenantId, kid);
                    if (snapshot.ShouldRefresh)
                    {
                        await signingKeyCache.WarmAsync(tenant.EntraTenantId, forceRefresh: true, CancellationToken.None);
                        snapshot = signingKeyCache.GetSnapshot(tenant.EntraTenantId, kid);
                    }

                    if (snapshot.IsExpired || !snapshot.ContainsRequestedKey)
                    {
                        logger.LogError("ID token validation failed for tenant {TenantId}: signing key '{Kid}' not found in cache", tenant.EntraTenantId, kid);
                        context.HandleResponse();
                        context.Response.Redirect("/error?reason=token_validation_failed");
                        return;
                    }

                    try
                    {
                        var validationParameters = new TokenValidationParameters
                        {
                            ValidIssuer = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/v2.0",
                            ValidAudience = tenant.EntraClientId,
                            IssuerSigningKeys = snapshot.Keys,
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromMinutes(5),
                            RequireSignedTokens = true,
                            RequireExpirationTime = true
                        };

                        handler.ValidateToken(idToken, validationParameters, out var validatedToken);
                        jwt = (System.IdentityModel.Tokens.Jwt.JwtSecurityToken)validatedToken;
                    }
                    catch (SecurityTokenException ex)
                    {
                        logger.LogError(ex, "ID token signature/claims validation failed for tenant {TenantId}", tenant.EntraTenantId);
                        context.HandleResponse();
                        context.Response.Redirect("/error?reason=token_validation_failed");
                        return;
                    }
                }

                // Hard nonce validation — fail closed if nonce claim absent or mismatched
                var tokenNonce = jwt.Payload.Nonce;
                if (string.IsNullOrEmpty(tokenNonce))
                {
                    throw new AuthenticationException("Nonce claim is missing from the ID token.");
                }

                if (!string.Equals(expectedNonce, tokenNonce, StringComparison.Ordinal))
                {
                    throw new AuthenticationException("Nonce mismatch: token nonce does not match authentication properties nonce.");
                }

                var identity = new ClaimsIdentity(jwt.Claims, "PrismEntraID", "name", "role");
                var principal = new ClaimsPrincipal(identity);
                var returnUrl = PrismReturnUrl.Normalize(props.RedirectUri);

                // Persist the Prism member session without carrying the one-off post-login
                // redirect target forward into later authenticated requests.
                props.RedirectUri = null;
                await context.HttpContext.SignInAsync("PrismMemberCookie", principal, props);

                // Tell the OIDC middleware to STOP. 
                // If we don't call HandleResponse, it will try to sign in again and overwrite our cookie.
                context.HandleResponse();

                // Redirect manually after framework-backed local URL normalization.
                context.Response.Redirect(returnUrl);
            }
        };

        var onRedirectToIdentityProviderForSignOut = options.Events.OnRedirectToIdentityProviderForSignOut;
        options.Events.OnRedirectToIdentityProviderForSignOut = async context =>
        {
            await onRedirectToIdentityProviderForSignOut(context);

            var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();
            var tenant = prismContext?.CurrentTenant;

            if (tenant != null)
            {
                if (!string.IsNullOrEmpty(tenant.OidcAuthority))
                {
                    // Generic OIDC provider logout
                    context.ProtocolMessage.IssuerAddress = $"{tenant.OidcAuthority}/protocol/openid-connect/logout";
                    if (string.IsNullOrWhiteSpace(context.ProtocolMessage.ClientId))
                    {
                        context.ProtocolMessage.ClientId = tenant.OidcClientId;
                    }

                    if (string.IsNullOrWhiteSpace(context.ProtocolMessage.IdTokenHint))
                    {
                        var authResult = await context.HttpContext.AuthenticateAsync("PrismMemberCookie");
                        var idToken = authResult.Properties?.GetTokens()
                            ?.FirstOrDefault(token => token.Name == "id_token")
                            ?.Value;

                        if (!string.IsNullOrWhiteSpace(idToken))
                        {
                            context.ProtocolMessage.IdTokenHint = idToken;
                            logger.LogDebug(
                                "Prism OIDC logout restored id_token_hint from PrismMemberCookie for generic authority {Authority}",
                                tenant.OidcAuthority);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Prism OIDC logout is missing id_token_hint for generic authority {Authority}; logout will fall back to client_id only",
                                tenant.OidcAuthority);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(tenant.EntraTenantId))
                {
                    // Entra-specific logout
                    var baseUri = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}";
                    context.ProtocolMessage.IssuerAddress = $"{baseUri}/oauth2/v2.0/logout";

                    var userEmail = context.HttpContext.User.FindFirst("preferred_username")?.Value
                                    ?? context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        context.ProtocolMessage.SetParameter("logout_hint", userEmail);
                    }
                }
            }
        };

    }
}
