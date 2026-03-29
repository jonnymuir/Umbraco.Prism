using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Injects tenant branding overrides into HTML responses.
/// </summary>
/// <param name="next">The next middleware delegate in the pipeline.</param>
public class PrismBrandingMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Applies branding and mobile-shell response transformations for eligible HTML responses.
    /// </summary>
    /// <param name="context">The current HTTP request and response context.</param>
    /// <param name="prismContext">Scoped Prism context containing the resolved tenant and branding overrides.</param>
    /// <returns>A task that completes after optional HTML injection and downstream middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context, IPrismContext prismContext)
    {
        PersistMobileQueryFlagAsCookie(context);

        var tenant = prismContext.CurrentTenant;
        var overrides = tenant?.BrandingOverrides;
        var mobileOverrides = tenant?.MobileBrandingOverrides;
        var overrideDeclarations = tenant?.BrandingCssDeclarations;
        var mobileOverrideDeclarations = tenant?.MobileBrandingCssDeclarations;
        var isPrismMobileRequest = PrismMobileRequestDetection.IsPrismMobileRequest(context);
        var injectBiometricEnroll = isPrismMobileRequest
            && (tenant?.AllowBiometricLogin ?? false)
            && context.User.Identity?.IsAuthenticated == true;
        var tenantHost = tenant?.Hostname ?? context.Request.Host.Value;
        var hasBaseOverrides = overrides is { Count: > 0 };
        var hasMobileOverrides = isPrismMobileRequest && mobileOverrides is { Count: > 0 };
        var hasMobileShellGuards = isPrismMobileRequest;

        if (!hasBaseOverrides && !hasMobileOverrides && !hasMobileShellGuards)
        {
            await next(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await next(context);

        context.Response.Body = originalBody;

        if (context.Request.Method == HttpMethods.Head
            || context.Response.StatusCode == StatusCodes.Status304NotModified
            || context.Response.StatusCode == StatusCodes.Status204NoContent
            || context.Response.StatusCode == StatusCodes.Status205ResetContent)
        {
            return;
        }

        if (context.WebSockets.IsWebSocketRequest
            || context.Request.Headers.ContainsKey("Upgrade")
            || context.Response.Headers.ContainsKey("Upgrade"))
        {
            return;
        }

        buffer.Seek(0, SeekOrigin.Begin);

        if (!IsHtmlResponseCandidate(context))
        {
            await WriteBufferToResponseAsync(context, buffer);
            return;
        }

        var bodyText = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
        if (!ShouldInject(context, bodyText))
        {
            buffer.Seek(0, SeekOrigin.Begin);
            await WriteBufferToResponseAsync(context, buffer);
            return;
        }

        var css = BuildCssOverrides(
            overrides,
            hasMobileOverrides ? mobileOverrides : null,
            overrideDeclarations,
            hasMobileOverrides ? mobileOverrideDeclarations : null);
        var injected = InjectBranding(bodyText, css, hasMobileShellGuards, injectBiometricEnroll, tenantHost);
        var bytes = Encoding.UTF8.GetBytes(injected);

        if (!context.Response.HasStarted)
        {
            context.Response.ContentLength = bytes.Length;
        }
        await context.Response.Body.WriteAsync(bytes);
    }

    private static async Task WriteBufferToResponseAsync(HttpContext context, MemoryStream buffer)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.ContentLength = buffer.Length;
        }

        await buffer.CopyToAsync(context.Response.Body);
    }

    private static bool IsHtmlResponseCandidate(HttpContext context)
    {
        var contentType = context.Response.ContentType;
        if (!string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = context.Request.Path.Value;
        if (!string.IsNullOrWhiteSpace(path) && Path.HasExtension(path))
        {
            return path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);
        }

        if (context.Request.Headers.TryGetValue("Accept", out var acceptHeader))
        {
            return acceptHeader.Any(v => v is not null && v.Contains("text/html", StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static void PersistMobileQueryFlagAsCookie(HttpContext context)
    {
        var queryFlag = PrismMobileRequestDetection.GetPrismMobileQueryFlag(context);
        if (!queryFlag.HasValue)
        {
            return;
        }

        if (queryFlag.Value)
        {
            context.Response.Cookies.Append(
                PrismMobileRequestDetection.CookieName,
                "1",
                new CookieOptions
                {
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = context.Request.IsHttps,
                    Path = "/"
                });
            return;
        }

        context.Response.Cookies.Delete(
            PrismMobileRequestDetection.CookieName,
            new CookieOptions
            {
                Path = "/"
            });
    }

    private static bool ShouldInject(HttpContext context, string bodyText)
    {
        if (context.Response.StatusCode < StatusCodes.Status200OK || context.Response.StatusCode >= StatusCodes.Status300MultipleChoices)
        {
            return false;
        }

        var contentType = context.Response.ContentType;
        if (!string.IsNullOrWhiteSpace(contentType) && contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return bodyText.Contains("</head>", StringComparison.OrdinalIgnoreCase)
            || bodyText.Contains("</body>", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCssOverrides(
        IReadOnlyDictionary<string, string>? overrides,
        IReadOnlyDictionary<string, string>? mobileOverrides,
        string? overrideDeclarations,
        string? mobileOverrideDeclarations)
    {
        var hasOverrides = (overrides is { Count: > 0 }) || (mobileOverrides is { Count: > 0 });
        if (!hasOverrides)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(":root{");

        if (!string.IsNullOrWhiteSpace(overrideDeclarations))
        {
            builder.Append(overrideDeclarations);
        }
        else
        {
            AppendOverrides(builder, overrides);
        }

        if (!string.IsNullOrWhiteSpace(mobileOverrideDeclarations))
        {
            builder.Append(mobileOverrideDeclarations);
        }
        else
        {
            AppendOverrides(builder, mobileOverrides);
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendOverrides(StringBuilder builder, IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides == null || overrides.Count == 0)
        {
            return;
        }

        foreach (var (name, value) in overrides)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) continue;
            builder.Append(name.Trim());
            builder.Append(':');
            builder.Append(value.Trim());
            builder.Append(';');
        }
    }

        private static string InjectBranding(string html, string css, bool includeMobileShellGuards, bool injectBiometricEnroll = false, string? tenantHost = null)
    {
                var injection = new StringBuilder();

                if (!string.IsNullOrWhiteSpace(css))
                {
                        injection.Append($"<style id=\"prism-branding-overrides\">{css}</style>");
                }

                if (includeMobileShellGuards)
                {
                        if (!html.Contains("viewport-fit=cover", StringComparison.OrdinalIgnoreCase))
                        {
                                injection.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0, viewport-fit=cover\" />");
                        }

                        injection.Append(BuildMobileShellStyleTag());
                        injection.Append(BuildMobileShellGuardScriptTag());
                }

                if (injectBiometricEnroll && !string.IsNullOrWhiteSpace(tenantHost))
                {
                    injection.Append(BuildBiometricEnrollScriptTag(tenantHost));
                }

                var injectionMarkup = injection.ToString();
                if (string.IsNullOrWhiteSpace(injectionMarkup))
                {
                        return html;
                }

        var headCloseIndex = html.LastIndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headCloseIndex >= 0)
        {
                        return html.Insert(headCloseIndex, injectionMarkup);
        }

        var bodyCloseIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyCloseIndex >= 0)
        {
                        return html.Insert(bodyCloseIndex, injectionMarkup);
        }

                return html + injectionMarkup;
        }

        private static string BuildMobileShellStyleTag()
        {
                return """
<style id="prism-mobile-shell-base">
html.prism-mobile,
html.prism-mobile body {
    width: 100%;
    max-width: 100%;
    min-height: 100%;
    margin: 0;
    overflow-x: hidden;
}

html.prism-mobile body {
    padding-top: env(safe-area-inset-top, 0px);
    padding-right: env(safe-area-inset-right, 0px);
    padding-bottom: env(safe-area-inset-bottom, 0px);
    padding-left: env(safe-area-inset-left, 0px);
}

html.prism-mobile .container {
    width: 100%;
    max-width: none;
}
</style>
""";
        }

        private static string BuildMobileShellGuardScriptTag()
        {
                return """
<script id="prism-mobile-shell-guard">
(function () {
    var root = document.documentElement;
    if (!root.classList.contains('prism-mobile')) {
        root.classList.add('prism-mobile');
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (!(target instanceof Element)) return;

        var anchor = target.closest('a');
        if (!anchor) return;

        var href = anchor.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('javascript:')) return;

        if (href.startsWith('mailto:') || href.startsWith('tel:')) {
            event.preventDefault();
            return;
        }

        var forceInWebView = anchor.target && anchor.target.toLowerCase() === '_blank';
        if (!forceInWebView) return;

        event.preventDefault();
        window.location.assign(anchor.href);
    }, true);

    window.open = function (url) {
        if (typeof url === 'string' && url.length > 0) {
            window.location.assign(url);
        }
        return null;
    };
})();
</script>
""";
    }

    private static string BuildBiometricEnrollScriptTag(string tenantHost)
    {
        var escapedHost = tenantHost.Replace("\\", "\\\\").Replace("'", "\\'");
        return $$"""
<script id="prism-biometric-enroll">
(function () {
  var TENANT_HOST = '{{escapedHost}}';
  var SS_PFX = 'capacitor-storage_';
  var TOKEN_KEY = SS_PFX + 'prism_biometric_token_' + TENANT_HOST;
  var ENROLL_KEY = 'prism_biometric_enrollment_state_' + TENANT_HOST;
  var DEV_ID_KEY = 'prism_device_id';

  var Cap = window.Capacitor;
  if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return;

  (async function () {
    try {
      var storedResult = await Cap.nativePromise('SecureStorage', 'internalGetItem', { prefixedKey: TOKEN_KEY, sync: false });
      var hasToken = storedResult && storedResult.data && storedResult.data !== 'null';
      if (hasToken) return;

      var info = await Cap.nativePromise('BiometricAuthNative', 'checkBiometry', {});
      if (!info || !info.isAvailable) return;

      showEnrollBanner();
    } catch (e) {
      // Silent fail
    }
  })();

  function showEnrollBanner() {
    if (document.getElementById('prism-bio-banner')) return;
    var banner = document.createElement('div');
    banner.id = 'prism-bio-banner';
    banner.style.cssText = 'position:fixed;bottom:0;left:0;right:0;z-index:99999;padding:16px 16px calc(16px + env(safe-area-inset-bottom,0px));background:#fff;border-top:1px solid #e5e7eb;box-shadow:0 -4px 16px rgba(0,0,0,.12);font-family:-apple-system,BlinkMacSystemFont,sans-serif;';
    banner.innerHTML = '<p style="margin:0 0 8px;font-size:1rem;font-weight:600;color:#111827;">Enable Face ID / Touch ID?</p>' +
      '<p style="margin:0 0 12px;font-size:.875rem;color:#6b7280;">Sign in faster next time without entering your password.</p>' +
      '<div style="display:flex;gap:8px;">' +
        '<button id="prism-bio-yes" style="flex:1;padding:12px;background:#2563eb;color:#fff;border:none;border-radius:8px;font-size:.875rem;font-weight:600;cursor:pointer;">Enable</button>' +
        '<button id="prism-bio-no" style="flex:1;padding:12px;background:#f3f4f6;color:#374151;border:none;border-radius:8px;font-size:.875rem;font-weight:600;cursor:pointer;">Not now</button>' +
      '</div>';
    document.body.appendChild(banner);
    document.getElementById('prism-bio-no').addEventListener('click', function () { banner.remove(); });
    document.getElementById('prism-bio-yes').addEventListener('click', handleEnroll);
  }

  async function handleEnroll() {
    var yesBtn = document.getElementById('prism-bio-yes');
    if (yesBtn) yesBtn.textContent = 'Setting up\u2026';
    try {
      await Cap.nativePromise('BiometricAuthNative', 'internalAuthenticate', {
        reason: 'Register biometric login',
        allowDeviceCredential: true,
        iosFallbackTitle: 'Use Passcode'
      });

      var devResult = await Cap.nativePromise('Preferences', 'get', { key: DEV_ID_KEY });
      var deviceId = devResult && devResult.value ? devResult.value : null;
      if (!deviceId) {
        var arr = new Uint8Array(16);
        crypto.getRandomValues(arr);
        arr[6] = (arr[6] & 0x0f) | 0x40;
        arr[8] = (arr[8] & 0x3f) | 0x80;
        var hex = Array.from(arr).map(function(b) { return b.toString(16).padStart(2,'0'); }).join('');
        deviceId = hex.slice(0,8)+'-'+hex.slice(8,12)+'-'+hex.slice(12,16)+'-'+hex.slice(16,20)+'-'+hex.slice(20);
        await Cap.nativePromise('Preferences', 'set', { key: DEV_ID_KEY, value: deviceId });
      }

      var resp = await fetch('https://' + TENANT_HOST + '/umbraco/prism/mobile/biometric/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ deviceId: deviceId, platform: Cap.getPlatform() })
      });

      if (!resp.ok) throw new Error('Register failed: ' + resp.status);
      var data = await resp.json();
      if (!data.biometricToken) throw new Error('No biometric token');

      await Cap.nativePromise('SecureStorage', 'internalSetItem', {
        prefixedKey: TOKEN_KEY,
        data: JSON.stringify(data.biometricToken),
        sync: false,
        access: 'whenUnlocked'
      });

      var biometryInfo = await Cap.nativePromise('BiometricAuthNative', 'checkBiometry', {});
      var fp = [biometryInfo.biometryType,(biometryInfo.biometryTypes||[]).slice().sort().join(','),biometryInfo.isAvailable,biometryInfo.strongBiometryIsAvailable,biometryInfo.deviceIsSecure].join('|');
      await Cap.nativePromise('Preferences', 'set', { key: ENROLL_KEY, value: fp });

      var banner = document.getElementById('prism-bio-banner');
      if (banner) {
        banner.innerHTML = '<p style="margin:0;font-size:.9rem;font-weight:600;color:#16a34a;text-align:center;padding:4px 0;">&#10003; Biometric login enabled</p>';
        setTimeout(function () { banner.remove(); }, 2000);
      }
    } catch (e) {
      var msg = e && (e.message || String(e));
      var banner = document.getElementById('prism-bio-banner');
      if (!banner) return;
      if (msg && (msg.toLowerCase().includes('cancel') || msg.toLowerCase().includes('usercancel'))) {
        banner.remove();
        return;
      }
      var yesBtn2 = document.getElementById('prism-bio-yes');
      if (yesBtn2) yesBtn2.textContent = 'Enable';
      var errEl = banner.querySelector('#prism-bio-err');
      if (!errEl) {
        errEl = document.createElement('p');
        errEl.id = 'prism-bio-err';
        errEl.style.cssText = 'margin:8px 0 0;color:#dc2626;font-size:.8rem;';
        banner.appendChild(errEl);
      }
      errEl.textContent = 'Setup failed. Please try again.';
    }
  }
})();
</script>
""";
    }
}
