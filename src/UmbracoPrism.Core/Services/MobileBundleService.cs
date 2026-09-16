using System.IO.Compression;
using System.Text;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Produces Prism mobile starter bundles with tenant-specific runtime and identity configuration.
/// </summary>
public class MobileBundleService : IMobileBundleService
{
  /// <summary>
  /// Builds a ZIP archive containing a Capacitor app scaffold for a tenant.
  /// </summary>
  /// <param name="tenant">Tenant record used to derive default host and Entra settings.</param>
  /// <param name="request">Bundle generation options provided from the backoffice workflow.</param>
  /// <param name="cancellationToken">Cancellation token for bundle generation.</param>
  /// <returns>ZIP archive bytes for download.</returns>
  /// <exception cref="ArgumentException">Thrown when request input contains invalid app identifiers or URLs.</exception>
    public Task<byte[]> BuildBundleAsync(PrismTenantSchema tenant, PrismMobileBundleRequest request, CancellationToken cancellationToken = default)
    {
        var appName = string.IsNullOrWhiteSpace(request.AppName) ? tenant.Name : request.AppName.Trim();
        var appId = string.IsNullOrWhiteSpace(request.AppId)
            ? $"com.prism.{ToSafeIdentifier(tenant.Name)}"
            : request.AppId.Trim();
        var version = string.IsNullOrWhiteSpace(request.Version) ? "1.0.0" : request.Version.Trim();
        var marker = string.IsNullOrWhiteSpace(request.UserAgentMarker) ? "PrismMobile" : request.UserAgentMarker.Trim();
        var startUrl = BuildStartUrl(request.StartUrl, tenant.Hostname);
        var iconUrl = RewriteMediaHost(request.IconUrl?.Trim(), startUrl);
        var splashUrl = RewriteMediaHost(request.SplashUrl?.Trim(), startUrl);
        var errorBackgroundColor = string.IsNullOrWhiteSpace(request.ErrorBackgroundColor) ? "#0f172a" : request.ErrorBackgroundColor.Trim();
        var errorTextColor = string.IsNullOrWhiteSpace(request.ErrorTextColor) ? "#f8fafc" : request.ErrorTextColor.Trim();
        var errorTitle = string.IsNullOrWhiteSpace(request.ErrorTitle) ? "We’re having trouble connecting" : request.ErrorTitle.Trim();
        var errorMessage = string.IsNullOrWhiteSpace(request.ErrorMessage) ? "Please check your connection and try again." : request.ErrorMessage.Trim();
        var showErrorDiagnostics = request.ShowErrorDiagnostics ?? true;
        var biometricAuthEnabled = request.BiometricAuthEnabled ?? false;
        var mobileDiagnosticsEnabled = request.MobileDiagnosticsEnabled ?? false;

        if (!IsValidAppId(appId))
        {
            throw new ArgumentException("App ID must be a reverse-domain identifier, e.g. com.example.portal");
        }

        if (!string.IsNullOrWhiteSpace(iconUrl) && iconUrl.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Icon URL must point to a raster image (PNG or JPG). SVG files cannot be converted by the image pipeline. Please export your icon as a 1024×1024 PNG.");
        }

        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
          AddEntry(archive, "README.md", BuildReadme(appName, startUrl, iconUrl, splashUrl, biometricAuthEnabled));
            AddEntry(archive, "package.json", BuildPackageJson(appName, biometricAuthEnabled));
            AddEntry(archive, "AGENT_PROMPT.md", BuildAgentPrompt(appName, startUrl, biometricAuthEnabled));
            AddEntry(archive, "capacitor.config.ts", BuildCapacitorConfig(tenant, appId, appName, version, startUrl, marker));
            AddEntry(archive, ".gitignore", "node_modules\nandroid\nios\n.DS_Store\n");
            AddEntry(archive, "www/index.html", BuildPlaceholderIndex(appName, startUrl, errorBackgroundColor, errorTextColor, errorTitle, errorMessage, showErrorDiagnostics, biometricAuthEnabled));
            AddEntry(archive, "www/mobile-overrides.css", BuildMobileOverrideTemplate());
            AddEntry(archive, "scripts/doctor-mobile.sh", BuildDoctorScript(startUrl));
            AddEntry(archive, "scripts/bootstrap-ios.sh", BuildBootstrapIosScript(startUrl, biometricAuthEnabled, mobileDiagnosticsEnabled));
            AddEntry(archive, "scripts/bootstrap-android.sh", BuildBootstrapAndroidScript(biometricAuthEnabled));
            AddEntry(archive, "scripts/trust-ios-localhost-cert.sh", BuildTrustIosLocalhostCertScript(startUrl));
          AddEntry(archive, "resources/mobile-assets.json", BuildAssetsManifest(iconUrl, splashUrl, errorBackgroundColor, errorTextColor, errorTitle, errorMessage, showErrorDiagnostics));

            // Default app icon (an opaque-background Umbraco Prism mark — @capacitor/assets
            // requires no alpha channel for the iOS App Store icon specifically). Without this,
            // every generated app ships Capacitor's own generic default icon, found live on the
            // first real TestFlight build. A tenant/implementer overrides it by replacing this
            // file (any square, opaque-background source @capacitor/assets accepts — PNG or SVG)
            // before running the bootstrap script; the generate step re-runs against whatever's
            // there.
            AddEntry(archive, "resources/icon.svg", BuildDefaultAppIconSvg());

            if (biometricAuthEnabled)
            {
                AddEntry(archive, "resources/ios-info-plist-additions.xml", BuildIosInfoPlistAdditions(appName));
                AddEntry(archive, "resources/android-manifest-additions.xml", BuildAndroidManifestAdditions());
            }
        }

        return Task.FromResult(memory.ToArray());
    }

    private static string BuildStartUrl(string? startUrl, string hostname)
    {
        if (!string.IsNullOrWhiteSpace(startUrl))
        {
            if (Uri.TryCreate(startUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return uri.ToString().TrimEnd('/');
            }

            throw new ArgumentException("Start URL must be an absolute URL, e.g. https://portal.example.com");
        }

        var host = hostname.Trim();
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return host.TrimEnd('/');
        }

        return $"https://{host}";
    }

    private static string? RewriteMediaHost(string? mediaUrl, string resolvedStartUrl)
    {
        if (string.IsNullOrEmpty(mediaUrl)) return mediaUrl;
        if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var mediaUri)) return mediaUrl;
        if (!mediaUri.IsLoopback) return mediaUrl;

        if (!Uri.TryCreate(resolvedStartUrl, UriKind.Absolute, out var originUri)) return mediaUrl;

        var builder = new UriBuilder(mediaUri)
        {
            Scheme = originUri.Scheme,
            Host = originUri.Host,
            Port = originUri.IsDefaultPort ? -1 : originUri.Port
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private static bool IsValidAppId(string appId)
    {
        var segments = appId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2) return false;

        return segments.All(segment => segment.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'));
    }

    private static string ToSafeIdentifier(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length == 0 || builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "tenant" : result;
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string BuildPackageJson(string appName, bool biometricAuthEnabled)
    {
        var biometricDeps = biometricAuthEnabled
            ? """
,
    "@aparajita/capacitor-biometric-auth": "^7.0.0",
    "@aparajita/capacitor-secure-storage": "^7.0.0"
"""
            : string.Empty;

        return $$"""
{
  "name": "{{ToSafeIdentifier(appName)}}-mobile",
  "private": true,
  "version": "1.0.0",
  "description": "Generated Prism mobile shell",
  "scripts": {
    "doctor": "bash scripts/doctor-mobile.sh",
    "bootstrap:ios": "bash scripts/bootstrap-ios.sh",
    "bootstrap:android": "bash scripts/bootstrap-android.sh",
    "sync": "npx cap sync",
    "run:ios": "npx cap run ios",
    "run:android": "npx cap run android",
    "open:ios": "npx cap open ios",
    "open:android": "npx cap open android"
  },
  "dependencies": {
    "@capacitor/core": "^7.0.0"{{biometricDeps}}
  },
  "devDependencies": {
    "@capacitor/cli": "^7.0.0",
    "@capacitor/android": "^7.0.0",
    "@capacitor/ios": "^7.0.0",
    "@capacitor/assets": "^3.0.0",
    "typescript": "^5.7.0",
    "xcode": "^3.0.1"
  }
}
""";
    }

    private static string BuildCapacitorConfig(PrismTenantSchema tenant, string appId, string appName, string version, string startUrl, string marker)
    {
      var uri = new Uri(startUrl);
      var mobileStartUrl = AddPrismMobileQueryFlag(startUrl);
      var cleartext = uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
      var allowNavigationHosts = BuildAllowNavigationHosts(tenant, uri);
      var allowNavigationJs = string.Join(", ", allowNavigationHosts.Select(host => $"'{EscapeSingleQuotes(host)}'"));

        return $$"""
import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: '{{appId}}',
  appName: '{{EscapeSingleQuotes(appName)}}',
  webDir: 'www',
  bundledWebRuntime: false,
  ios: {
    // History: 'automatic' (Capacitor's own default) caused a horizontal content-shift/touch-
    // offset bug in WKWebView's UIScrollView content-inset adjustment on the OIDC login screen
    // (Entra/ciamlogin.com) — hosted content this app can't add safe-area CSS to. 'never' (#219)
    // stopped that shift but disabled safe-area adjustment entirely, leaving hosted content
    // under the status bar/notch. 'always' (#248) reserved safe-area space unconditionally —
    // but confirmed live it STILL left hosted content (the same Entra page) rendering under the
    // status bar on a real device: contentInset only offsets a WKWebView's *scroll position*, it
    // doesn't move anything for content that doesn't scroll, or that a page positions outside
    // the normal flow — which a page this app doesn't control is free to do regardless of what
    // native inset is set.
    //
    // The actual fix is native, not a WKWebView content setting at all: PrismBridgeViewController
    // now pins the WKWebView's own frame to the safe-area layout guide (see bootstrap-ios.sh),
    // so the unsafe strip at the top/bottom is never part of the WebView's drawable area in the
    // first place, regardless of what any page — ours or a hosted IdP's — does with scroll or
    // positioning. contentInset goes back to 'never' here so the two mechanisms don't double up
    // (the frame already excludes the unsafe area; an inset on top of that would reserve it
    // twice).
    contentInset: 'never'
  },
  appendUserAgent: '{{EscapeSingleQuotes(marker)}}',
  server: {
    url: '{{EscapeSingleQuotes(mobileStartUrl)}}',
    cleartext: {{cleartext}},
    allowNavigation: [{{allowNavigationJs}}]
  },
  plugins: {
    SplashScreen: {
      launchAutoHide: true
    },
    StatusBar: {
      overlaysWebView: false,
      style: 'DEFAULT'
    }
  }
};

export default config;
""";
    }

  private static IReadOnlyList<string> BuildAllowNavigationHosts(PrismTenantSchema tenant, Uri startUri)
  {
    var hosts = new List<string>();

    AddHost(hosts, startUri.Authority);
    AddHost(hosts, startUri.Host);

    AddHost(hosts, "login.microsoftonline.com");
    AddHost(hosts, "*.ciamlogin.com");
    AddHost(hosts, "*.b2clogin.com");

    var entraTenantId = tenant.EntraTenantId?.Trim();
    if (!string.IsNullOrWhiteSpace(entraTenantId))
    {
      AddHost(hosts, $"{entraTenantId}.ciamlogin.com");
      AddHost(hosts, $"{entraTenantId}.b2clogin.com");
    }

    return hosts;
  }

  private static void AddHost(List<string> hosts, string? host)
  {
    if (string.IsNullOrWhiteSpace(host))
    {
      return;
    }

    if (!hosts.Contains(host, StringComparer.OrdinalIgnoreCase))
    {
      hosts.Add(host);
    }
  }

  private static string AddPrismMobileQueryFlag(string startUrl)
  {
    var uri = new Uri(startUrl);
    var builder = new UriBuilder(uri);
    var currentQuery = builder.Query;
    var trimmed = string.IsNullOrWhiteSpace(currentQuery) ? string.Empty : currentQuery.TrimStart('?');

    if (trimmed.Contains("prismMobile=", StringComparison.OrdinalIgnoreCase))
    {
      var updatedParts = trimmed
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.StartsWith("prismMobile=", StringComparison.OrdinalIgnoreCase) ? "prismMobile=1" : part);
      builder.Query = string.Join("&", updatedParts);
    }
    else
    {
      builder.Query = string.IsNullOrWhiteSpace(trimmed)
        ? "prismMobile=1"
        : $"{trimmed}&prismMobile=1";
    }

    return builder.Uri.ToString().TrimEnd('/');
  }

    private static string BuildReadme(string appName, string startUrl, string? iconUrl, string? splashUrl, bool biometricAuthEnabled)
    {
      var iconLine = string.IsNullOrWhiteSpace(iconUrl) ? "(not provided)" : iconUrl;
      var splashLine = string.IsNullOrWhiteSpace(splashUrl) ? "(not provided)" : splashUrl;

      var biometricSection = biometricAuthEnabled
          ? """

## Biometric Login Setup

This bundle was generated with **biometric authentication enabled**. The bootstrap scripts automatically inject
required platform entitlements, but you should verify the following prerequisites.

### iOS

- The `NSFaceIDUsageDescription` key is injected into `Info.plist` by `bootstrap-ios.sh`.
- Face ID / Touch ID must be enrolled on the device or simulator.
- **Simulator note:** `BiometricAuth.checkBiometry()` returns `isAvailable: false` on the iOS Simulator because
  it has no biometric hardware. Test biometric flows on a physical device or use the Simulator's
  *Features → Face ID → Enrolled* menu to enable a simulated match.

### Android

- The `USE_BIOMETRIC` permission is injected into `AndroidManifest.xml` by `bootstrap-android.sh`.
- A fingerprint or biometric credential must be enrolled on the device/emulator.
- **Emulator note:** enroll a simulated fingerprint via `adb emu finger touch 1`, then authenticate
  with the same command when prompted.

### Plugin packages

The following Capacitor plugins are included in `package.json`:

- `@aparajita/capacitor-biometric-auth` — biometric prompt and availability checks.
- `@aparajita/capacitor-secure-storage` — hardware-backed secure storage for tokens.

Both plugins auto-register via Capacitor's plugin discovery; no `capacitor.config.ts` changes are needed.
"""
          : string.Empty;

        return $$"""
# {{appName}} Mobile Shell

This bundle was generated by Umbraco Prism "Produce Mobile".

## Start URL

{{startUrl}}

## Quick start

1. Install dependencies:
   ```bash
   npm install
   ```
2. Run environment checks:

```bash
npm run doctor
```

3. Bootstrap iOS end-to-end:

```bash
npm run bootstrap:ios
```

Optional Android bootstrap:

```bash
npm run bootstrap:android
```

## Environment prerequisites

- Node.js 20+
- Xcode (for iOS)
- CocoaPods (for iOS)
- Android Studio + Android SDK (for Android)

Install CocoaPods on macOS:

```bash
brew install cocoapods
pod --version
```

## Common setup errors

- `[error] CocoaPods is not installed.`
  - Install CocoaPods, then rerun `npx cap add ios`.
- `[error] ios platform has not been added yet.`
  - Run `npx cap add ios` before `sync` or `open`.
- `[error] android platform has not been added yet.`
  - Run `npx cap add android` before `sync` or `open`.

### iOS localhost HTTPS (`NSURLErrorDomain -1202`)

If your `Start URL` is `https://localhost:<port>`, iOS simulator/device may reject the cert until it is trusted.

After `npx cap add ios`, run:

```bash
bash scripts/trust-ios-localhost-cert.sh
```

Then redeploy:

```bash
npx cap run ios
```

Notes:

- The script extracts the certificate from your current localhost endpoint and adds it to the booted simulator keychain.
- If no simulator is booted, open one first (Xcode or `xcrun simctl boot`).
- For real devices, use a LAN/tunnel/public HTTPS URL or install/trust your local CA profile on the device.

## Existing helper scripts

The bundle includes generated automation scripts:

- `scripts/doctor-mobile.sh` — validates tools, SDKs, and Start URL context.
- `scripts/bootstrap-ios.sh` — installs deps, adds/syncs iOS platform, applies localhost cert trust, and runs/opens iOS.
- `scripts/bootstrap-android.sh` — installs deps, adds/syncs Android platform, and runs/opens Android.
- `scripts/trust-ios-localhost-cert.sh` — imports localhost cert into booted iOS simulator keychain.
- `AGENT_PROMPT.md` — ready prompt to hand off setup troubleshooting to an AI coding agent.

You can still use low-level commands directly when needed:

```bash
npm run sync
npm run run:ios
npm run run:android
npm run open:ios
npm run open:android
```

## Runtime behavior

- Prism detects mobile mode using the appended user-agent marker.
- Tenant branding overrides are applied first.
- Mobile branding overrides are applied after tenant overrides.
- Generated config sets iOS `contentInset: 'always'` and `StatusBar.overlaysWebView: false` for safer default viewport behavior — `'always'` unconditionally reserves safe-area space (status bar/notch), fixing both a horizontal content-shift bug and a status-bar overlap that Capacitor's own default (`'automatic'`) and an earlier `'never'` attempt each caused in turn, on hosted content the app doesn't control (e.g. the Entra/OIDC login screen).
- App startup uses Capacitor top-level WebView loading of your Start URL.
- Generated config appends `prismMobile=1` to Start URL for server-side mobile detection.
- Prism mobile middleware can enforce in-WebView behavior for `target="_blank"` and `window.open`.
- A local fallback startup page is included in `www/index.html` if you choose to switch away from direct server URL mode.

## Entra authentication mode

- **Strict in-WebView mode:** keeps flows inside the WebView but may not satisfy all Entra/Conditional Access policies.
- **Compliance mode (recommended):** uses system browser auth session for Entra and can visually leave WebView.

Choose this explicitly per tenant/security policy. If strict in-WebView is mandatory, validate tenant policy and user journeys early.

## Customize mobile-specific UI

Use `www/mobile-overrides.css` as your starting point for mobile-scoped styles.
{{biometricSection}}
## Icons & Splash

- Icon source: {{iconLine}}
- Splash source: {{splashLine}}

Add final platform assets under the `resources/` folder and run Capacitor asset generation.
The `resources/mobile-assets.json` file stores the values entered in Backoffice for reference.
""";
    }

  private static string BuildAssetsManifest(
    string? iconUrl,
    string? splashUrl,
    string errorBackgroundColor,
    string errorTextColor,
    string errorTitle,
    string errorMessage,
    bool showErrorDiagnostics)
  {
    var splashWarning = !string.IsNullOrWhiteSpace(splashUrl) && splashUrl.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        ? """
,
  "splashWarning": "SVG format detected — convert to a high-resolution PNG before running @capacitor/assets"
"""
        : string.Empty;

    return $$"""
{
  "iconUrl": {{ToJsonStringOrNull(iconUrl)}},
  "splashUrl": {{ToJsonStringOrNull(splashUrl)}},
  "startupError": {
    "backgroundColor": {{ToJsonStringOrNull(errorBackgroundColor)}},
    "textColor": {{ToJsonStringOrNull(errorTextColor)}},
    "title": {{ToJsonStringOrNull(errorTitle)}},
    "message": {{ToJsonStringOrNull(errorMessage)}},
    "showDiagnostics": {{ToJsonBoolean(showErrorDiagnostics)}}
  },
  "notes": "Download and convert to final app assets (recommended 1024x1024 icon and high-resolution splash)"{{splashWarning}}
}
""";
  }

    private static string BuildPlaceholderIndex(
        string appName,
        string startUrl,
        string errorBackgroundColor,
        string errorTextColor,
        string errorTitle,
        string errorMessage,
        bool showErrorDiagnostics,
        bool biometricAuthEnabled)
    {
        var biometricStartupScript = biometricAuthEnabled
            ? """

    var __prismDebug = (function() {
      var KEY = 'prism_debug_log';
      function store(msg) {
        try {
          var log = JSON.parse(localStorage.getItem(KEY) || '[]');
          log.push({ t: new Date().toISOString(), m: msg });
          if (log.length > 50) log = log.slice(-50);
          localStorage.setItem(KEY, JSON.stringify(log));
        } catch(e) {}
      }
      function replay() {
        try {
          var log = JSON.parse(localStorage.getItem(KEY) || '[]');
          if (log.length > 0) {
            console.log('[Prism Debug Replay] ' + log.length + ' stored log(s) from previous page:');
            log.forEach(function(e) { console.log('  [' + e.t + '] ' + e.m); });
            localStorage.removeItem(KEY);
          }
        } catch(e) {}
      }
      function log(msg) {
        console.log(msg);
        store(msg);
      }
      return { log: log, replay: replay };
    })();

    async function tryBiometricSignIn() {
      __prismDebug.replay();
      try {
        __prismDebug.log('[Prism Bio] tryBiometricSignIn: starting');
        var Cap = window.Capacitor;
        if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) {
          __prismDebug.log('[Prism Bio] Not a native platform — skipping biometric');
          return false;
        }

        var tenantHost = new URL(prismBootstrap.startUrl).host;
        var SS_PREFIX = 'capacitor-storage_';
        var tokenKey = SS_PREFIX + 'prism_biometric_token_' + tenantHost;
        var enrollKey = 'prism_biometric_enrollment_state_' + tenantHost;
        var deviceIdKey = 'prism_device_id';

        // 1. Check stored biometric token (SecureStorage uses internalGetItem)
        __prismDebug.log('[Prism Bio] Step 1: checking SecureStorage for token, key: ' + tokenKey);
        var storedResult = await Cap.nativePromise('SecureStorage', 'internalGetItem', {
          prefixedKey: tokenKey,
          sync: false
        });
        var storedToken = storedResult && storedResult.data ? JSON.parse(storedResult.data) : null;
        if (!storedToken) {
          __prismDebug.log('[Prism Bio] Step 1: no stored token — not yet enrolled');
          return false;
        }
        __prismDebug.log('[Prism Bio] Step 1: stored token found');

        // 2. Check biometry availability
        __prismDebug.log('[Prism Bio] Step 2: checking biometry availability');
        var biometryInfo = await Cap.nativePromise('BiometricAuthNative', 'checkBiometry', {});
        __prismDebug.log('[Prism Bio] Step 2: biometryInfo = ' + JSON.stringify(biometryInfo));
        if (!biometryInfo || !biometryInfo.isAvailable) {
          __prismDebug.log('[Prism Bio] Step 2: biometry not available');
          return false;
        }

        // 3. Check enrollment change
        var fingerprint = [
          biometryInfo.biometryType,
          (biometryInfo.biometryTypes || []).slice().sort().join(','),
          biometryInfo.isAvailable,
          biometryInfo.strongBiometryIsAvailable,
          biometryInfo.deviceIsSecure
        ].join('|');
        __prismDebug.log('[Prism Bio] Step 3: enrollment fingerprint = ' + fingerprint);
        var storedFingerprint = localStorage.getItem(enrollKey);
        if (storedFingerprint && storedFingerprint !== fingerprint) {
          __prismDebug.log('[Prism Bio] Step 3: enrollment changed — clearing token');
          await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: tokenKey, sync: false });
          localStorage.removeItem(enrollKey);
          return false;
        }

        // 4. Prompt biometric authentication
        __prismDebug.log('[Prism Bio] Step 4: prompting biometric authentication');
        await Cap.nativePromise('BiometricAuthNative', 'internalAuthenticate', {
          reason: 'Sign in with biometrics',
          allowDeviceCredential: true,
          iosFallbackTitle: 'Use Passcode'
        });
        __prismDebug.log('[Prism Bio] Step 4: biometric authentication passed');

        // 5. Get device ID
        var deviceId = localStorage.getItem(deviceIdKey) || '';
        __prismDebug.log('[Prism Bio] Step 5: deviceId = ' + (deviceId || '(empty)'));

        // 6. Exchange biometric token for PrismMemberCookie (Set-Cookie on response)
        __prismDebug.log('[Prism Bio] Step 6: exchanging token with server');
        var resp = await fetch('https://' + tenantHost + '/umbraco/prism/mobile/biometric/exchange', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({ biometricToken: storedToken, deviceId: deviceId })
        });
        __prismDebug.log('[Prism Bio] Step 6: exchange response status = ' + resp.status);

        if (!resp.ok) {
          if (resp.status === 401 || resp.status === 403) {
            __prismDebug.log('[Prism Bio] Step 6: server rejected token — clearing stored credentials');
            await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: tokenKey, sync: false });
            localStorage.removeItem(enrollKey);
          }
          return false;
        }

        // Save updated enrollment fingerprint
        localStorage.setItem(enrollKey, fingerprint);
        __prismDebug.log('[Prism Bio] Step 6: exchange successful — proceeding to app');
        return true;
      } catch (e) {
        console.warn('[Prism Bio] tryBiometricSignIn threw:', e && (e.message || e));
        return false;
      }
    }
"""
            : "";

        var biometricBootstrapBlock = biometricAuthEnabled
            ? """
      __prismDebug.log('[Prism Bio] Bootstrap: biometric auth enabled — attempting sign-in');
      const biometricOk = await tryBiometricSignIn();
      __prismDebug.log('[Prism Bio] Bootstrap: tryBiometricSignIn returned ' + biometricOk);
      if (biometricOk) {
        window.location.replace(mobileStartUrl);
        return;
      }
      __prismDebug.log('[Prism Bio] Bootstrap: biometric did not sign in — falling through to Entra');

"""
            : """
      console.log('[Prism] Bootstrap: biometric auth NOT compiled into this bundle');

""";

        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>{{appName}} Mobile</title>
  <style>
    :root {
      --prism-error-bg: {{EscapeSingleQuotes(errorBackgroundColor)}};
      --prism-error-text: {{EscapeSingleQuotes(errorTextColor)}};
    }
    html, body {
      margin: 0;
      min-height: 100%;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      background: var(--prism-error-bg);
      color: var(--prism-error-text);
    }
    .screen {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px;
      box-sizing: border-box;
    }
    .card {
      width: 100%;
      max-width: 480px;
      border-radius: 14px;
      border: 1px solid color-mix(in srgb, var(--prism-error-text) 20%, transparent);
      background: color-mix(in srgb, var(--prism-error-bg) 86%, black);
      box-shadow: 0 20px 50px rgba(0,0,0,0.22);
      padding: 24px;
      box-sizing: border-box;
    }
    h1 {
      margin: 0 0 10px;
      font-size: 1.25rem;
      line-height: 1.35;
      letter-spacing: .01em;
    }
    p {
      margin: 0;
      line-height: 1.5;
      opacity: .95;
    }
    .actions {
      margin-top: 18px;
      display: flex;
      gap: 10px;
    }
    button {
      border: 0;
      border-radius: 10px;
      padding: 10px 14px;
      font-weight: 600;
      cursor: pointer;
      color: #fff;
      background: color-mix(in srgb, var(--prism-error-text) 20%, #2563eb);
    }
    details {
      margin-top: 16px;
      border-radius: 8px;
      background: rgba(0, 0, 0, 0.2);
      padding: 10px;
    }
    summary {
      cursor: pointer;
      font-weight: 600;
      user-select: none;
    }
    pre {
      margin: 10px 0 0;
      white-space: pre-wrap;
      word-break: break-word;
      font-size: 0.82rem;
      opacity: 0.92;
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
    }
  </style>
</head>
<body>
  <main class="screen">
    <section class="card">
      <h1 id="title">Opening {{EscapeSingleQuotes(appName)}}…</h1>
      <p id="message">Connecting to {{EscapeSingleQuotes(startUrl)}}.</p>
      <div class="actions">
        <button id="retry" type="button" hidden>Try again</button>
      </div>
      <details id="diagnostics" hidden>
        <summary>Technical details</summary>
        <pre id="details"></pre>
      </details>
    </section>
  </main>
  <noscript>This app shell requires JavaScript to connect to your Start URL.</noscript>
  <!-- [Prism Debug] biometricAuthEnabled: {{ToJsonBoolean(biometricAuthEnabled)}} -->
  <script>
    console.log('[Prism] www/index.html loaded — biometricAuthEnabled: {{ToJsonBoolean(biometricAuthEnabled)}}');
    const prismBootstrap = {
      startUrl: '{{EscapeSingleQuotes(startUrl)}}',
      timeoutMs: 10000,
      errorTitle: '{{EscapeSingleQuotes(errorTitle)}}',
      errorMessage: '{{EscapeSingleQuotes(errorMessage)}}',
      showDiagnostics: {{ToJsonBoolean(showErrorDiagnostics)}}
    };

    function toMobileStartUrl(rawUrl) {
      const parsed = new URL(rawUrl);
      parsed.searchParams.set('prismMobile', '1');
      return parsed.toString();
    }

    const mobileStartUrl = toMobileStartUrl(prismBootstrap.startUrl);

    const titleEl = document.getElementById('title');
    const messageEl = document.getElementById('message');
    const retryButton = document.getElementById('retry');
    const diagnosticsEl = document.getElementById('diagnostics');
    const detailsEl = document.getElementById('details');

    function setLoading() {
      titleEl.textContent = 'Opening {{EscapeSingleQuotes(appName)}}…';
      messageEl.textContent = `Connecting to ${mobileStartUrl}.`;
      retryButton.hidden = true;
      diagnosticsEl.hidden = true;
      detailsEl.textContent = '';
    }

    function formatErrorDetails(result) {
      const lines = [
        `Start URL: ${mobileStartUrl}`,
        `Timestamp: ${new Date().toISOString()}`,
        `Timeout: ${prismBootstrap.timeoutMs}ms`
      ];

      if (result && result.reason) {
        lines.push(`Reason: ${result.reason}`);
      }

      if (result && result.message) {
        lines.push(`Error: ${result.message}`);
      }

      return lines.join('\n');
    }

    function showError(result) {
      titleEl.textContent = prismBootstrap.errorTitle;
      messageEl.textContent = prismBootstrap.errorMessage;
      retryButton.hidden = false;

      if (prismBootstrap.showDiagnostics) {
        diagnosticsEl.hidden = false;
        detailsEl.textContent = formatErrorDetails(result);
      }
    }

    async function canReachStartUrl() {
      const controller = new AbortController();
      let timedOut = false;
      const timeoutId = window.setTimeout(() => {
        timedOut = true;
        controller.abort();
      }, prismBootstrap.timeoutMs);

      try {
        await fetch(mobileStartUrl, {
          method: 'GET',
          mode: 'no-cors',
          cache: 'no-store',
          signal: controller.signal
        });
        return { ok: true };
      } catch (error) {
        return {
          ok: false,
          reason: timedOut ? 'Request timed out before reaching Start URL.' : 'Failed to reach Start URL.',
          message: error instanceof Error ? error.message : String(error)
        };
      } finally {
        window.clearTimeout(timeoutId);
      }
    }

{{biometricStartupScript}}
    async function bootstrap() {
      console.log('[Prism] bootstrap() called');
      setLoading();
{{biometricBootstrapBlock}}
      const result = await canReachStartUrl();
      if (result.ok) {
        window.location.replace(mobileStartUrl);
        return;
      }

      showError(result);
    }

    retryButton.addEventListener('click', bootstrap);
    bootstrap();
  </script>
</body>
</html>
""";
    }

    private static string BuildMobileOverrideTemplate()
    {
        return """
/* Example mobile-only token overrides */
:root {
  --prism-page-gutter: 12px;
  --prism-grid-min: 180px;
}

/* Safe area helpers for notch / home indicator devices */
.prism-mobile {
  --prism-safe-top: env(safe-area-inset-top, 0px);
  --prism-safe-right: env(safe-area-inset-right, 0px);
  --prism-safe-bottom: env(safe-area-inset-bottom, 0px);
  --prism-safe-left: env(safe-area-inset-left, 0px);
}

.prism-mobile body {
  padding-top: var(--prism-safe-top);
  padding-right: var(--prism-safe-right);
  padding-bottom: var(--prism-safe-bottom);
  padding-left: var(--prism-safe-left);
}

.prism-mobile .container {
  width: 100%;
  max-width: none;
  margin: 0;
  box-sizing: border-box;
}

/* App-shell styling examples */
.prism-mobile .desktop-nav {
  display: none;
}

.prism-mobile .mobile-nav {
  display: flex;
}
""";
    }

    private static string BuildDefaultAppIconSvg()
    {
        // Sourced from assets/app-icon.svg (a derivative of assets/logo.svg with an opaque
        // background added — iOS App Store icons must have no alpha channel). Keep both in sync
        // by eye; there's no build step linking them, this is a small, rarely-changed asset.
        return """
<svg width="1024" height="1024" viewBox="0 0 1024 1024" xmlns="http://www.w3.org/2000/svg">
    <rect width="1024" height="1024" fill="#1B264F"/>
    <g transform="translate(179.5, 195.5) scale(6.33)">
        <defs>
            <linearGradient id="i_brand1" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" style="stop-color:#3544B1" />
                <stop offset="100%" style="stop-color:#2DA7D1" />
            </linearGradient>
            <linearGradient id="i_brand2" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" style="stop-color:#3544B1" />
                <stop offset="100%" style="stop-color:#F5A623" />
            </linearGradient>
            <linearGradient id="i_brand3" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" style="stop-color:#3544B1" />
                <stop offset="100%" style="stop-color:#E91E63" />
            </linearGradient>
        </defs>

        <g transform="translate(2, 25)">
            <path d="M2 25C2 14.5066 10.5066 6 21 6H35C37.7614 6 40 8.23858 40 11V39C40 41.7614 37.7614 44 35 44H21C10.5066 44 2 35.4934 2 25Z" fill="#F8FAFC"/>
            <path d="M44 11H50L58 25L50 39H44V11Z" fill="#F8FAFC"/>
            <rect x="62" y="0" width="30" height="14" rx="7" fill="url(#i_brand1)"/>
            <rect x="62" y="18" width="38" height="14" rx="7" fill="url(#i_brand2)"/>
            <rect x="62" y="36" width="30" height="14" rx="7" fill="url(#i_brand3)"/>
        </g>
    </g>
</svg>
""";
    }

    private static string BuildTrustIosLocalhostCertScript(string startUrl)
    {
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

    private static string BuildDoctorScript(string startUrl)
    {
        var uri = new Uri(startUrl);
        var host = uri.Host;
        var port = uri.IsDefaultPort
            ? (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : uri.Port;

        return $$"""
#!/usr/bin/env bash
set -euo pipefail

PLATFORM="${1:-all}"
START_URL="{{startUrl}}"
HOST="{{host}}"
PORT="{{port}}"

echo "Running Prism mobile doctor..."

if ! command -v node >/dev/null 2>&1; then
  echo "❌ Node.js not found"; exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
  echo "❌ npm not found"; exit 1
fi

if ! command -v npx >/dev/null 2>&1; then
  echo "❌ npx not found"; exit 1
fi

echo "✅ Node: $(node --version)"
echo "✅ npm:  $(npm --version)"

if [[ "$PLATFORM" == "all" || "$PLATFORM" == "ios" ]]; then
  if ! command -v xcodebuild >/dev/null 2>&1; then
    echo "❌ Xcode CLI tools missing (xcodebuild not found)"; exit 1
  fi

  if ! command -v pod >/dev/null 2>&1; then
    echo "❌ CocoaPods missing (install with: brew install cocoapods)"; exit 1
  fi

  echo "✅ Xcode: $(xcodebuild -version | head -n 1)"
  echo "✅ CocoaPods: $(pod --version)"
fi

if [[ "$PLATFORM" == "all" || "$PLATFORM" == "android" ]]; then
  if ! command -v adb >/dev/null 2>&1; then
    echo "⚠️ adb not found (Android SDK platform-tools may be missing)"
  else
    echo "✅ adb available"
  fi
fi

if [[ "$HOST" == "localhost" || "$HOST" == "127.0.0.1" || "$HOST" == "::1" ]]; then
  echo "ℹ️ Start URL uses localhost: $START_URL"
  if [[ "${START_URL#https://}" == "$START_URL" ]]; then
    echo "⚠️ Localhost is not HTTPS. iOS App Transport Security may block HTTP unless cleartext is enabled."
  else
    if command -v openssl >/dev/null 2>&1; then
      if echo | openssl s_client -connect "$HOST:$PORT" -servername "$HOST" -showcerts 2>/dev/null | grep -q "BEGIN CERTIFICATE"; then
        echo "✅ HTTPS cert is reachable for $HOST:$PORT"
      else
        echo "⚠️ Could not read HTTPS cert from $HOST:$PORT. Ensure your local site is running."
      fi
    fi
  fi
fi

echo "Doctor complete."
""";
    }

    private static string BuildBootstrapIosScript(string startUrl, bool biometricAuthEnabled, bool mobileDiagnosticsEnabled)
    {
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

""";

        var infoPlistInjection = biometricAuthEnabled
            ? """

echo "Injecting NSFaceIDUsageDescription into Info.plist..."
if [ -f ios/App/App/Info.plist ]; then
  if ! grep -q "NSFaceIDUsageDescription" ios/App/App/Info.plist; then
    plutil -insert NSFaceIDUsageDescription -string "We use Face ID to securely log you in without requiring your password each time." ios/App/App/Info.plist
    echo "✓ NSFaceIDUsageDescription added to Info.plist"
  else
    echo "✓ NSFaceIDUsageDescription already present in Info.plist"
  fi
else
  echo "⚠️ Info.plist not found. Run 'npx cap add ios' first."
fi

"""
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
        var zoomFixInjection = """

echo "Disabling WebKit's zoom-into-focused-input behaviour on hosted content..."
if [ -d ios/App/App ]; then
  cat > ios/App/App/PrismBridgeViewController.swift << 'PRISM_SWIFT_EOF'
import Capacitor
import WebKit

""" + mobileDiagnosticsFlagDeclaration + """

// Force-pins every page's own viewport meta tag to maximum-scale=1 at the WebKit level, so it
// applies even to cross-origin hosted content (forMainFrameOnly: false) that this app has no
// CSS/markup control over — see bootstrap-ios.sh's own comment for the full rationale. Runs at
// document start and again on DOMContentLoaded, so it wins regardless of whether the page's own
// <meta name=viewport> tag exists yet.
class PrismBridgeViewController: CAPBridgeViewController, WKScriptMessageHandler {
    private static let viewportFixDiagnosticMessageName = "prismViewportFixDiag"

    // TEMPORARY — confirms live that moving script injection to capacitorDidLoad() (see its own
    // remarks) actually reaches the real webview, the same way an earlier version of this exact
    // counter proved the previous injection point (webViewConfiguration(for:)) never did. Remove
    // once confirmed.
    fileprivate static var viewportScriptPingCount = 0

    // TEMPORARY — reported live: plugin= (PrismContentWatcherPlugin.callCount) stays at 0 despite
    // interacting with pages that definitely mutate the DOM, even though every step of the
    // registration/JS-export/message-routing path was independently confirmed correct against
    // Capacitor's own vendored source. A completely separate, already-proven-reliable
    // WKScriptMessageHandler channel (the same mechanism vp= uses) reports each stage of
    // prism-mobile-content-watcher.js's own execution directly, without depending on the
    // still-unproven plugin bridge to report progress — the same isolation technique that found
    // the capacitorDidLoad() root cause in the first place. ws= (script executed at all, pinged
    // unconditionally at the top of the file) / wr= (Cap.isNativePlatform()/nativePromise guard
    // passed and the MutationObserver was actually attached) / wm= (the observer fired and a
    // native call was about to be attempted) / we= (that native call's promise rejected) — reading
    // which of these four climbs and which doesn't pinpoints exactly which link is broken, the
    // same way raw=/init=/cfg= did previously. Remove once root-caused.
    private static let contentWatcherDiagnosticMessageName = "prismContentWatcherDiag"
    fileprivate static var contentWatcherScriptStartedPingCount = 0
    fileprivate static var contentWatcherReadyPingCount = 0
    fileprivate static var contentWatcherMutationPingCount = 0
    fileprivate static var contentWatcherErrorPingCount = 0

    private var navigationHold: PrismNavigationHoldDelegate?

    // Root-caused from Capacitor's own vendored iOS source (CAPBridgeViewController.prepareWebView):
    // a webViewConfiguration(for:) override's returned WKWebViewConfiguration.userContentController
    // gets discarded and replaced wholesale with Capacitor's own internal one, one line later,
    // before the real webview is ever built — confirmed live, previously, by a counter proving
    // that override WAS being called while everything added to its content controller (a
    // paint-holding content-change script, and — a real product bug, not just a diagnostic gap —
    // this app's own viewport-zoom-fix for Entra's hosted login page) never actually ran.
    // capacitorDidLoad() is called from inside loadView() — documented: webView/bridge are already
    // set by this point, but no navigation has started yet — and webView.configuration
    // .userContentController here IS the real, live one Capacitor itself keeps (the same one its
    // own plugin bridge JS gets added to), so anything added here actually takes effect.
    override func capacitorDidLoad() {
        super.capacitorDidLoad()
        guard let webView = self.webView else { return }
        let contentController = webView.configuration.userContentController

        // Capacitor's own `zoomEnabled: false` only disables the user's own pinch-zoom gesture —
        // it does not stop WKWebView's own "zoom into a focused text input" behaviour, which fires
        // independently whenever a page's own viewport doesn't cap maximum-scale. Reported live on
        // the Entra/ciamlogin.com sign-in page (hosted content this app doesn't control and can't
        // add page-level CSS/viewport-meta to): the password field triggered a zoomed-in,
        // left-clipped layout. forMainFrameOnly: false specifically because this needs to reach
        // that cross-origin hosted content — a server-rendered <script> tag (the approach used for
        // this app's own pages' paint-holding detection, see PrismContentWatcherPlugin's own
        // remarks) fundamentally cannot reach a page a different server renders; only native
        // injection at the WebView level can.
        let viewportFixSource = "(function(){function pin(){var meta=document.querySelector('meta[name=viewport]');if(!meta){meta=document.createElement('meta');meta.name='viewport';document.head.appendChild(meta);}meta.content='width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no';if(\(PrismMobileDiagnosticsFlag.enabled)&&window.webkit&&window.webkit.messageHandlers&&window.webkit.messageHandlers.\(Self.viewportFixDiagnosticMessageName)){window.webkit.messageHandlers.\(Self.viewportFixDiagnosticMessageName).postMessage('viewport-ready');}}if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',pin);}else{pin();}})();"
        let viewportFixScript = WKUserScript(source: viewportFixSource, injectionTime: .atDocumentStart, forMainFrameOnly: false)
        contentController.addUserScript(viewportFixScript)
        if PrismMobileDiagnosticsFlag.enabled {
            // WKUserContentController retains whatever's added as a message handler for as long
            // as it exists — adding `self` directly here would be the textbook WKScriptMessageHandler
            // retain cycle (this view controller owns the webview, which owns this very content
            // controller, which would then own this view controller right back). The
            // weak-referencing proxy below is the standard fix.
            contentController.add(WeakScriptMessageHandler(target: self), name: Self.viewportFixDiagnosticMessageName)
            contentController.add(WeakScriptMessageHandler(target: self), name: Self.contentWatcherDiagnosticMessageName)
        }

        // Capacitor's own canonical JS-to-native bridge, replacing an earlier
        // WKScriptMessageHandler-based attempt for paint-holding's own content-change detection —
        // see PrismContentWatcherPlugin's own remarks. registerPluginInstance (not
        // registerPluginType, which silently no-ops when autoRegisterPlugins is true — confirmed
        // from Capacitor's own vendored source, and true by default here since this app never
        // overrides it) is what actually wires this into Capacitor's real, live content
        // controller via its own JSExport mechanism — the exact mechanism
        // @aparajita/capacitor-biometric-auth's own plugin (already proven working in this app)
        // uses too.
        bridge?.registerPluginInstance(PrismContentWatcherPlugin())
    }

    override func viewDidLoad() {
        super.viewDidLoad()
        guard let webView = self.webView else { return }
        // See PrismNavigationHoldDelegate's own remarks for why this exists and how it avoids
        // reimplementing Capacitor's own navigation handling. Also wraps webView.uiDelegate, not
        // just navigationDelegate — reported live: every decidePolicyFor decision during a sign-out
        // attempt came back "allow", including for the logout POST itself, so whatever's actually
        // bouncing the app to system Safari isn't happening through navigationDelegate at all. A
        // suspicious "allow GET about:blank" in that same log is the fingerprint of a window.open()
        // call, which WebKit routes through a completely different delegate method
        // (createWebViewWith, on WKUIDelegate) that this app was never observing.
        let hold = PrismNavigationHoldDelegate(
            forwardingTo: webView.navigationDelegate,
            forwardingUIDelegateTo: webView.uiDelegate,
            // Reported live: a window.open()-style popup request for this app's own
            // /auth/logout — the exact URL a plain top-level navigation was already handling
            // correctly — got sent straight to system Safari by Capacitor's own createWebViewWith,
            // which (confirmed from its vendored source) has no allowlist logic at all, unlike
            // decidePolicyFor. Reuses the SAME allowlist Capacitor's own regular navigation
            // handling already trusts (bridge.config.shouldAllowNavigation, the exact call
            // decidePolicyFor itself makes) rather than inventing a second one, so a popup
            // targeting this app's own trusted hosts stays in-app regardless of what triggered it.
            isHostTrustedInApp: { [weak self] host in self?.bridge?.config.shouldAllowNavigation(to: host) ?? false }
        )
        navigationHold = hold
        // Read by PrismContentWatcherPlugin — see its own remarks on why a plain direct reference,
        // set here, rather than reached via bridge.viewController.
        PrismContentWatcherPlugin.activeHold = hold
        webView.navigationDelegate = hold
        webView.uiDelegate = hold

        // TEMPORARY (see PrismNavigationHoldDelegate's own remarks) — a no-op unless this specific
        // build was produced with mobile diagnostics enabled. A two-finger long-press anywhere on
        // the page toggles the on-screen paint-holding diagnostic label on/off; otherwise
        // invisible. Reported live: an earlier single-finger version, restricted to near the top
        // of the screen to avoid colliding with normal page interaction, didn't work at all — the
        // real status bar area isn't part of this app's own view hierarchy (it's drawn by iOS
        // itself, and AppDelegate's own safe-area-pinned container deliberately sits below it, see
        // its own remarks), so no gesture recognizer attached to anything in this app can ever see
        // a touch that lands there. Attached directly to webView (not just its superview) so it
        // works anywhere the page actually renders, not a strip that turned out to be unreachable.
        if PrismMobileDiagnosticsFlag.enabled {
            let diagnosticsGesture = UILongPressGestureRecognizer(target: hold, action: #selector(PrismNavigationHoldDelegate.handleDiagnosticsGesture(_:)))
            diagnosticsGesture.numberOfTouchesRequired = 2
            diagnosticsGesture.minimumPressDuration = 1.5
            diagnosticsGesture.delegate = hold
            webView.addGestureRecognizer(diagnosticsGesture)
        }

        // Real product fix, unconditional on diagnostics — reported live: on a slow connection
        // the navigation spinner sits dead-center, but a user's eyes are already on wherever they
        // just tapped, not the screen's center, so a subtle spinner there goes unnoticed. Tracks
        // the most recent tap's location so showSpinner() can appear right where attention already
        // is instead. cancelsTouchesInView = false, plus shouldRecognizeSimultaneouslyWith
        // (already true for every recognizer on this delegate — see its own remarks), mean this
        // only observes taps, it never intercepts or delays them: ordinary page interaction (link
        // taps, button presses, WKWebView's own tap handling) is completely unaffected.
        let tapTracker = UITapGestureRecognizer(target: hold, action: #selector(PrismNavigationHoldDelegate.handleTapForSpinnerPositioning(_:)))
        tapTracker.cancelsTouchesInView = false
        tapTracker.delegate = hold
        webView.addGestureRecognizer(tapTracker)
    }

    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        switch message.name {
        case Self.viewportFixDiagnosticMessageName:
            if message.body as? String == "viewport-ready" {
                Self.viewportScriptPingCount += 1
            }
        case Self.contentWatcherDiagnosticMessageName:
            switch message.body as? String {
            case "script-started": Self.contentWatcherScriptStartedPingCount += 1
            case "ready": Self.contentWatcherReadyPingCount += 1
            case "mutation": Self.contentWatcherMutationPingCount += 1
            case "error": Self.contentWatcherErrorPingCount += 1
            default: break
            }
        default:
            break
        }
    }
}

private final class WeakScriptMessageHandler: NSObject, WKScriptMessageHandler {
    private weak var target: WKScriptMessageHandler?

    init(target: WKScriptMessageHandler) {
        self.target = target
    }

    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        target?.userContentController(userContentController, didReceive: message)
    }
}

// Capacitor's own canonical JS-to-native bridge for content Prism itself serves (see
// prism-mobile-content-watcher.js's own remarks for the full history of why this replaced a
// native WKUserScript/WKScriptMessageHandler-based attempt entirely: that mechanism's
// WKWebViewConfiguration.userContentController was confirmed, from Capacitor's own vendored
// source, to be discarded before the real webview is ever built, so nothing added there was ever
// actually live). registerPluginInstance (in PrismBridgeViewController.capacitorDidLoad()) wires
// this into Capacitor's real, live content controller via its own JSExport mechanism — the exact
// mechanism @aparajita/capacitor-biometric-auth's own plugin already uses successfully in this
// app. jsName ("PrismContentWatcher") must exactly match the string prism-mobile-content-watcher.js
// passes to Cap.nativePromise — that's the only key Capacitor's own JS-side plugin-header lookup
// matches on.
@objc(PrismContentWatcherPlugin)
public class PrismContentWatcherPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "PrismContentWatcherPlugin"
    public let jsName = "PrismContentWatcher"
    public let pluginMethods: [CAPPluginMethod] = [
        .init(#selector(contentChanged))
    ]

    // Set by PrismBridgeViewController.viewDidLoad() once PrismNavigationHoldDelegate exists — not
    // reached via bridge.viewController, since whether that resolves to the SAME
    // PrismBridgeViewController instance that registered this plugin isn't something confirmed
    // from Capacitor's own source the way registerPluginInstance itself is; a plain direct
    // reference avoids depending on it. weak since PrismBridgeViewController (not this plugin) owns
    // the delegate's lifetime.
    fileprivate static weak var activeHold: PrismNavigationHoldDelegate?

    // Diagnostic: distinguishes "the JS bridge never reached this method at all" (stays at 0) from
    // "it did, but activeHold was nil" (this climbs, contentChangeSignalCount on the label doesn't)
    // from "everything downstream works" (both climb together) — the same reasoning the mechanism
    // this replaces used its own raw-message counter for.
    fileprivate static var callCount = 0

    @objc func contentChanged(_ call: CAPPluginCall) {
        Self.callCount += 1
        if let webView = self.bridge?.webView {
            Self.activeHold?.contentDidChange(in: webView)
        }
        call.resolve()
    }
}

// WKWebView, embedded the way Capacitor uses it here, shows a real blank gap between full-page
// navigations (confirmed live; confirmed by reading Capacitor's own vendored iOS source: no
// snapshot/hold mechanism anywhere in it, this is a genuine gap in what it provides, not something
// misconfigured). Two different attempts to paper over that gap with a frozen frame of the
// outgoing page failed live, in production, in the same way — a synchronous UIView snapshot, and
// WKWebView's own async takeSnapshot API, each independently resolved to a blank/black image —
// and both share the same root cause: both were captured at the instant a navigation starts,
// which is exactly the timing WebKit's own documentation and outside reports call out as
// unreliable, because the outgoing page hasn't settled yet. Neither is captured at that moment any
// more. Instead, scheduleSnapshotCapture() takes a fresh snapshot a short delay *after* each
// navigation finishes — the one timing this API is documented to actually work at — and caches it;
// showSpinner() for the *next* navigation just hands over whatever's already cached, synchronously,
// with no async call happening at the moment it's needed and so no timing race left to get wrong
// there. If nothing has been cached yet (the very first navigation after launch), WKWebView's own
// default rendering is left alone rather than covering it with a placeholder that isn't actually a
// frame of anything. Either way, a small spinner is layered on top too, revealed after a short
// delay (100ms) so a slow navigation still gets a visible sign something is happening.
//
// The cached frame is also refreshed while the user stays on a page — contentDidChange(in:) is
// called from PrismBridgeViewController's own WKScriptMessageHandler whenever injected JS detects
// input/change/scroll activity (debounced to one message per 100ms of quiet — see that script's
// own remarks), so a page the user has actually typed into or scrolled since it loaded doesn't
// keep showing the empty/unscrolled frame it had right after settling. It can still be briefly
// behind the very latest keystroke or scroll position — that's an accepted tradeoff of capturing
// only once things go quiet, the same one iOS's own app-switcher snapshots make — but it's no
// longer pinned to "whatever the page looked like the moment it finished loading" for the whole
// time the user stays on it.
//
// Works for every navigation regardless of origin, including the federated redirect chain through
// a hosted IdP (Entra) and back — a page this app doesn't control obviously can't run any JS of
// ours, so a DOM-level fix (a click-triggered spinner, tried first) can only ever cover taps on
// this app's own pages, not that whole chain. This covers all of it, because it hooks the WebView
// itself, not any one page's content.
//
// WKWebView.navigationDelegate is a single slot Capacitor already fills with its own
// WebViewDelegationHandler (real navigation policy/redirect/auth-challenge handling the bridge
// depends on to function) — replacing it outright, or reimplementing everything it does by hand,
// is exactly the kind of blind reimplementation that already broke this app twice this session (on
// the safe-area fix, before landing on the AppDelegate approach actually shipped). So this
// implements only the two methods it needs (didStartProvisionalNavigation/didFinish/didFail) and
// forwards every other WKNavigationDelegate call straight through to Capacitor's own delegate
// unchanged, via the standard Cocoa message-forwarding decorator pattern
// (responds(to:)/forwardingTarget(for:)) — Capacitor's own handling of everything else is
// untouched, not reimplemented.
private final class PrismNavigationHoldDelegate: NSObject, WKNavigationDelegate, WKUIDelegate, UIGestureRecognizerDelegate {
    private let target: WKNavigationDelegate?
    // Not weak — matches `target` above, which is also a plain strong reference to the same kind
    // of Capacitor-owned delegate object. No retain cycle risk: neither this object nor `target`
    // holds any reference back to the webview/view controller that in turn retains `hold`.
    private let uiTarget: WKUIDelegate?
    private let isHostTrustedInApp: (String) -> Bool
    private var spinnerView: UIActivityIndicatorView?
    private var spinnerRevealWorkItem: DispatchWorkItem?
    private var snapshotOverlayView: UIImageView?
    private var lastGoodSnapshot: UIImage?
    // Read by showSpinner() to position the spinner near where the user is actually looking —
    // see viewDidLoad's own remarks on the tap-tracking gesture that sets this, and
    // handleTapForSpinnerPositioning's own remarks on the recency window.
    private var lastTapLocation: (point: CGPoint, at: Date)?
    private var pendingSnapshotCapture: DispatchWorkItem?
    private var isCaptureInFlight = false
    private var captureNeededAfterInFlight = false
    private var pendingCaptureSource = "none"

    // Permanent, but never present at all unless this exact bundle was produced with mobile
    // diagnostics deliberately enabled (PrismMobileDiagnosticsFlag.enabled, a build-time constant
    // — see BuildBootstrapIosScript's own remarks), and never visible even then unless the reveal
    // gesture (see handleDiagnosticsGesture) has been used. Tracks what's actually happening in
    // this pipeline so it can be read directly off a device, where there's no attached debugger/
    // console to check instead. A UILabel, not anything drawn into the web page itself — it can't
    // ever trigger this class's own JS-side input/change/scroll listeners, so no risk of it
    // feeding back into the very thing it's reporting on. Shows only build/version, cache hit/
    // miss counters, and timing — nothing from the page's own content, and nothing a user typed.
    private var diagnosticLabel: UILabel?
    private var diagnosticDismissWorkItem: DispatchWorkItem?
    private var isDiagnosticsRevealed = false
    private var lastCaptureSource = "none"
    private var lastCaptureAt: Date?
    private var contentChangeSignalCount = 0
    private var captureSuccessCount = 0
    private var captureFailureCount = 0

    init(forwardingTo target: WKNavigationDelegate?, forwardingUIDelegateTo uiTarget: WKUIDelegate?, isHostTrustedInApp: @escaping (String) -> Bool) {
        self.target = target
        self.uiTarget = uiTarget
        self.isHostTrustedInApp = isHostTrustedInApp
    }

    override func responds(to aSelector: Selector!) -> Bool {
        if super.responds(to: aSelector) { return true }
        if target?.responds(to: aSelector) ?? false { return true }
        return uiTarget?.responds(to: aSelector) ?? false
    }

    override func forwardingTarget(for aSelector: Selector!) -> Any? {
        if super.responds(to: aSelector) { return nil }
        if target?.responds(to: aSelector) ?? false { return target }
        // Anything neither this class nor `target` implements falls through to `uiTarget` —
        // needed because this is now also installed as webView.uiDelegate (see viewDidLoad's own
        // remarks), and Capacitor's own uiDelegate may implement WKUIDelegate methods beyond
        // createWebViewWith (JS alert/confirm panels, for instance) that this class doesn't
        // reimplement and must not silently drop just by having taken over the delegate slot.
        return uiTarget
    }

    func webView(_ webView: WKWebView, didStartProvisionalNavigation navigation: WKNavigation!) {
        // A capture scheduled for the page now being navigated away from — if it hasn't fired
        // yet, cancel it rather than let it run mid-navigation, which is the exact bad timing
        // this whole scheme exists to avoid. Also drops any catch-up already flagged for once an
        // in-flight capture finishes — that capture started on the *old* page (its result is
        // still valid and kept), but a follow-up triggered by it firing mid-navigation on the
        // *new* page would not be. lastGoodSnapshot just stays one navigation stale in that case;
        // see the class-level remarks on why that's an accepted tradeoff, not a bug.
        pendingSnapshotCapture?.cancel()
        pendingSnapshotCapture = nil
        captureNeededAfterInFlight = false
        showSpinner(over: webView)
        target?.webView?(webView, didStartProvisionalNavigation: navigation)
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        target?.webView?(webView, didFinish: navigation)
        // One extra runloop turn so the new page has actually painted before the spinner is
        // hidden — didFinish fires on load completion, not first paint.
        DispatchQueue.main.async { [weak self] in self?.hideSpinner() }
        scheduleSnapshotCapture(of: webView, delay: 0.3, source: "settle")
    }

    // Called by PrismBridgeViewController's own WKScriptMessageHandler when injected JS detects
    // the page has been typed into, changed, or scrolled — see the class-level remarks. A much
    // shorter delay than the post-didFinish capture: the page is already loaded and settled, this
    // is just refreshing a cached frame to match what's actually on screen now, not waiting out
    // trailing load-time rendering.
    fileprivate func contentDidChange(in webView: WKWebView) {
        contentChangeSignalCount += 1
        scheduleSnapshotCapture(of: webView, delay: 0.1, source: "change")
    }

    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        target?.webView?(webView, didFail: navigation, withError: error)
        hideSpinner()
    }

    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        target?.webView?(webView, didFailProvisionalNavigation: navigation, withError: error)
        hideSpinner()
    }

    // Observes, never alters, Capacitor's own real navigation-policy decision — reported live: a
    // sign-out tap bounces the whole app out to system Safari, landing on this app's own
    // /auth/logout URL, blank. Confirmed from Capacitor's vendored source that its decision here is
    // purely host-allowlist-based with no method/navigationType distinction, and confirmed every
    // Sign Out button submits a plain top-level POST form — so which exact URL, with which method,
    // actually gets cancelled (bounced) is the one thing that can't be settled by reading source,
    // only by watching a real decision happen. Manually forwards to target rather than relying on
    // forwardingTarget(for:) (used for everything else this class doesn't implement) specifically
    // because observing requires wrapping the completion handler, not just relaying the call
    // unchanged — target?.webView?(...) still safely no-ops exactly like forwardingTarget would if
    // target is nil or doesn't implement this, which the guard below detects and falls back to
    // .allow for (WKWebView's own default with no navigationDelegate at all) so a navigation can
    // never hang waiting for a decisionHandler that was never going to fire.
    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        let urlString = navigationAction.request.url?.absoluteString ?? "nil"
        let method = navigationAction.request.httpMethod ?? "GET"
        // Explicit Void? annotation: deliberate here, not an accident this needs silencing —
        // nil is exactly how "target is nil, or doesn't implement this optional method" is
        // distinguished from "it does, and just called our wrapped completion".
        let forwarded: Void? = target?.webView?(webView, decidePolicyFor: navigationAction, decisionHandler: { policy in
            Self.recordNavigationDecision(url: urlString, method: method, decision: policy == .cancel ? "CANCEL" : "allow")
            decisionHandler(policy)
        })
        if forwarded == nil {
            Self.recordNavigationDecision(url: urlString, method: method, decision: "allow")
            decisionHandler(.allow)
        }
    }

    // createWebViewWith (WKUIDelegate) is a completely separate delegate method from
    // decidePolicyFor (WKNavigationDelegate) above — reported live: decidePolicyFor showed nothing
    // but "allow" during a sign-out attempt that still ended up in system Safari, including for
    // the logout POST itself, immediately followed by "allow GET about:blank" and then this
    // method firing for that exact same /auth/logout URL. Something in that chain issues a
    // window.open()-style request for a URL a plain top-level navigation was already handling
    // correctly — Capacitor's own implementation of this method (confirmed from its vendored
    // source) has no allowlist check of any kind, unlike decidePolicyFor: it unconditionally opens
    // the popup's target URL in system Safari regardless of host. Rather than alter that
    // wholesale, this checks the target host against the SAME allowlist Capacitor's own
    // decidePolicyFor already trusts for ordinary navigation (bridge.config.shouldAllowNavigation,
    // via isHostTrustedInApp — see viewDidLoad's own remarks) — a popup targeting one of this
    // app's own trusted hosts loads in the same webview instead, regardless of what triggered it;
    // anything else still goes to Capacitor's own real uiDelegate exactly as before, unchanged.
    func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration, for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
        let urlString = navigationAction.request.url?.absoluteString ?? "nil"
        if let host = navigationAction.request.url?.host, isHostTrustedInApp(host) {
            Self.recordNavigationDecision(url: urlString, method: "WINDOW.OPEN", decision: "IN-APP")
            webView.load(navigationAction.request)
            return nil
        }
        Self.recordNavigationDecision(url: urlString, method: "WINDOW.OPEN", decision: "EXTERNAL")
        return uiTarget?.webView?(webView, createWebViewWith: configuration, for: navigationAction, windowFeatures: windowFeatures) ?? nil
    }

    // TEMPORARY diagnostic aid, same spirit as the paint-holding counters above but for a
    // different bug: sign-out leaves the app entirely, so there's no "next navigation's spinner"
    // moment left in THIS app session to show a label at — UserDefaults, not an in-memory property,
    // specifically so the log survives the round trip through Safari even if the user force-quits
    // while there (reported as part of the same investigation) rather than just backgrounding.
    // Recording is gated on PrismMobileDiagnosticsFlag.enabled (a build-time constant) but NOT on
    // isDiagnosticsRevealed (a display-time toggle) — capturing what happened shouldn't depend on
    // whether anyone happened to have the overlay open at that exact moment; only showing it does.
    private static let navigationDecisionLogKey = "prism.diag.navigationDecisionLog"

    private static func recordNavigationDecision(url: String, method: String, decision: String) {
        guard PrismMobileDiagnosticsFlag.enabled else { return }
        var log = UserDefaults.standard.stringArray(forKey: navigationDecisionLogKey) ?? []
        log.append("\(decision) \(method) \(url)")
        if log.count > 10 {
            log.removeFirst(log.count - 10)
        }
        UserDefaults.standard.set(log, forKey: navigationDecisionLogKey)
    }

    private func showSpinner(over webView: WKWebView) {
        // The webview's own superview (AppDelegate's safe-area-pinned container — see its own
        // remarks), not the webview itself: adding a plain UIView as a WKWebView's own direct
        // subview risks interfering with WKWebView's own internal view hierarchy, which it
        // manages itself. Read fresh here rather than captured once at init — the view hierarchy
        // may not be fully attached yet at viewDidLoad time.
        guard let hostView = webView.superview, spinnerView == nil else { return }

        // A real frame of the outgoing page, if one's on hand — captured proactively, after the
        // previous navigation settled (see scheduleSnapshotCapture()'s own remarks for why that
        // timing, and not this one, is the only one this API is documented to work reliably at).
        // Shown synchronously: no async call happens here, so there's no timing race left to get
        // wrong at the point it matters. With nothing cached yet (the very first navigation after
        // launch), only the spinner below appears — WKWebView's own default rendering is left
        // alone rather than covering it with a placeholder that isn't actually a frame of
        // anything.
        if let snapshot = lastGoodSnapshot {
            let imageView = UIImageView(image: snapshot)
            imageView.contentMode = .top
            imageView.clipsToBounds = true
            imageView.frame = webView.frame
            hostView.addSubview(imageView)
            snapshotOverlayView = imageView
        }

        let spinner = UIActivityIndicatorView(style: .medium)
        spinner.hidesWhenStopped = true
        hostView.addSubview(spinner)
        spinnerView = spinner

        // Reported live: a dead-center spinner is easy to miss on a slow connection, because the
        // user's eyes are already on wherever they just tapped, not the screen's center. Anchored
        // there instead when a recent-enough tap is on record — clamped inward so it's never
        // clipped by the view's own edges — so it lands exactly where attention already is. Falls
        // back to dead-center (the previous behaviour) otherwise: a navigation can also come from
        // a redirect, a JS-driven navigation, or simply an old tap from well before this one
        // started, none of which should place a spinner somewhere the user isn't looking any more.
        var center = CGPoint(x: webView.frame.midX, y: webView.frame.midY)
        if let tap = lastTapLocation, Date().timeIntervalSince(tap.at) < 2.0 {
            center = webView.convert(tap.point, to: hostView)
        }
        let inset = max(spinner.bounds.width, spinner.bounds.height)
        let clampBounds = hostView.bounds.insetBy(dx: inset, dy: inset)
        if clampBounds.width > 0 && clampBounds.height > 0 {
            center.x = min(max(center.x, clampBounds.minX), clampBounds.maxX)
            center.y = min(max(center.y, clampBounds.minY), clampBounds.maxY)
        }
        spinner.center = center

        let reveal = DispatchWorkItem { spinner.startAnimating() }
        spinnerRevealWorkItem = reveal
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.1, execute: reveal)

        showDiagnosticLabel(over: hostView, webView: webView)
    }

    private func hideSpinner() {
        spinnerRevealWorkItem?.cancel()
        spinnerRevealWorkItem = nil
        spinnerView?.removeFromSuperview()
        spinnerView = nil
        snapshotOverlayView?.removeFromSuperview()
        snapshotOverlayView = nil
        // The diagnostic label outlives the spinner/snapshot by a couple of seconds (see its own
        // remarks) rather than disappearing the instant the new page paints — it needs to still be
        // readable after the transition it's describing has already finished.
    }

    // Only ever wired up (see viewDidLoad's own remarks) when PrismMobileDiagnosticsFlag.enabled —
    // a build that doesn't have diagnostics on never creates the gesture recognizer that could
    // call this. Two fingers (numberOfTouchesRequired, set at the call site) plus a 1.5s hold
    // together rule out an accidental trigger from ordinary scrolling/tapping/WebKit's own
    // ~0.5s single-finger long-press-for-selection gesture — deliberately not interfering with
    // that (see viewDidLoad's own remarks on why a different touch signature, not a screen
    // region, is what actually keeps the two apart).
    @objc fileprivate func handleDiagnosticsGesture(_ recognizer: UILongPressGestureRecognizer) {
        guard recognizer.state == .began else { return }
        isDiagnosticsRevealed.toggle()
        UIImpactFeedbackGenerator(style: .light).impactOccurred()
    }

    // Wired up unconditionally in viewDidLoad (not gated on PrismMobileDiagnosticsFlag — this is
    // a real product fix, not a diagnostic aid). Records where showSpinner() should anchor its
    // spinner for the *next* navigation this tap triggers. The recency check happens in
    // showSpinner() itself, not here, since how stale a tap is allowed to be before falling back
    // to dead-center is a property of when it's read, not when it's recorded.
    @objc fileprivate func handleTapForSpinnerPositioning(_ recognizer: UITapGestureRecognizer) {
        guard recognizer.state == .ended, let view = recognizer.view else { return }
        lastTapLocation = (recognizer.location(in: view), Date())
    }

    // Lets this gesture recognize alongside WKWebView's own internal ones (its own single-finger
    // long-press for text selection among them) rather than one silently blocking the other —
    // they target different touch counts already, so this is defence in depth, not the primary
    // fix (that's requiring two fingers at all — see viewDidLoad's own remarks).
    func gestureRecognizer(_ gestureRecognizer: UIGestureRecognizer, shouldRecognizeSimultaneouslyWith otherGestureRecognizer: UIGestureRecognizer) -> Bool {
        true
    }

    // Gated on two independent things: PrismMobileDiagnosticsFlag.enabled (a build-time constant —
    // this bundle either was or wasn't produced with diagnostics on, and if not, nothing below
    // this guard is reachable at all) and isDiagnosticsRevealed (a per-session runtime toggle via
    // the long-press gesture — so a diagnostics-enabled build still shows nothing until someone
    // deliberately asks for it). Reflects exactly what showSpinner() is about to show (or not
    // show) for THIS navigation, not a live-updating log — simplest thing that answers "did this
    // navigation have a cached frame, how did it get there, and how stale is it."
    private func showDiagnosticLabel(over hostView: UIView, webView: WKWebView) {
        guard PrismMobileDiagnosticsFlag.enabled, isDiagnosticsRevealed else { return }

        diagnosticDismissWorkItem?.cancel()
        diagnosticLabel?.removeFromSuperview()

        let ageDescription: String
        if let lastCaptureAt {
            ageDescription = String(format: "%.1fs", Date().timeIntervalSince(lastCaptureAt))
        } else {
            ageDescription = "n/a"
        }
        // build/marketing-version included specifically so a stale-TestFlight-build question
        // never has to be a guess: CFBundleVersion is CURRENT_PROJECT_VERSION at archive time,
        // set to the CI run number in deploy-testflight.yml — a plain, always-unique, always-
        // increasing per-deploy counter to compare against the workflow run that actually shipped
        // whatever's being tested right now.
        let build = Bundle.main.infoDictionary?["CFBundleVersion"] as? String ?? "?"
        let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "?"
        let text = "paint-diag v\(version)(\(build)) snap=\(lastGoodSnapshot != nil ? "yes" : "no") src=\(lastCaptureSource) age=\(ageDescription) vp=\(PrismBridgeViewController.viewportScriptPingCount) plugin=\(PrismContentWatcherPlugin.callCount) ws=\(PrismBridgeViewController.contentWatcherScriptStartedPingCount) wr=\(PrismBridgeViewController.contentWatcherReadyPingCount) wm=\(PrismBridgeViewController.contentWatcherMutationPingCount) we=\(PrismBridgeViewController.contentWatcherErrorPingCount) chg=\(contentChangeSignalCount) ok=\(captureSuccessCount) fail=\(captureFailureCount)"

        // Piggybacks on the same label/reveal gesture rather than a separate view — see
        // recordNavigationDecision's own remarks on why this is captured via UserDefaults
        // regardless of whether anyone had this label open at the time. Shown here (a completely
        // unrelated bug's diagnostics) rather than only right after a sign-out attempt because
        // sign-out leaves the app entirely — this may be the first moment back in it with
        // anywhere left to show a label at all.
        let navLog = UserDefaults.standard.stringArray(forKey: Self.navigationDecisionLogKey) ?? []
        let navLogText = navLog.isEmpty ? "" : "\nnav-log (last \(navLog.count)):\n" + navLog.joined(separator: "\n")

        let label = UILabel()
        label.text = text + navLogText
        label.font = .monospacedSystemFont(ofSize: 10, weight: .regular)
        label.textColor = .white
        label.backgroundColor = UIColor.black.withAlphaComponent(0.6)
        label.numberOfLines = 0
        label.textAlignment = .center
        label.isUserInteractionEnabled = false
        label.translatesAutoresizingMaskIntoConstraints = false
        hostView.addSubview(label)
        diagnosticLabel = label
        NSLayoutConstraint.activate([
            label.leadingAnchor.constraint(equalTo: webView.leadingAnchor),
            label.trailingAnchor.constraint(equalTo: webView.trailingAnchor),
            label.bottomAnchor.constraint(equalTo: webView.bottomAnchor)
        ])

        let dismiss = DispatchWorkItem { [weak label] in label?.removeFromSuperview() }
        diagnosticDismissWorkItem = dismiss
        // Longer when there's a navigation log to read too — that's several lines of URLs, not
        // the usual one-liner, and needs real time to actually read rather than just glimpse.
        let dismissDelay: TimeInterval = navLog.isEmpty ? 2.5 : 8.0
        DispatchQueue.main.asyncAfter(deadline: .now() + dismissDelay, execute: dismiss)
    }

    // 0.3s (didFinish, waiting out trailing load-time rendering) or 0.1s (contentDidChange,
    // waiting out a burst of typing/scrolling) — see each call site's own remarks. Neither is a
    // device-measured constant; worth revisiting with real measurements once this can be tested
    // live. Cancels any capture still pending from a previous trigger first — didFinish and
    // contentDidChange can each fire multiple times in quick succession (a fast navigation, or a
    // user still actively typing), and only the most recent trigger's delay should count, not a
    // pile-up of independently-scheduled timers.
    private func scheduleSnapshotCapture(of webView: WKWebView, delay: TimeInterval, source: String) {
        pendingSnapshotCapture?.cancel()
        pendingCaptureSource = source
        let capture = DispatchWorkItem { [weak self, weak webView] in
            guard let self, let webView else { return }
            self.captureSnapshot(of: webView)
        }
        pendingSnapshotCapture = capture
        DispatchQueue.main.asyncAfter(deadline: .now() + delay, execute: capture)
    }

    // Cancelling the DispatchWorkItem above only stops a capture that hasn't started yet — once
    // takeSnapshot itself has actually been called, that async call is in flight and isn't
    // something a cancelled DispatchWorkItem can stop. isCaptureInFlight guards against a second
    // trigger starting an overlapping takeSnapshot call while one's already running: it's deferred
    // instead (captureNeededAfterInFlight), so a change that arrives mid-capture still eventually
    // gets its own fresh snapshot rather than being silently dropped — just delayed until right
    // after the current one finishes, never running two at once.
    private func captureSnapshot(of webView: WKWebView) {
        guard !isCaptureInFlight else {
            captureNeededAfterInFlight = true
            return
        }
        isCaptureInFlight = true
        // Confirmed reliable at this timing, unlike at navigation start (see the class-level
        // remarks) — but still validated before caching: a nil result (the API can still decline,
        // e.g. mid-memory-pressure) leaves the previous cached frame in place rather than being
        // treated as a valid "blank page" to show next time.
        let source = pendingCaptureSource
        webView.takeSnapshot(with: nil) { [weak self, weak webView] image, _ in
            guard let self else { return }
            if let image {
                self.lastGoodSnapshot = image
                self.lastCaptureSource = source
                self.lastCaptureAt = Date()
                self.captureSuccessCount += 1
            } else {
                self.captureFailureCount += 1
            }
            self.isCaptureInFlight = false
            if self.captureNeededAfterInFlight {
                self.captureNeededAfterInFlight = false
                if let webView {
                    self.captureSnapshot(of: webView)
                }
            }
        }
    }
}
PRISM_SWIFT_EOF
  echo "✓ PrismBridgeViewController.swift written"

  echo "Pinning the app's root view to the safe area layout guide..."
  cat > ios/App/App/AppDelegate.swift << 'PRISM_APPDELEGATE_EOF'
import UIKit
import Capacitor

@UIApplicationMain
class AppDelegate: UIResponder, UIApplicationDelegate {

    var window: UIWindow?

    func application(_ application: UIApplication, didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        // Pins the storyboard's own root view controller (PrismBridgeViewController — see its
        // own remarks) inside a plain wrapper whose view is safe-area-pinned, via standard view
        // controller containment. Not done inside PrismBridgeViewController itself: Capacitor's
        // own loadView() is `final` and unconditionally does `view = webView` — the view
        // controller's `view` and its `webView` are literally the same object, not a webview
        // nested inside some container Prism could re-constrain. Confirmed live (reading
        // Capacitor's own vendored source, and by two failed attempts guided by the wrong
        // assumption): trying to reconstrain "the webview" from inside that view controller is
        // trying to reconstrain a view relative to itself. Wrapping from the outside — one
        // level up, in the window's own root view controller — sidesteps that entirely and
        // needs nothing Capacitor doesn't already fully support (its bridge view controller
        // works the same as a child VC as it does as the window's direct root).
        if let bridgeViewController = window?.rootViewController {
            let container = UIViewController()
            container.view.backgroundColor = .white
            container.addChild(bridgeViewController)
            container.view.addSubview(bridgeViewController.view)
            bridgeViewController.view.translatesAutoresizingMaskIntoConstraints = false
            NSLayoutConstraint.activate([
                bridgeViewController.view.topAnchor.constraint(equalTo: container.view.safeAreaLayoutGuide.topAnchor),
                bridgeViewController.view.bottomAnchor.constraint(equalTo: container.view.safeAreaLayoutGuide.bottomAnchor),
                bridgeViewController.view.leadingAnchor.constraint(equalTo: container.view.leadingAnchor),
                bridgeViewController.view.trailingAnchor.constraint(equalTo: container.view.trailingAnchor)
            ])
            bridgeViewController.didMove(toParent: container)
            window?.rootViewController = container
        }
        return true
    }

    func applicationWillResignActive(_ application: UIApplication) {
        // Sent when the application is about to move from active to inactive state. This can occur for certain types of temporary interruptions (such as an incoming phone call or SMS message) or when the user quits the application and it begins the transition to the background state.
        // Use this method to pause ongoing tasks, disable timers, and invalidate graphics rendering callbacks. Games should use this method to pause the game.
    }

    func applicationDidEnterBackground(_ application: UIApplication) {
        // Use this method to release shared resources, save user data, invalidate timers, and store enough application state information to restore your application to its current state in case it is terminated later.
        // If your application supports background execution, this method is called instead of applicationWillTerminate: when the user quits.
    }

    func applicationWillEnterForeground(_ application: UIApplication) {
        // Called as part of the transition from the background to the active state; here you can undo many of the changes made on entering the background.
    }

    func applicationDidBecomeActive(_ application: UIApplication) {
        // Restart any tasks that were paused (or not yet started) while the application was inactive. If the application was previously in the background, optionally refresh the user interface.
    }

    func applicationWillTerminate(_ application: UIApplication) {
        // Called when the application is about to terminate. Save data if appropriate. See also applicationDidEnterBackground:.
    }

    func application(_ app: UIApplication, open url: URL, options: [UIApplication.OpenURLOptionsKey: Any] = [:]) -> Bool {
        // Called when the app was launched with a url. Feel free to add additional processing here,
        // but if you want the App API to support tracking app url opens, make sure to keep this call
        return ApplicationDelegateProxy.shared.application(app, open: url, options: options)
    }

    func application(_ application: UIApplication, continue userActivity: NSUserActivity, restorationHandler: @escaping ([UIUserActivityRestoring]?) -> Void) -> Bool {
        // Called when the app was launched with an activity, including Universal Links.
        // Feel free to add additional processing here, but if you want the App API to support
        // tracking app url opens, make sure to keep this call
        return ApplicationDelegateProxy.shared.application(application, continue: userActivity, restorationHandler: restorationHandler)
    }

}
PRISM_APPDELEGATE_EOF
  echo "✓ AppDelegate.swift written"

  STORYBOARD="ios/App/App/Base.lproj/Main.storyboard"
  if [ -f "$STORYBOARD" ]; then
    if grep -q 'customClass="CAPBridgeViewController"' "$STORYBOARD"; then
      sed -i.bak 's/customClass="CAPBridgeViewController" customModule="Capacitor"/customClass="PrismBridgeViewController" customModule="App"/' "$STORYBOARD"
      rm -f "$STORYBOARD.bak"
      echo "✓ Main.storyboard wired to PrismBridgeViewController"
    else
      echo "✓ Main.storyboard already wired to PrismBridgeViewController"
    fi
  else
    echo "⚠️ Main.storyboard not found. Run 'npx cap add ios' first."
  fi

  cat > .prism-add-swift-file.mjs << 'PRISM_NODE_EOF'
import xcode from 'xcode';
import fs from 'node:fs';

const pbxprojPath = 'ios/App/App.xcodeproj/project.pbxproj';
const project = xcode.project(pbxprojPath);
project.parseSync();

const refs = project.hash.project.objects.PBXFileReference || {};
const alreadyPresent = Object.values(refs).some(
  ref => ref && typeof ref === 'object' && typeof ref.path === 'string' && ref.path.includes('PrismBridgeViewController.swift')
);

let pbxprojDirty = false;

if (!alreadyPresent) {
  const target = project.getFirstTarget().uuid;
  project.addSourceFile('App/PrismBridgeViewController.swift', { target }, 'App');
  pbxprojDirty = true;
  console.log('✓ PrismBridgeViewController.swift registered in project.pbxproj');
} else {
  console.log('✓ PrismBridgeViewController.swift already registered in project.pbxproj');
}

// Capacitor's own iOS template defaults IPHONEOS_DEPLOYMENT_TARGET to 14.0. Not urgent today —
// App Store Connect still accepts a 14.0 upload, just flagging it (warning 90068) — but Apple's
// own notice on that warning states 15.0 becomes a hard floor for uploads/submissions starting
// Spring 2027, so there's no reason to keep shipping a value already known to stop working.
// updateBuildProperty with no build/targetName filter applies across every configuration and
// every target, matching how a single Xcode "Deployment Target" field edit would behave.
project.updateBuildProperty('IPHONEOS_DEPLOYMENT_TARGET', '15.0');
pbxprojDirty = true;
console.log('✓ IPHONEOS_DEPLOYMENT_TARGET set to 15.0 in project.pbxproj');

if (pbxprojDirty) {
  fs.writeFileSync(pbxprojPath, project.writeSync());
}
PRISM_NODE_EOF
  node .prism-add-swift-file.mjs
  rm -f .prism-add-swift-file.mjs
else
  echo "⚠️ ios/App/App not found. Run 'npx cap add ios' first."
fi

""";

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
{{infoPlistInjection}}{{zoomFixInjection}}
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

    private static string BuildBootstrapAndroidScript(bool biometricAuthEnabled)
    {
        var manifestInjection = biometricAuthEnabled
            ? """

echo "Injecting USE_BIOMETRIC permission into AndroidManifest.xml..."
MANIFEST_PATH="android/app/src/main/AndroidManifest.xml"
if [ -f "$MANIFEST_PATH" ]; then
  if ! grep -q "android.permission.USE_BIOMETRIC" "$MANIFEST_PATH"; then
    # Insert USE_BIOMETRIC permission before the <application> tag (perl for macOS/Linux compat)
    perl -i -pe 's|(<application)|    <uses-permission android:name="android.permission.USE_BIOMETRIC" />\n$1|' "$MANIFEST_PATH"
    echo "✓ USE_BIOMETRIC permission added to AndroidManifest.xml"
  else
    echo "✓ USE_BIOMETRIC permission already present in AndroidManifest.xml"
  fi
else
  echo "⚠️ AndroidManifest.xml not found. Run 'npx cap add android' first."
fi

"""
            : string.Empty;

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

npx cap sync android

echo "Generating app icon and splash screen from resources/icon.svg..."
npx capacitor-assets generate --android
{{manifestInjection}}
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

    private static string BuildAgentPrompt(string appName, string startUrl, bool biometricAuthEnabled)
    {
        var biometricContext = biometricAuthEnabled
            ? """

## Biometric authentication

This bundle has biometric auth enabled. The bootstrap scripts inject platform entitlements automatically:

- **iOS:** `NSFaceIDUsageDescription` is added to `Info.plist`.
- **Android:** `USE_BIOMETRIC` permission is added to `AndroidManifest.xml`.

Plugins `@aparajita/capacitor-biometric-auth` and `@aparajita/capacitor-secure-storage` are in `package.json`
and auto-register via Capacitor discovery.

Simulator testing notes:
- iOS Simulator: `BiometricAuth.checkBiometry()` returns `isAvailable: false`. Use *Features → Face ID → Enrolled* for simulated match.
- Android Emulator: enroll a fingerprint with `adb emu finger touch 1`.
"""
            : string.Empty;

        return $$"""
# Prism Mobile Agent Prompt

You are helping bootstrap the generated "{{appName}}" Capacitor app.

## Goal

Get this app running in an emulator as quickly as possible.

## Deterministic sequence

1. Run `npm install`
2. Run `npm run doctor`
3. For iOS: run `npm run bootstrap:ios`
4. For Android: run `npm run bootstrap:android`

## Context

- Start URL: `{{startUrl}}`
- If localhost HTTPS fails on iOS (`NSURLErrorDomain -1202`), run `bash scripts/trust-ios-localhost-cert.sh`.
- If iOS/Android platform missing, run `npx cap add ios` / `npx cap add android` before sync/open.

## Troubleshooting hints

- iOS: verify Xcode + CocoaPods installed and simulator booted.
- Android: verify Android SDK/adb and an active emulator/device.
- Re-run `npm run doctor` after each fix.
{{biometricContext}}
""";
    }

    private static string BuildIosInfoPlistAdditions(string appName)
    {
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

    private static string BuildAndroidManifestAdditions()
    {
        return """
<!--
  Android manifest additions for biometric authentication.
  The bootstrap-android.sh script injects these automatically.
  If you need to add them manually, add this permission inside the <manifest> element
  of android/app/src/main/AndroidManifest.xml.
-->
<uses-permission android:name="android.permission.USE_BIOMETRIC" />
""";
    }

    private static string EscapeSingleQuotes(string value) => value.Replace("'", "\\'");

    private static string ToJsonStringOrNull(string? value)
    {
      if (string.IsNullOrWhiteSpace(value)) return "null";
      var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
      return $"\"{escaped}\"";
    }

    private static string ToJsonBoolean(bool value) => value ? "true" : "false";
}
