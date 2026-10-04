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
        var mobileDiagnosticsEnabled = settings.MobileDiagnosticsEnabled;
        var pushNotificationsEnabled = settings.PushNotificationsEnabled;

        // Same host BuildAllowNavigationHosts treats as this app's own (startUri.Host there,
        // startUrl's own host here — both derived from the same tenant.Hostname). Uri.Host can
        // never contain a quote/backslash (DNS syntax forbids both), so no escaping is needed to
        // embed it safely in the Swift string literal below.
        var ownHost = new Uri(startUrl).Host;

        // A separate small file-scope declaration, prepended whole rather than spliced into the
        // middle of zoomFixInjection below — that big literal's own content (embedded JS closures)
        // contains stray "}}" sequences that would collide with C# raw-string interpolation syntax
        // if that literal were made interpolated itself. Kept as a plain build-time Swift constant
        // (referenced by name from a few small, unconditional insertions inside zoomFixInjection),
        // not a runtime toggle — see PrismNavigationHoldDelegate's own remarks for why the on-
        // screen diagnostic label needs a separate reveal gesture on top of this, and why this
        // flag exists as a distinct, deliberate opt-in at bundle-generation time (the
        // --mobile-diagnostics CLI flag / MobileDiagnosticsEnabled request field) rather than
        // always being compiled in: this determines whether ANY of that code is compiled into
        // this specific build at all, not just whether it's currently visible.
        var mobileDiagnosticsFlagDeclaration = $$"""
fileprivate enum PrismMobileDiagnosticsFlag {
    static let enabled = {{(mobileDiagnosticsEnabled ? "true" : "false")}}
}

fileprivate enum PrismOwnHost {
    // Read by PrismNavigationHoldDelegate.updateSafeAreaTopReservation (see its own remarks) to
    // tell this app's own pages — which size their own header tall enough to clear the status
    // bar/notch once the webview extends under it — apart from hosted content (Entra's sign-in
    // pages, or any other host in allowNavigation) that can't be trusted to do the same for
    // itself, so the webview's own frame reserves that strip on its behalf instead. The same
    // host BuildAllowNavigationHosts already treats as trusted, not a new concept.
    static let value = "{{ownHost}}"
}

""";

        var infoPlistInjection = biometricAuthEnabled
            ? BundleTemplates.Read("ios-info-plist-injection.sh")
            : string.Empty;

        // @capacitor-firebase/messaging needs: (1) UIBackgroundModes remote-notification so iOS
        // wakes the app for background pushes, (2) an aps-environment entitlement — "production"
        // is correct here, not "development", because this script feeds an App Store Connect
        // (TestFlight/production) build, never a plain Xcode debug-signed run (see
        // docs/PUSH_SETUP.md), (3) the entitlements file wired into CODE_SIGN_ENTITLEMENTS in
        // project.pbxproj (done in the .prism-add-push-config.mjs script below, alongside
        // GoogleService-Info.plist), and (4) an AppDelegate hook so the plugin can bridge the raw
        // APNs token it receives into an FCM token — patched onto AppDelegate.swift AFTER
        // zoomFixInjection (below) rewrites that file wholesale on every run, not spliced into
        // this literal, since a plain (non-interpolated) """ string can't reference this method's
        // own locals the way the infoPlistInjection/zoomFixInjection split already relies on.
        var pushNotificationsInjection = pushNotificationsEnabled
            ? BundleTemplates.Read("ios-push-injection.sh")
            : string.Empty;

        // Capacitor's own `zoomEnabled: false` (already the framework default we rely on — see
        // capacitor.config.ts's absence of the key) only disables the user's pinch-zoom gesture.
        // It does NOT stop WKWebView's own "zoom into a focused text input" behaviour, which
        // fires independently whenever a page's own viewport doesn't cap maximum-scale — reported
        // live on the Entra/ciamlogin.com sign-in page (hosted content we don't control and can't
        // add page-level CSS/viewport-meta to, same constraint as the contentInset fix above): the
        // password field triggered a zoomed-in, left-clipped layout. The only lever that reaches
        // hosted content is a native WKUserScript, injected via the documented Capacitor extension
        // point (CAPBridgeViewController.webViewConfiguration(for:), "recommended to call super's
        // implementation and modify the result") — so this subclasses it and rewires
        // Main.storyboard to use the subclass, the sanctioned way to add custom native iOS code to
        // a generated Capacitor project. Unconditional (not gated on biometricAuthEnabled) since
        // it's a general WebKit fix, not biometric-specific.
        //
        // CONFIRMED LIVE (a prior version of this fix shipped without the pbxproj step below and
        // was dead on arrival on a real device — blank black screen, no crash): Xcode does NOT
        // auto-discover new files dropped into a project folder (this generated project has no
        // fileSystemSynchronizedGroups). A .swift file written to disk but never added to
        // project.pbxproj's Sources build phase is silently excluded from compilation — with NO
        // build error, `xcodebuild ... build` succeeds regardless — and the storyboard's
        // customClass reference then fails to resolve at RUNTIME (NSClassFromString returns nil),
        // leaving a blank window with nothing rendered. Only a real simulator install+launch
        // caught this; a build-only check did not. The `xcode` npm package (see package.json)
        // programmatically adds the correct PBXBuildFile/PBXFileReference/Sources-phase entries —
        // proven end-to-end afterward via a real `xcrun simctl` install+launch+screenshot showing
        // the app's actual home screen, not just a successful compile.
        var zoomFixInjection = BundleTemplates.Read("ios-zoom-fix.sh")
            .Replace("@@PRISM_DIAGNOSTICS_DECLARATIONS@@", mobileDiagnosticsFlagDeclaration);

        return $$"""
#!/usr/bin/env bash
set -euo pipefail

echo "Bootstrapping iOS project..."

npm install
bash scripts/doctor-mobile.sh ios

if ! npx cap ls | grep -qi "ios"; then
  echo "Adding iOS platform..."
  npx cap add ios
fi

npx cap sync ios

echo "Generating app icon and splash screen from resources/icon.svg..."
npx capacitor-assets generate --ios
{{infoPlistInjection}}{{zoomFixInjection}}{{pushNotificationsInjection}}
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
}
