using System.Text;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Persistence;
using static UmbracoPrism.Core.Services.MobileBundles.BundleText;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// The Capacitor project itself: package.json, capacitor.config.ts, the asset manifest and the environment doctor script.
/// </summary>
internal static class CapacitorProjectFiles
{
    internal static string BuildPackageJson(MobileBundleSettings settings)
    {
        var appName = settings.AppName;
        var biometricAuthEnabled = settings.BiometricAuthEnabled;
        var pushNotificationsEnabled = settings.PushNotificationsEnabled;

        // @capacitor/app: unconditional, not a flag-gated dep like the ones below — every
        // generated app needs its own 'resume' lifecycle event, not just ones with biometric
        // auth or push notifications on. Wayfinder's own wayfinder-poll.js (the join-gateway
        // waiting stage) listens for it directly when window.Capacitor is present: a mobile
        // WebView's document visibilitychange isn't always reliable for an app-level background/
        // foreground transition, and a citizen who backgrounds the app mid-wait needs the page to
        // re-check on return rather than sit on a stale setTimeout that may never actually have
        // fired while backgrounded. First-party Capacitor plugin, auto-linked by `npx cap sync`
        // (already run for every generated app), so no native code of our own is needed.
        var biometricDeps = biometricAuthEnabled
            ? """
,
    "@aparajita/capacitor-biometric-auth": "^7.0.0",
    "@aparajita/capacitor-secure-storage": "^7.0.0"
"""
            : string.Empty;

        // @capacitor-firebase/messaging (not @capacitor/push-notifications) — it bridges iOS
        // APNs tokens through Firebase's own SDK into FCM tokens, so both platforms hand the
        // server a token type PrismNotificationService (FirebaseAdmin.Messaging, FCM-only) can
        // actually send to. @capacitor/push-notifications alone would give iOS a raw APNs token
        // FCM can't target directly.
        var pushDeps = pushNotificationsEnabled
            ? """
,
    "@capacitor-firebase/messaging": "^7.3.0",
    "firebase": "^11.0.0"
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
    "@capacitor/core": "^7.0.0",
    "@capacitor/app": "^7.0.0"{{biometricDeps}}{{pushDeps}}
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

    internal static string BuildCapacitorConfig(MobileBundleSettings settings)
    {
        var tenant = settings.Tenant;
        var appId = settings.AppId;
        var appName = settings.AppName;
        var version = settings.Version;
        var startUrl = settings.StartUrl;
        var marker = settings.UserAgentMarker;

      var uri = new Uri(startUrl);
      var mobileStartUrl = AddPrismMobileQueryFlag(startUrl);
      var cleartext = uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
      var allowNavigationHosts = BuildAllowNavigationHosts(tenant, uri);
      var allowNavigationJs = string.Join(", ", allowNavigationHosts.Select(host => $"'{EscapeSingleQuotes(host)}'"));
      var appUserAgent = EscapeSingleQuotes(AppUserAgent(settings));
      // A service worker runs in WKWebView only for the domains the app declares as its own (see
      // ios-saved-pages.sh), and only once the app keeps its webviews to them.
      var appBoundDomains = settings.SavedPagesEnabled
          ? ",\n    // Saved pages need a service worker, which iOS runs only for the app's own domains.\n    limitsNavigationsToAppBoundDomains: true"
          : string.Empty;

        return $$"""
import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: '{{appId}}',
  appName: '{{EscapeSingleQuotes(appName)}}',
  webDir: 'www',
  bundledWebRuntime: false,
  ios: {
    // The tabs are native, so the site leaves out its web tab bar, and sign-in uses the system
    // sign-in sheet (see prism-system-sign-in.js).
    appendUserAgent: '{{appUserAgent}}',
    // The native shell positions each tab's webview itself (see PrismShell.swift), so WebKit
    // must not add its own safe-area inset on top.
    contentInset: 'never'{{appBoundDomains}}
  },
  android: {
    appendUserAgent: '{{appUserAgent}}',
    // The native shell keeps each tab's webview clear of the system bars itself (see MainActivity.java).
    adjustMarginsForEdgeToEdge: 'disable'
  },
  appendUserAgent: '{{EscapeSingleQuotes(marker)}}',
  server: {
    url: '{{EscapeSingleQuotes(mobileStartUrl)}}',
    cleartext: {{cleartext}},
    errorPath: 'offline.html',
    allowNavigation: [{{allowNavigationJs}}]
  },
  plugins: {
    // Every tab's bridge loads @capacitor/app; the native shell decides what Back does instead.
    App: {
      disableBackButtonHandler: true
    },
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

  /// <summary>What the app adds to its user agent, which tells the site what the app does natively.</summary>
  private static string AppUserAgent(MobileBundleSettings settings)
  {
    var tokens = new List<string>
    {
      settings.UserAgentMarker,
      PrismMobileRequestDetection.NativeTabsUserAgentToken,
      PrismMobileRequestDetection.SystemSignInUserAgentToken
    };
    if (settings.SavedPagesEnabled)
    {
      tokens.Add(PrismMobileRequestDetection.SavedPagesUserAgentToken);
    }

    return string.Join(' ', tokens);
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

  internal static string AddPrismMobileQueryFlag(string startUrl)
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

  internal static string BuildAssetsManifest(MobileBundleSettings settings)
  {
      var iconUrl = settings.IconUrl;
      var splashUrl = settings.SplashUrl;
      var errorBackgroundColor = settings.ErrorBackgroundColor;
      var errorTextColor = settings.ErrorTextColor;
      var errorTitle = settings.ErrorTitle;
      var errorMessage = settings.ErrorMessage;
      var showErrorDiagnostics = settings.ShowErrorDiagnostics;

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

    internal static string BuildDoctorScript(MobileBundleSettings settings)
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
}
