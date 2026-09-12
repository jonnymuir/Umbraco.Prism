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
using Microsoft.AspNetCore.Antiforgery;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Notifications;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Persistence;
using Umbraco.Extensions;

namespace UmbracoPrism.Core;

public class PrismComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // 1. Core Services
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ISecretVaultService, SecretVaultService>();
        builder.Services.AddSingleton<ITenantTokenResolver, TenantTokenResolver>();
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
        builder.Services.AddSingleton<IPrismTenantBindingValidator, PrismTenantBindingValidator>();
        builder.Services.AddScoped<IPrismContext, PrismContext>();
        builder.Services.AddScoped<IPrismUserContext, PrismUserContext>();
        builder.Services.AddScoped<IPrismNotificationService, PrismNotificationService>();
        builder.Services.Configure<PrismConfiguration>(
            builder.Config.GetSection(PrismConfiguration.SectionName));
        // 3. Middleware Registration
        // ForwardedHeaders must run before any middleware that reads RemoteIpAddress
        // (e.g. biometric rate limiting in BiometricController). Trust is opt-in, via
        // Prism:ForwardedHeaders (see PrismForwardedHeadersOptions) — ASP.NET Core's own
        // loopback-only default applies to any address a deployment doesn't explicitly name,
        // so RemoteIpAddress stays trustworthy (and the biometric-exchange rate limiter
        // meaningful) until a deployment actually sits behind a real, named proxy.
        builder.Services.Configure<PrismForwardedHeadersOptions>(
            builder.Config.GetSection(PrismForwardedHeadersOptions.SectionName));
        builder.Services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<PrismForwardedHeadersOptions>>(
                (options, prismOptions) => prismOptions.Value.ApplyTo(options));

        // SEC-PT2-004: Security response headers — configurable via Prism:SecurityHeaders.
        // Defaults: X-Content-Type-Options, X-Frame-Options (SAMEORIGIN), Referrer-Policy,
        // Permissions-Policy, HSTS (HTTPS only), CSP-Report-Only (promote to enforced CSP
        // once tuned per-deployment). Backoffice paths excluded by default.
        builder.Services.Configure<PrismSecurityHeadersOptions>(
            builder.Config.GetSection(PrismSecurityHeadersOptions.SectionName));

        // ASP.NET Core's own antiforgery middleware sets X-Frame-Options: SAMEORIGIN itself,
        // automatically, on any response where a token gets issued (IAntiforgery
        // .GetAndStoreTokens) — an easy-to-miss built-in behaviour, not something either
        // Wayfinder.Umbraco or this repo's own code asks for. Found live: every page that
        // mints an antiforgery token (the citizen stage form, the caseworker worklist) sent
        // X-Frame-Options TWICE — once from here, once from the framework's own default.
        // Two independent, identically-valued instances of the same header is a real
        // clickjacking-protection regression, not a cosmetic duplicate: some browsers treat a
        // header sent more than once as untrustworthy and ignore it entirely rather than pick
        // one value. PrismSecurityHeadersOptions.FrameOptions already covers this
        // (configurably, including the ability to omit it) — suppress the framework's own
        // unconditional, unconfigurable copy so there is exactly one source of truth.
        //
        // A second, unrelated framework default fixed in the same place: AntiforgeryOptions's
        // own Cookie.SecurePolicy defaults to CookieSecurePolicy.None, not SameAsRequest as its
        // sibling cookie-auth handler does (confirmed: `new AntiforgeryOptions().Cookie
        // .SecurePolicy` is `None` out of the box) — so the antiforgery cookie itself ships
        // with no Secure flag on every page that mints a token, found live via the same DAST
        // scan (Cookie Without Secure Flag [10011]) on both /apply-for-a-juggling-licence and
        // /caseworker-queue. TestSite (and every real deployment) is HTTPS-only, so there is no
        // legitimate plain-HTTP case this cookie needs to survive — Always, matching
        // PrismMemberCookie's own SecurePolicy above.
        builder.Services.Configure<AntiforgeryOptions>(options =>
        {
            options.SuppressXFrameOptionsHeader = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter(
                "PrismTenantResolution",
                app =>
                {
                    app.UseForwardedHeaders();
                    app.UseMiddleware<PrismSecurityHeadersMiddleware>();
                    app.UseMiddleware<PrismTenantMiddleware>();
                    app.UseMiddleware<PrismMobileCookieMiddleware>();
                }
            ));
        });

        // 4. Authorization Handler
        // PrismTenantHandler stays Singleton, like every other IAuthorizationHandler here —
        // it resolves the Scoped IPrismContext per-call via IHttpContextAccessor rather than
        // constructor-injecting it (see that class's own remarks: a Scoped IAuthorizationHandler
        // trips .NET's ValidateOnBuild graph validator over unrelated Umbraco-framework
        // singletons that directly constructor-inject the Scoped IAuthorizationService).
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

            // Tenant-binding enforcement, applied automatically to every PrismMemberCookie
            // request, not opt-in per controller. Runs on every request that carries the
            // cookie; PrismTenantMiddleware (registered as a PrePipeline filter, above any
            // Umbraco middleware including this one) has already resolved IPrismContext.CurrentTenant
            // by the time this fires. A principal whose own tenant claims don't match the
            // hostname-resolved tenant is rejected — the request continues as anonymous rather
            // than as the wrong tenant's member. Endpoints that want a hard 403 instead of an
            // anonymous fallback declare the "PrismStrictIsolation" policy explicitly on top
            // of this (see PrismTenantHandler), sharing the same IPrismTenantBindingValidator
            // check rather than reimplementing it.
            cookieOptions.Events.OnValidatePrincipal = context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<IPrismTenantBindingValidator>();
                var prismContext = context.HttpContext.RequestServices.GetRequiredService<IPrismContext>();

                if (prismContext.CurrentTenant is null ||
                    context.Principal is null ||
                    !validator.IsBound(context.Principal, prismContext.CurrentTenant))
                {
                    context.RejectPrincipal();
                }

                return Task.CompletedTask;
            };
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

            // Wayfinder.Umbraco's own ServiceRequestPolling policy is no longer registered
            // here — Prism.Core carries no Wayfinder dependency at all now (see
            // UmbracoPrism.MockBusinessApp's own narrowing to a genuine support system). A host
            // that installs Wayfinder.Umbraco directly (e.g. UmbracoPrism.TestSite) registers
            // that policy itself, supplying whatever authentication scheme its own member/user
            // model uses.
        });

        builder.Services.Configure<PrismAdminOptions>(builder.Config.GetSection("Prism:AdminGroups"));

        // 8. Management API & Notifications
        // SEC-PT2-009 ANTIFORGERY POLICY (enforced in controllers, not globally):
        // - Browser form-POST endpoints (e.g. AccountController.Logout): [ValidateAntiForgeryToken]
        // - Capacitor mobile JSON API endpoints (Biometric, Push, Vinyl): [IgnoreAntiforgeryToken]
        //   Rationale: native apps cannot supply the ASP.NET Core antiforgery cookie+header pair.
        //   CSRF protection on those endpoints: SameSite=Lax + JSON Content-Type + origin checks.
        // Any new browser-facing POST endpoint MUST carry [ValidateAntiForgeryToken].
        // Registered as a proper Umbraco package migration plan (runs during Umbraco's own
        // boot/upgrade phase, gated behind its "upgrading, please wait" holding page like
        // Umbraco's own core migrations) rather than reactively on UmbracoApplicationStartedNotification
        // — that notification fires once Umbraco has ALREADY reached RuntimeLevel.Run and started
        // serving real traffic, so a reactive handler races every request against its own table
        // creation. Found live via a CI flake: "no such table: PrismTenants" on tenant-dependent
        // pages, reproducing even on main, whenever a request landed in the window between
        // Umbraco reaching Run and the old PrismMigrationHandler's migration finishing. This
        // registration makes that race structurally impossible instead of papering over it.
        builder.PackageMigrationPlans().Add<PrismMigrationPlan>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, PrismContentTypeSeeder>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, PrismStarterContentSeeder>();
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedHandler>();
        builder.Services.ConfigureOptions<PrismManagementApiConfiguration>();
    }
}
