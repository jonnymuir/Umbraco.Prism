using System.Net;
using System.Text;
using UmbracoPrism.Core.Persistence;
using static UmbracoPrism.Core.Services.MobileBundles.BundleText;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// The web assets in <c>www/</c> and <c>resources/</c> that the native shell loads before it hands over to the tenant's site.
/// </summary>
internal static class WebShellFiles
{
    private static string BuildShellStyles(MobileBundleSettings settings) =>
        "  <style>\n"
        + BundleTemplates.Read("shell-error-card.css")
            .Replace("@@PRISM_ERROR_BG@@", EscapeSingleQuotes(settings.ErrorBackgroundColor))
            .Replace("@@PRISM_ERROR_TEXT@@", EscapeSingleQuotes(settings.ErrorTextColor))
        + "  </style>\n";

    /// <summary>
    /// Shown by Capacitor (<c>server.errorPath</c>) whenever a page from the tenant's site fails
    /// to load, so a dropped connection mid-session gets the tenant's own branded screen with a
    /// retry instead of a blank WebView.
    /// </summary>
    internal static string BuildOfflinePage(MobileBundleSettings settings)
    {
        var appName = WebUtility.HtmlEncode(settings.AppName);
        var startUrl = EscapeSingleQuotes(CapacitorProjectFiles.AddPrismMobileQueryFlag(settings.StartUrl));

        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
  <title>{{appName}}</title>
{{BuildShellStyles(settings)}}</head>
<body>
  <main class="screen">
    <section class="card" role="alert">
      <h1>{{WebUtility.HtmlEncode(settings.ErrorTitle)}}</h1>
      <p>{{WebUtility.HtmlEncode(settings.ErrorMessage)}}</p>
      <div class="actions">
        <button id="retry" type="button" data-prism-offline-retry>Try again</button>
      </div>
    </section>
  </main>
  <script>
    function retry() {
      window.location.replace('{{startUrl}}');
    }
    document.getElementById('retry').addEventListener('click', retry);
    window.addEventListener('online', retry);
  </script>
</body>
</html>
""";
    }

    internal static string BuildPlaceholderIndex(MobileBundleSettings settings)
    {
        var appName = settings.AppName;
        var startUrl = settings.StartUrl;
        var errorBackgroundColor = settings.ErrorBackgroundColor;
        var errorTextColor = settings.ErrorTextColor;
        var errorTitle = settings.ErrorTitle;
        var errorMessage = settings.ErrorMessage;
        var showErrorDiagnostics = settings.ShowErrorDiagnostics;
        var biometricAuthEnabled = settings.BiometricAuthEnabled;

        var biometricStartupScript = biometricAuthEnabled
            ? BundleTemplates.Read("placeholder-biometric-startup.js")
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
{{BuildShellStyles(settings)}}</head>
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

    internal static string BuildMobileOverrideTemplate()
    {
        return BundleTemplates.Read("mobile-overrides.css");
    }

    internal static string BuildDefaultAppIconSvg()
    {
        // Sourced from assets/app-icon.svg (a derivative of assets/logo.svg with an opaque
        // background added — iOS App Store icons must have no alpha channel). Keep both in sync
        // by eye; there's no build step linking them, this is a small, rarely-changed asset.
        return BundleTemplates.Read("default-app-icon.svg");
    }
}
