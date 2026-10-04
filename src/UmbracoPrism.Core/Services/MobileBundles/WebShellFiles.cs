using System.Text;
using UmbracoPrism.Core.Persistence;
using static UmbracoPrism.Core.Services.MobileBundles.BundleText;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// The web assets in <c>www/</c> and <c>resources/</c> that the native shell loads before it hands over to the tenant's site.
/// </summary>
internal static class WebShellFiles
{
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
