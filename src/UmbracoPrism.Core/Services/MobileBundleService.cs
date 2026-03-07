using System.IO.Compression;
using System.Text;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

public class MobileBundleService : IMobileBundleService
{
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

        if (!IsValidAppId(appId))
        {
            throw new ArgumentException("App ID must be a reverse-domain identifier, e.g. com.example.portal");
        }

        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
          AddEntry(archive, "README.md", BuildReadme(appName, startUrl, iconUrl, splashUrl));
            AddEntry(archive, "package.json", BuildPackageJson(appName));
            AddEntry(archive, "capacitor.config.ts", BuildCapacitorConfig(appId, appName, version, startUrl, marker));
            AddEntry(archive, ".gitignore", "node_modules\nandroid\nios\n.DS_Store\n");
            AddEntry(archive, "www/index.html", BuildPlaceholderIndex(appName, startUrl));
            AddEntry(archive, "www/mobile-overrides.css", BuildMobileOverrideTemplate());
          AddEntry(archive, "resources/mobile-assets.json", BuildAssetsManifest(iconUrl, splashUrl));
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

    private static string BuildPackageJson(string appName)
    {
        return $$"""
{
  "name": "{{ToSafeIdentifier(appName)}}-mobile",
  "private": true,
  "version": "1.0.0",
  "description": "Generated Prism mobile shell",
  "scripts": {
    "sync": "npx cap sync",
    "open:ios": "npx cap open ios",
    "open:android": "npx cap open android"
  },
  "dependencies": {
    "@capacitor/core": "^7.0.0"
  },
  "devDependencies": {
    "@capacitor/cli": "^7.0.0",
    "@capacitor/android": "^7.0.0",
    "@capacitor/ios": "^7.0.0"
  }
}
""";
    }

    private static string BuildCapacitorConfig(string appId, string appName, string version, string startUrl, string marker)
    {
        var cleartext = startUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "true" : "false";

        return $$"""
import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: '{{appId}}',
  appName: '{{EscapeSingleQuotes(appName)}}',
  webDir: 'www',
  bundledWebRuntime: false,
  appendUserAgent: '{{EscapeSingleQuotes(marker)}}',
  server: {
    url: '{{EscapeSingleQuotes(startUrl)}}',
    cleartext: {{cleartext}}
  },
  plugins: {
    SplashScreen: {
      launchAutoHide: true
    }
  }
};

export default config;
""";
    }

    private static string BuildReadme(string appName, string startUrl, string? iconUrl, string? splashUrl)
    {
      var iconLine = string.IsNullOrWhiteSpace(iconUrl) ? "(not provided)" : iconUrl;
      var splashLine = string.IsNullOrWhiteSpace(splashUrl) ? "(not provided)" : splashUrl;

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
2. Sync native platforms:
   ```bash
   npm run sync
   ```
3. Open platform projects:
   ```bash
   npm run open:ios
   npm run open:android
   ```

## Runtime behavior

- Prism detects mobile mode using the appended user-agent marker.
- Tenant branding overrides are applied first.
- Mobile branding overrides are applied after tenant overrides.

## Customize mobile-specific UI

Use `www/mobile-overrides.css` as your starting point for mobile-scoped styles.

## Icons & Splash

- Icon source: {{iconLine}}
- Splash source: {{splashLine}}

Add final platform assets under the `resources/` folder and run Capacitor asset generation.
The `resources/mobile-assets.json` file stores the values entered in Backoffice for reference.
""";
    }

  private static string BuildAssetsManifest(string? iconUrl, string? splashUrl)
  {
    return $$"""
{
  "iconUrl": {{ToJsonStringOrNull(iconUrl)}},
  "splashUrl": {{ToJsonStringOrNull(splashUrl)}},
  "notes": "Download and convert to final app assets (recommended 1024x1024 icon and high-resolution splash)"
}
""";
  }

    private static string BuildPlaceholderIndex(string appName, string startUrl)
    {
        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>{{appName}} Mobile</title>
</head>
<body>
  <noscript>This app shell requires JavaScript.</noscript>
  <script>
    window.location.replace('{{EscapeSingleQuotes(startUrl)}}');
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

/* App-shell styling examples */
.prism-mobile .desktop-nav {
  display: none;
}

.prism-mobile .mobile-nav {
  display: flex;
}
""";
    }

    private static string EscapeSingleQuotes(string value) => value.Replace("'", "\\'");

    private static string ToJsonStringOrNull(string? value)
    {
      if (string.IsNullOrWhiteSpace(value)) return "null";
      var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
      return $"\"{escaped}\"";
    }
}
