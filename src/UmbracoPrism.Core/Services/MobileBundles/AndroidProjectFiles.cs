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

        var manifestInjection = biometricAuthEnabled
            ? BundleTemplates.Read("android-biometric-manifest-injection.sh")
            : string.Empty;

        // @capacitor-firebase/messaging's own Android gradle scripts apply the
        // com.google.gms.google-services plugin automatically once google-services.json is
        // present — no manual build.gradle edit needed here, unlike biometric's manifest
        // permission. Only responsibility left to this script is getting the file into place.
        var pushInjection = pushNotificationsEnabled
            ? BundleTemplates.Read("android-push-injection.sh")
            : string.Empty;

        // See PrismIdentityCookiePlugin's own remarks (BuildBootstrapIosScript, same file) for
        // the underlying problem — this is Android's own equivalent. No per-host cookie removal
        // API exists on android.webkit.CookieManager, unlike iOS's WKWebsiteDataStore, so this
        // clears the WebView's whole cookie jar instead of targeting specific hosts — safe here
        // because this single-purpose app WebView has no other cookie worth preserving across
        // sign-out. Unconditional, not gated on biometricAuthEnabled — matches
        // prism-biometric-signout.js's own reasoning: this matters for every mobile sign-out,
        // not just biometric-enabled tenants.
        //
        // MainActivity.java is rewritten wholesale, not patched with a regex — same precedent as
        // AppDelegate.swift/PrismBridgeViewController.swift on iOS (BuildBootstrapIosScript,
        // same file): Capacitor's own default MainActivity.java has no content beyond the bare
        // class declaration, so nothing is lost, and a blind regex against a file whose exact
        // current content isn't verified in this build pipeline would be guesswork. registerPlugin()
        // must run before super.onCreate() — Capacitor's own documented pattern for wiring in a
        // plugin autoRegisterPlugins' own classpath discovery won't find.
        //
        // CONFIRMED LIVE (a prior version of this fix wrote a .kt file here — dead on arrival on
        // the very first CI dispatch: "cannot find symbol: class PrismIdentityCookiePlugin" from
        // MainActivity.java's own compileDebugJavaWithJavac task). Capacitor's default `cap add
        // android` app module applies no Kotlin Gradle plugin at all — it's pure Java — so a .kt
        // file under src/main/java is silently never compiled by anything; javac just doesn't see
        // it. Plain Java instead, matching MainActivity.java's own toolchain exactly, sidesteps
        // this rather than also patching build.gradle to add a Kotlin plugin this project doesn't
        // otherwise need.
        var javaPackagePath = appId.Replace('.', '/');
        var cookiePluginInjection = $$"""

echo "Writing PrismIdentityCookiePlugin.java and registering it in MainActivity..."
JAVA_DIR="android/app/src/main/java/{{javaPackagePath}}"
if [ -d "$JAVA_DIR" ]; then
  cat > "$JAVA_DIR/PrismIdentityCookiePlugin.java" << 'PRISM_COOKIE_PLUGIN_EOF'
package {{appId}};

import android.webkit.CookieManager;
import android.webkit.ValueCallback;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

@CapacitorPlugin(name = "PrismIdentityCookiePlugin")
public class PrismIdentityCookiePlugin extends Plugin {
  @PluginMethod
  public void clearCookies(PluginCall call) {
    final CookieManager cookieManager = CookieManager.getInstance();
    cookieManager.removeAllCookies(new ValueCallback<Boolean>() {
      @Override
      public void onReceiveValue(Boolean value) {
        cookieManager.flush();
        call.resolve();
      }
    });
  }
}
PRISM_COOKIE_PLUGIN_EOF
  echo "✓ PrismIdentityCookiePlugin.java written"

  cat > "$JAVA_DIR/MainActivity.java" << 'PRISM_MAINACTIVITY_EOF'
package {{appId}};

import android.os.Bundle;
import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {
  @Override
  public void onCreate(Bundle savedInstanceState) {
    registerPlugin(PrismIdentityCookiePlugin.class);
    super.onCreate(savedInstanceState);
  }
}
PRISM_MAINACTIVITY_EOF
  echo "✓ MainActivity.java rewritten to register PrismIdentityCookiePlugin"
else
  echo "⚠️ $JAVA_DIR not found. Run 'npx cap add android' first."
fi

""";

        return $$"""
#!/usr/bin/env bash
set -euo pipefail

echo "Bootstrapping Android project..."

npm install
bash scripts/doctor-mobile.sh android

if ! npx cap ls | grep -qi "android"; then
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
{{manifestInjection}}{{pushInjection}}{{cookiePluginInjection}}
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
