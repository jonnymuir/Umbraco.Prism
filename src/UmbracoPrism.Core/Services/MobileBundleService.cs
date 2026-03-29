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
        var iconUrl = request.IconUrl?.Trim();
        var splashUrl = request.SplashUrl?.Trim();
        var errorBackgroundColor = string.IsNullOrWhiteSpace(request.ErrorBackgroundColor) ? "#0f172a" : request.ErrorBackgroundColor.Trim();
        var errorTextColor = string.IsNullOrWhiteSpace(request.ErrorTextColor) ? "#f8fafc" : request.ErrorTextColor.Trim();
        var errorTitle = string.IsNullOrWhiteSpace(request.ErrorTitle) ? "We’re having trouble connecting" : request.ErrorTitle.Trim();
        var errorMessage = string.IsNullOrWhiteSpace(request.ErrorMessage) ? "Please check your connection and try again." : request.ErrorMessage.Trim();
        var showErrorDiagnostics = request.ShowErrorDiagnostics ?? true;
        var biometricAuthEnabled = request.BiometricAuthEnabled ?? false;

        if (!IsValidAppId(appId))
        {
            throw new ArgumentException("App ID must be a reverse-domain identifier, e.g. com.example.portal");
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
            AddEntry(archive, "scripts/bootstrap-ios.sh", BuildBootstrapIosScript(startUrl, biometricAuthEnabled));
            AddEntry(archive, "scripts/bootstrap-android.sh", BuildBootstrapAndroidScript(biometricAuthEnabled));
            AddEntry(archive, "scripts/trust-ios-localhost-cert.sh", BuildTrustIosLocalhostCertScript(startUrl));
          AddEntry(archive, "resources/mobile-assets.json", BuildAssetsManifest(iconUrl, splashUrl, errorBackgroundColor, errorTextColor, errorTitle, errorMessage, showErrorDiagnostics));

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
    "typescript": "^5.7.0"
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
    contentInset: 'automatic'
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
- Generated config sets iOS `contentInset: 'automatic'` and `StatusBar.overlaysWebView: false` for safer default viewport behavior.
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
  "notes": "Download and convert to final app assets (recommended 1024x1024 icon and high-resolution splash)"
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

    async function tryBiometricSignIn() {
      try {
        console.log('[Prism Bio] tryBiometricSignIn: starting');
        var Cap = window.Capacitor;
        if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) {
          console.log('[Prism Bio] Not a native platform — skipping biometric');
          return false;
        }

        var tenantHost = new URL(prismBootstrap.startUrl).host;
        var SS_PREFIX = 'capacitor-storage_';
        var tokenKey = SS_PREFIX + 'prism_biometric_token_' + tenantHost;
        var enrollKey = 'prism_biometric_enrollment_state_' + tenantHost;
        var deviceIdKey = 'prism_device_id';

        // 1. Check stored biometric token (SecureStorage uses internalGetItem)
        console.log('[Prism Bio] Step 1: checking SecureStorage for token, key:', tokenKey);
        var storedResult = await Cap.nativePromise('SecureStorage', 'internalGetItem', {
          prefixedKey: tokenKey,
          sync: false
        });
        var storedToken = storedResult && storedResult.data ? JSON.parse(storedResult.data) : null;
        if (!storedToken) {
          console.log('[Prism Bio] Step 1: no stored token — not yet enrolled');
          return false;
        }
        console.log('[Prism Bio] Step 1: stored token found');

        // 2. Check biometry availability
        console.log('[Prism Bio] Step 2: checking biometry availability');
        var biometryInfo = await Cap.nativePromise('BiometricAuthNative', 'checkBiometry', {});
        console.log('[Prism Bio] Step 2: biometryInfo =', JSON.stringify(biometryInfo));
        if (!biometryInfo || !biometryInfo.isAvailable) {
          console.log('[Prism Bio] Step 2: biometry not available');
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
        console.log('[Prism Bio] Step 3: enrollment fingerprint =', fingerprint);
        var enrollResult = await Cap.nativePromise('Preferences', 'get', { key: enrollKey });
        var storedFingerprint = enrollResult && enrollResult.value;
        if (storedFingerprint && storedFingerprint !== fingerprint) {
          console.log('[Prism Bio] Step 3: enrollment changed — clearing token');
          await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: tokenKey, sync: false });
          await Cap.nativePromise('Preferences', 'remove', { key: enrollKey });
          return false;
        }

        // 4. Prompt biometric authentication
        console.log('[Prism Bio] Step 4: prompting biometric authentication');
        await Cap.nativePromise('BiometricAuthNative', 'internalAuthenticate', {
          reason: 'Sign in with biometrics',
          allowDeviceCredential: true,
          iosFallbackTitle: 'Use Passcode'
        });
        console.log('[Prism Bio] Step 4: biometric authentication passed');

        // 5. Get device ID
        var devResult = await Cap.nativePromise('Preferences', 'get', { key: deviceIdKey });
        var deviceId = devResult && devResult.value ? devResult.value : '';
        console.log('[Prism Bio] Step 5: deviceId =', deviceId || '(empty)');

        // 6. Exchange biometric token for PrismMemberCookie (Set-Cookie on response)
        console.log('[Prism Bio] Step 6: exchanging token with server');
        var resp = await fetch('https://' + tenantHost + '/umbraco/prism/mobile/biometric/exchange', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({ biometricToken: storedToken, deviceId: deviceId })
        });
        console.log('[Prism Bio] Step 6: exchange response status =', resp.status);

        if (!resp.ok) {
          if (resp.status === 401 || resp.status === 403) {
            console.log('[Prism Bio] Step 6: server rejected token — clearing stored credentials');
            await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: tokenKey, sync: false });
            await Cap.nativePromise('Preferences', 'remove', { key: enrollKey });
          }
          return false;
        }

        // Save updated enrollment fingerprint
        await Cap.nativePromise('Preferences', 'set', { key: enrollKey, value: fingerprint });
        console.log('[Prism Bio] Step 6: exchange successful — proceeding to app');
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
      console.log('[Prism Bio] Bootstrap: biometric auth enabled — attempting sign-in');
      const biometricOk = await tryBiometricSignIn();
      console.log('[Prism Bio] Bootstrap: tryBiometricSignIn returned', biometricOk);
      if (biometricOk) {
        window.location.replace(mobileStartUrl);
        return;
      }
      console.log('[Prism Bio] Bootstrap: biometric did not sign in — falling through to Entra');

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

    async function bootstrap() {
      console.log('[Prism] bootstrap() called');
      setLoading();      const result = await canReachStartUrl();
      if (result.ok) {
        window.location.replace(mobileStartUrl);
        return;
      }

      showError(result);
    }

    retryButton.addEventListener('click', bootstrap);
    bootstrap();
{{biometricStartupScript}}  </script>
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

    private static string BuildBootstrapIosScript(string startUrl, bool biometricAuthEnabled)
    {
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
{{infoPlistInjection}}
echo "Applying localhost cert trust (if needed)..."
if ! bash scripts/trust-ios-localhost-cert.sh; then
  echo "⚠️ Cert trust step did not complete. Continuing..."
fi

if xcrun simctl list devices booted | grep -q "(Booted)"; then
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
    # Insert USE_BIOMETRIC permission before the <application> tag
    sed -i.bak '/<application/i\    <uses-permission android:name="android.permission.USE_BIOMETRIC" />' "$MANIFEST_PATH"
    rm -f "$MANIFEST_PATH.bak"
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

npx cap sync android
{{manifestInjection}}
if command -v adb >/dev/null 2>&1 && adb devices | tail -n +2 | grep -q "device"; then
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
