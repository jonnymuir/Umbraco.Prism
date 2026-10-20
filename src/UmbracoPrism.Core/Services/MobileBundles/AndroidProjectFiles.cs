using System.Text;
using UmbracoPrism.Core.Persistence;
using static UmbracoPrism.Core.Services.MobileBundles.BundleText;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// The Android side: the bootstrap script that patches the generated Gradle project, and the manifest additions.
/// </summary>
internal static class AndroidProjectFiles
{
    internal static string BuildBootstrapAndroidScript(MobileBundleSettings settings)
    {
        var appId = settings.AppId;
        var biometricAuthEnabled = settings.BiometricAuthEnabled;
        var pushNotificationsEnabled = settings.PushNotificationsEnabled;

        var manifestInjection =
            (biometricAuthEnabled ? BundleTemplates.Read("android-biometric-manifest-injection.sh") : string.Empty)
            + (settings.DeviceCaptureEnabled ? BundleTemplates.Read("android-device-capture-manifest-injection.sh") : string.Empty);

        // @capacitor-firebase/messaging's own Android gradle scripts apply the
        // com.google.gms.google-services plugin automatically once google-services.json is
        // present — no manual build.gradle edit needed here, unlike biometric's manifest
        // permission. Only responsibility left to this script is getting the file into place.
        var pushInjection = pushNotificationsEnabled
            ? BundleTemplates.Read("android-push-injection.sh")
            : string.Empty;

        // The native tab shell replaces Capacitor's MainActivity: a bottom navigation bar with one
        // Capacitor bridge per tab. Plain Java, since the app module Capacitor generates has no
        // Kotlin plugin applied.
        var nativeShell = BundleTemplates.Read("android-native-shell.sh")
            .Replace("@@PRISM_JAVA_DIR@@", appId.Replace('.', '/'))
            .Replace("@@PRISM_PACKAGE@@", appId)
            .Replace("@@PRISM_START_URL@@", CapacitorProjectFiles.AddPrismMobileQueryFlag(settings.StartUrl));

        return $$"""
#!/usr/bin/env bash
set -euo pipefail

echo "Bootstrapping Android project..."

npm install
bash scripts/doctor-mobile.sh android

if [ ! -d android ]; then
  echo "Adding Android platform..."
  npx cap add android
fi

# Upgrade Gradle wrapper to 8.14 for Java 25+ compatibility
GRADLE_WRAPPER="android/gradle/wrapper/gradle-wrapper.properties"
if [ -f "$GRADLE_WRAPPER" ]; then
  echo "Upgrading Gradle wrapper to 8.14 (Java 25 compatible)..."
  sed -i.bak 's|distributionUrl=.*|distributionUrl=https\\://services.gradle.org/distributions/gradle-8.14-all.zip|' "$GRADLE_WRAPPER"
  rm -f "$GRADLE_WRAPPER.bak"
  echo "✓ Gradle wrapper upgraded to 8.14"
fi

# Google Play has required every upload to target Android 16 (API 36) since 31 Aug 2026 and
# rejects anything lower outright ("edits.commit" 400, misleadingly worded "Target SDK of
# artifact is too low: <versionCode>" — that number is the artifact's versionCode, not an SDK
# level, found live on this pipeline's first real Play upload). Capacitor 7's own
# `cap add android` template still defaults compileSdkVersion/targetSdkVersion to 35, so bump
# both here rather than waiting on an upstream Capacitor release.
VARIABLES_GRADLE="android/variables.gradle"
if [ -f "$VARIABLES_GRADLE" ]; then
  echo "Bumping compileSdkVersion/targetSdkVersion to 36 (Google Play requires API 36+ for uploads since Aug 2026)..."
  sed -i.bak -E 's/(compileSdkVersion = )[0-9]+/\136/; s/(targetSdkVersion = )[0-9]+/\136/' "$VARIABLES_GRADLE"
  rm -f "$VARIABLES_GRADLE.bak"
  echo "✓ compileSdkVersion/targetSdkVersion bumped to 36"
fi

npx cap sync android

echo "Generating app icon and splash screen from resources/icon.svg..."
npx capacitor-assets generate --android
{{manifestInjection}}{{pushInjection}}{{nativeShell}}
if [[ "${CI:-}" == "true" ]]; then
  echo "CI environment detected — skipping emulator run/open. The android/ project is synced and"
  echo "ready for a signing/build step (e.g. ./gradlew bundleRelease) to take over from here."
elif command -v adb >/dev/null 2>&1 && adb devices | tail -n +2 | grep -q "device"; then
  echo "Android device/emulator found. Running app..."
  npx cap run android
else
  echo "No running Android emulator/device found. Opening Android Studio project..."
  npx cap open android
  echo "Tip: start an emulator/device, then run: npx cap run android"
fi
""";
    }

    internal static string BuildAndroidManifestAdditions()
    {
        return BundleTemplates.Read("android-manifest-additions.xml");
    }
}
