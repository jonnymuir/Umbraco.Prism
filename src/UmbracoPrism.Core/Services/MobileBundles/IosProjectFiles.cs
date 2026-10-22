using System.Text;
using UmbracoPrism.Core.Persistence;
using static UmbracoPrism.Core.Services.MobileBundles.BundleText;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// The iOS side: the bootstrap script that patches the generated Xcode project (see the template files it composes), the localhost-cert trust script and the Info.plist additions.
/// </summary>
internal static class IosProjectFiles
{
    internal static string BuildBootstrapIosScript(MobileBundleSettings settings)
    {
        var startUrl = settings.StartUrl;
        var biometricAuthEnabled = settings.BiometricAuthEnabled;
        var pushNotificationsEnabled = settings.PushNotificationsEnabled;

        var infoPlistInjection =
            (biometricAuthEnabled ? BundleTemplates.Read("ios-info-plist-injection.sh") : string.Empty)
            + (settings.DeviceCaptureEnabled ? BundleTemplates.Read("ios-info-plist-device-capture.sh") : string.Empty);

        // @capacitor-firebase/messaging needs: (1) UIBackgroundModes remote-notification so iOS
        // wakes the app for background pushes, (2) an aps-environment entitlement — "production"
        // is correct here, not "development", because this script feeds an App Store Connect
        // (TestFlight/production) build, never a plain Xcode debug-signed run (see
        // docs/PUSH_SETUP.md), (3) the entitlements file wired into CODE_SIGN_ENTITLEMENTS in
        // project.pbxproj (done in the .prism-add-push-config.mjs script below, alongside
        // GoogleService-Info.plist), and (4) an AppDelegate hook so the plugin can bridge the raw
        // APNs token it receives into an FCM token, patched onto AppDelegate.swift after the native
        // shell (below) rewrites that file on every run.
        var pushNotificationsInjection = pushNotificationsEnabled
            ? BundleTemplates.Read("ios-push-injection.sh")
            : string.Empty;

        var nativeShell = BundleTemplates.Read("ios-native-shell.sh");

        var journeyStatus = settings.JourneyStatusEnabled
            ? BundleTemplates.Read("ios-journey-status.sh")
            : string.Empty;

        var savedPages = settings.SavedPagesEnabled
            ? BundleTemplates.Read("ios-saved-pages.sh").Replace("@@PRISM_APP_BOUND_DOMAINS@@", string.Join(' ', AppBoundDomains(settings)))
            : string.Empty;

        return $$"""
#!/usr/bin/env bash
set -euo pipefail

echo "Bootstrapping iOS project..."

npm install
bash scripts/doctor-mobile.sh ios

if [ ! -d ios ]; then
  echo "Adding iOS platform..."
  npx cap add ios
fi

npx cap sync ios

echo "Generating app icon and splash screen from resources/icon.svg..."
npx capacitor-assets generate --ios
{{BundleTemplates.Read("ios-launch-screen.sh")}}{{infoPlistInjection}}{{savedPages}}{{nativeShell}}{{pushNotificationsInjection}}{{journeyStatus}}{{BundleTemplates.Read("ios-privacy-manifest.sh")}}
echo "Applying localhost cert trust (if needed)..."
if ! bash scripts/trust-ios-localhost-cert.sh; then
  echo "⚠️ Cert trust step did not complete. Continuing..."
fi

if [[ "${CI:-}" == "true" ]]; then
  echo "CI environment detected — skipping simulator run/open. The ios/ project is synced and"
  echo "ready for a signing/archive step (e.g. xcodebuild) to take over from here."
elif xcrun simctl list devices booted | grep -q "(Booted)"; then
  echo "Booted simulator found. Running app..."
  npx cap run ios
else
  echo "No booted simulator found. Opening Xcode project..."
  npx cap open ios
  echo "Tip: boot a simulator, then run: npx cap run ios"
fi
""";
    }

    /// <summary>
    /// The domains the app's webviews may load once saved pages limit them to the app's own: the
    /// site, and the identity provider's sign-in pages for a tenant that signs in inside the app.
    /// iOS allows at most 10, without wildcards.
    /// </summary>
    internal static IReadOnlyList<string> AppBoundDomains(MobileBundleSettings settings)
    {
        var tenant = settings.Tenant;
        var hosts = new List<string> { new Uri(settings.StartUrl).Host };

        if (Uri.TryCreate(tenant.OidcAuthority?.Trim(), UriKind.Absolute, out var authority))
        {
            hosts.Add(authority.Host);
        }

        var entraTenantId = tenant.EntraTenantId?.Trim();
        if (!string.IsNullOrWhiteSpace(entraTenantId))
        {
            hosts.Add("login.microsoftonline.com");
            hosts.Add($"{entraTenantId}.ciamlogin.com");
            hosts.Add($"{entraTenantId}.b2clogin.com");
        }

        return [.. hosts.Select(host => host.ToLowerInvariant()).Distinct().Take(10)];
    }

    internal static string BuildTrustIosLocalhostCertScript(MobileBundleSettings settings)
    {
        var startUrl = settings.StartUrl;

        var uri = new Uri(startUrl);
        var host = uri.Host;
        var port = uri.IsDefaultPort
            ? (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : uri.Port;

        return $$"""
#!/usr/bin/env bash
set -euo pipefail

HOST="{{host}}"
PORT="{{port}}"

if [[ "$HOST" != "localhost" && "$HOST" != "127.0.0.1" && "$HOST" != "::1" ]]; then
  echo "Configured host '$HOST' is not localhost. No simulator cert trust step needed."
  exit 0
fi

if ! xcrun simctl list devices booted | grep -q "(Booted)"; then
  echo "No booted iOS simulator found. Boot one first, then rerun this script."
  exit 1
fi

CERT_PATH="/tmp/prism-localhost-$PORT.cer"

echo "Extracting certificate from https://$HOST:$PORT ..."
echo | openssl s_client -connect "$HOST:$PORT" -servername "$HOST" -showcerts 2>/dev/null \
  | awk '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/{print}' > "$CERT_PATH"

if [[ ! -s "$CERT_PATH" ]]; then
  echo "Could not extract certificate from https://$HOST:$PORT."
  echo "Ensure your local site is running with HTTPS before retrying."
  exit 1
fi

echo "Adding certificate to booted simulator keychain..."
xcrun simctl keychain booted add-root-cert "$CERT_PATH"

echo "Done. Re-run: npx cap run ios"
""";
    }

    internal static string BuildIosInfoPlistAdditions(MobileBundleSettings settings)
    {
        var appName = settings.AppName;

        return $$"""
<?xml version="1.0" encoding="UTF-8"?>
<!--
  iOS Info.plist additions for biometric authentication.
  The bootstrap-ios.sh script injects these automatically.
  If you need to add them manually, merge these keys into ios/App/App/Info.plist.
-->
<dict>
  <key>NSFaceIDUsageDescription</key>
  <string>{{EscapeSingleQuotes(appName)}} uses Face ID to securely log you in without requiring your password each time.</string>
</dict>
""";
    }

    /// <summary>
    /// The app's own <c>PrivacyInfo.xcprivacy</c>, which App Store Connect requires. Capacitor's
    /// frameworks ship their own manifests; this one covers the shell's Swift (which reads and
    /// writes <c>UserDefaults</c>) and the data the enabled features collect. A tenant whose site
    /// collects more must add it here and in App Store Connect's privacy details.
    /// </summary>
    internal static string BuildPrivacyManifest(MobileBundleSettings settings)
    {
        var collected = new List<string> { "NSPrivacyCollectedDataTypeName", "NSPrivacyCollectedDataTypeEmailAddress" };
        if (settings.PushNotificationsEnabled)
            collected.Add("NSPrivacyCollectedDataTypeDeviceID");
        if (settings.DeviceCaptureEnabled)
        {
            collected.Add("NSPrivacyCollectedDataTypePhotosorVideos");
            collected.Add("NSPrivacyCollectedDataTypePreciseLocation");
        }

        var collectedEntries = string.Concat(collected.Select(type => $$"""

    <dict>
      <key>NSPrivacyCollectedDataType</key>
      <string>{{type}}</string>
      <key>NSPrivacyCollectedDataTypeLinked</key>
      <true/>
      <key>NSPrivacyCollectedDataTypeTracking</key>
      <false/>
      <key>NSPrivacyCollectedDataTypePurposes</key>
      <array>
        <string>NSPrivacyCollectedDataTypePurposeAppFunctionality</string>
      </array>
    </dict>
"""));

        return $$"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>NSPrivacyTracking</key>
  <false/>
  <key>NSPrivacyTrackingDomains</key>
  <array/>
  <key>NSPrivacyCollectedDataTypes</key>
  <array>{{collectedEntries}}
  </array>
  <key>NSPrivacyAccessedAPITypes</key>
  <array>
    <dict>
      <key>NSPrivacyAccessedAPIType</key>
      <string>NSPrivacyAccessedAPICategoryUserDefaults</string>
      <key>NSPrivacyAccessedAPITypeReasons</key>
      <array>
        <string>CA92.1</string>
      </array>
    </dict>
  </array>
</dict>
</plist>
""";
    }
}
