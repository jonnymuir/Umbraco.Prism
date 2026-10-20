using Microsoft.Extensions.DependencyInjection.Extensions;
﻿using Umbraco.Cms.Core.Composing;
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
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Notifications;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Filters;
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
        builder.Services.AddSingleton<IPrismPageAccessResolver, PrismPageAccessResolver>();
        builder.Services.AddSingleton<IBrandingService, BrandingService>();
        builder.Services.AddSingleton<IMobileBundleService, MobileBundleService>();
        builder.Services.TryAddSingleton<IPrismMobileTabsProvider, PrismDefaultMobileTabsProvider>();
        builder.Services.AddDistributedMemoryCache().AddSingleton<PrismMobileSignInCodes>();
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
        // Singleton, not scoped: PrismNotificationService holds no per-request state — its
        // constructor deps (IUmbracoDatabaseFactory, IConfiguration, ILogger) are all
        // singleton-safe, and every method opens its own short-lived IUmbracoDatabase via
        // _databaseFactory.CreateDatabase() rather than holding one across calls. Needs to be a
        // singleton specifically so UmbracoPrism.Automate's PrismSendPushNotificationAction (an
        // Umbraco Automate [Action], singleton-lifetime by that package's own convention) can
        // consume it directly — a scoped service consumed by a singleton fails ASP.NET Core's
        // own ValidateOnBuild captive-dependency check at startup.
        builder.Services.AddSingleton<IPrismNotificationService, PrismNotificationService>();
        builder.Services.Configure<PrismConfiguration>(
            builder.Config.GetSection(PrismConfiguration.SectionName));
        // 3. Middleware Registration
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter(
                "PrismTenantResolution",
                app =>
                {
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
        
        // 6. Member sign-in, confined to tenants that have it configured
        builder.Services.AddPrismMemberSignIn();

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
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, PrismConfiguredTenantSeeder>();
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedHandler>();
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismPageAccessRouteRefreshHandler>();
        builder.Services.ConfigureOptions<PrismManagementApiConfiguration>();

        // Global resource filter enforcing IPrismPageAccessResolver on every content-routed
        // request — see PrismPageAccessFilter's own remarks on why an MVC filter (not a raw
        // app.Use(...) middleware) is the pipeline-position-safe way to do this.
        builder.Services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<PrismPageAccessFilter>();
        });
    }
}
