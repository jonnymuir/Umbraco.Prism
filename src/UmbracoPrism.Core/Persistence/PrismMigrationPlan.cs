using Umbraco.Cms.Core.Packaging;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration plan for the Umbraco Prism package.
/// </summary>
public class PrismMigrationPlan : PackageMigrationPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrismMigrationPlan"/> class.
    /// </summary>
    public PrismMigrationPlan() : base("UmbracoPrism")
    {
    }

    /// <summary>
    /// Defines the migration plan.
    /// </summary>
    protected override void DefinePlan()
    {
        To<CreatePrismTables>("initial-state")
        .To<AddIdentityColumns>("add-identity-cols")
        .To<AddBrandingOverridesColumn>("add-branding-overrides")
        .To<AddMobileBrandingOverridesColumn>("add-mobile-branding-overrides")
        .To<AddMobileAppConfigColumn>("add-mobile-app-config")
        .To<CreatePrismDeviceCredentialsTable>("add-device-credentials")
        .To<AddRefreshTokenEncColumn>("add-refresh-token-enc")
        .To<AddAllowBiometricLoginColumn>("add-allow-biometric-login")
        .To<AddPushTokenColumn>("add-push-token")
        .To<CreatePrismNotificationSubscriptionsTable>("add-notification-subscriptions")
        .To<DropThemeColorColumn>("drop-theme-color")
        .To<AddOidcAuthorityColumns>("add-oidc-authority-columns")
        .To<AddOidcSecretProviderColumns>("add-oidc-secret-provider-columns");
    }
}
