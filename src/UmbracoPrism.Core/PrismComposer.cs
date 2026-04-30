using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using UmbracoPrism.Core.Middleware;
using Umbraco.Cms.Core.Notifications;
using UmbracoPrism.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Notifications;
using UmbracoPrism.Core.BackgroundServices;
using UmbracoPrism.Core.Extensions;

namespace UmbracoPrism.Core;

public class PrismComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // 1. Core Services
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ISecretVaultService, SecretVaultService>();
        builder.Services.AddSingleton<ITenantService, TenantService>();
        builder.Services.AddSingleton<IBrandingService, BrandingService>();
        builder.Services.AddSingleton<IMobileBundleService, MobileBundleService>();
        builder.Services.AddSingleton<IPrismBrandingMetadataService, PrismBrandingMetadataService>();
        builder.Services.AddSingleton<IPrismSigningKeyCache, PrismSigningKeyCache>();
        builder.Services.AddHttpClient("prism-oidc-metadata");
        builder.Services.AddHttpClient("PrismTokenRefresh");
        builder.Services.Configure<PrismTokenRefreshOptions>(
            builder.Config.GetSection(PrismTokenRefreshOptions.SectionName));
        builder.Services.AddSingleton<IPrismTokenRefreshService, PrismTokenRefreshService>();
        builder.Services.Configure<PrismBiometricOptions>(
            builder.Config.GetSection(PrismBiometricOptions.SectionName));
        builder.Services.ConfigureOptions<PrismKeyVaultConfigureOptions>();
        builder.Services.AddSingleton<IBiometricTokenService, BiometricTokenService>();
        builder.Services.AddSingleton<IRefreshTokenEncryptionService, RefreshTokenEncryptionService>();
        builder.Services.AddSingleton<IExchangeRateLimitService, ExchangeRateLimitService>();
        builder.Services.AddSingleton<INotificationRateLimitService, NotificationRateLimitService>();
        builder.Services.AddScoped<IPrismContext, PrismContext>();
        builder.Services.AddScoped<IPrismUserContext, PrismUserContext>();
        builder.Services.AddScoped<IPrismNotificationService, PrismNotificationService>();
        builder.Services.Configure<PrismConfiguration>(
            builder.Config.GetSection(PrismConfiguration.SectionName));
        builder.Services.AddHostedService<LimitedEditionDropNotifier>();

        // 2. Workflow Engine
        builder.AddPrismWorkflowEngine();

        // 3. Middleware Registration
        // ForwardedHeaders must run before any middleware that reads RemoteIpAddress
        // (e.g. biometric rate limiting in BiometricController).
        // PRODUCTION: configure KnownProxies / KnownNetworks to restrict which upstream
        // proxies are trusted to supply X-Forwarded-For (see ForwardedHeadersOptions docs).
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Clear default loopback-only network restrictions so ForwardedHeadersMiddleware
            // processes the header when running behind any proxy. Deployments SHOULD
            // restrict this to known proxy CIDRs via KnownNetworks before going to production.
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // SEC-PT2-004: Security response headers — configurable via Prism:SecurityHeaders.
        // Defaults: X-Content-Type-Options, X-Frame-Options (SAMEORIGIN), Referrer-Policy,
        // Permissions-Policy, HSTS (HTTPS only), CSP-Report-Only (promote to enforced CSP
        // once tuned per-deployment). Backoffice paths excluded by default.
        builder.Services.Configure<PrismSecurityHeadersOptions>(
            builder.Config.GetSection(PrismSecurityHeadersOptions.SectionName));

        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter(
                "PrismTenantResolution",
                app =>
                {
                    app.UseForwardedHeaders();
                    app.UseMiddleware<PrismSecurityHeadersMiddleware>();
                    app.UseMiddleware<PrismTenantMiddleware>();
                    app.UseMiddleware<PrismBrandingMiddleware>();
                }
            ));
        });

        // 4. Authorization Handler
        builder.Services.AddSingleton<IAuthorizationHandler, PrismTenantHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, PrismAdminHandler>();

        // 5. Dynamic OIDC Config & Credential Provider
        // Registering our custom PostConfigure logic as a Singleton is fine because 
        // it acts on the 'options' object passed in per-request.
        builder.Services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, PrismOidcConfiguration>();
        
        // 6. Authentication & Cookie Setup
        // Auth defaults are unconditional: PrismMemberCookie handles all member
        // requests regardless of whether an Azure Key Vault URI is configured.
        // Vault presence is an optional secret-provider detail, not a feature flag.
        var authBuilder = builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "PrismMemberCookie";
            options.DefaultSignInScheme = "PrismMemberCookie";
            options.DefaultChallengeScheme = "PrismEntraID";
        });

        authBuilder.AddMicrosoftIdentityWebApp(identityOptions =>
        {
            // Placeholders satisfy startup validation. 
            // Our CredentialProvider and OidcConfiguration will swap these out at runtime.
            identityOptions.Instance = "https://login.microsoftonline.com/";
            identityOptions.TenantId = "common";
            identityOptions.ClientId = "DYNAMIC_PLACEHOLDER";
            identityOptions.CallbackPath = "/signin-oidc";
            identityOptions.SignedOutCallbackPath = "/signout-callback-oidc";
            
            // Note: We no longer need TEMPORARY_PLACEHOLDER for the secret 
            // because the presence of an IClientAssertionProvider tells MSAL to use that instead.
        }, cookieOptions =>
        {
            cookieOptions.Cookie.Name = "PrismMemberCookie";
            cookieOptions.LoginPath = "/auth/login";
            cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
            cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        }, openIdConnectScheme: "PrismEntraID", cookieScheme: "PrismMemberCookie")
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches();

        // 7. Authorization Policy
        builder.Services.Configure<AuthorizationOptions>(options =>
        {
            options.AddPolicy("PrismStrictIsolation", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PrismTenantRequirement());
            });

            options.AddPolicy("PrismAdmins", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PrismAdminRequirement());
            });
        });

        builder.Services.Configure<PrismAdminOptions>(builder.Config.GetSection("Prism:AdminGroups"));

        // 8. Management API & Notifications
        // SEC-PT2-009 ANTIFORGERY POLICY (enforced in controllers, not globally):
        // - Browser form-POST endpoints (e.g. AccountController.Logout): [ValidateAntiForgeryToken]
        // - Capacitor mobile JSON API endpoints (Biometric, Push, Vinyl): [IgnoreAntiforgeryToken]
        //   Rationale: native apps cannot supply the ASP.NET Core antiforgery cookie+header pair.
        //   CSRF protection on those endpoints: SameSite=Lax + JSON Content-Type + origin checks.
        // Any new browser-facing POST endpoint MUST carry [ValidateAntiForgeryToken].
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, PrismMigrationHandler>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, PrismContentTypeSeeder>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, PrismStarterContentSeeder>();
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedHandler>();
        builder.Services.ConfigureOptions<PrismManagementApiConfiguration>();
    }
}
