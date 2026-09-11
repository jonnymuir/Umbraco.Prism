// Prism biometric enrollment prompt.
// Previously spliced inline into every response by PrismBrandingMiddleware, with the tenant
// hostname interpolated server-side into the script body (`{{escapedHost}}`). That
// interpolation was never actually load-bearing: by the time this script runs, the Capacitor
// WebView has already navigated to the tenant's real HTTPS origin (see
// UmbracoPrism.Core/Services/MobileBundleService.cs's generated www/index.html bootstrap
// shell — it does `window.location.replace(mobileStartUrl)` before any page carrying this
// script is ever reached), so `window.location.host` is exactly the tenant host, and API
// calls can use plain relative URLs instead of a server-baked absolute one. That let this
// become a plain static asset (SEC-PT2-004 CSP follow-up — no server involvement, no
// `unsafe-inline`/nonce needed). Hosts wire this in explicitly, e.g.:
//   <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-biometric-enroll.js"></script>
// only when a mobile request is detected AND the current user is authenticated — see
// docs/walkthroughs/building-a-mobile-app.md.
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
(function () {
  __prismDebug.replay();
  var TENANT_HOST = window.location.host;
  var SS_PFX = 'capacitor-storage_';
  var TOKEN_KEY = SS_PFX + 'prism_biometric_token_' + TENANT_HOST;
  var ENROLL_KEY = 'prism_biometric_enrollment_state_' + TENANT_HOST;
  var DEV_ID_KEY = 'prism_device_id';

  __prismDebug.log('[Prism Enroll] enrollment script running for tenant: ' + TENANT_HOST);

  var Cap = window.Capacitor;
  if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) {
    __prismDebug.log('[Prism Enroll] not a native platform — enrollment script skipped');
    return;
  }

  // Hook logout: intercept any logout/signout navigation to clear biometric credentials
  // and revoke the server-side credential record before the session ends.
  document.addEventListener('click', async function(e) {
    var a = e.target.closest('a[href*="logout" i], a[href*="signout" i], button[data-action*="logout" i]');
    if (!a) return;
    try {
      await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: TOKEN_KEY, sync: false });
      localStorage.removeItem(ENROLL_KEY);
      localStorage.removeItem(DEV_ID_KEY);
      await fetch('/umbraco/prism/mobile/biometric/revoke', { method: 'DELETE', credentials: 'include' });
      __prismDebug.log('[Prism Enroll] biometric credentials cleared on logout');
    } catch(err) { /* best-effort */ }
  }, true);

  (async function () {
    try {
      var storedResult = await Cap.nativePromise('SecureStorage', 'internalGetItem', { prefixedKey: TOKEN_KEY, sync: false });
      var hasToken = storedResult && storedResult.data && storedResult.data !== 'null';
      __prismDebug.log('[Prism Enroll] existing token check: ' + (hasToken ? 'token found' : 'no token found — will check biometry'));
      if (hasToken) {
        if (localStorage.getItem(ENROLL_KEY)) {
          __prismDebug.log('[Prism Enroll] existing token check: already enrolled — skipping banner');
          return;
        }
        // Stale token: Keychain has a token but localStorage fingerprint is gone (fresh install/wipe).
        // Clear the stale Keychain entry and fall through to show the enrollment banner.
        __prismDebug.log('[Prism Enroll] stale token from previous install — clearing and re-enrolling');
        await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: TOKEN_KEY, sync: false });
      }

      var info = await Cap.nativePromise('BiometricAuthNative', 'checkBiometry', {});
      __prismDebug.log('[Prism Enroll] biometry check result: ' + JSON.stringify(info));
      if (!info || !info.isAvailable) {
        __prismDebug.log('[Prism Enroll] biometry not available — skipping banner');
        return;
      }

      __prismDebug.log('[Prism Enroll] biometry available — showing enrollment banner');
      showEnrollBanner();
    } catch (e) {
      __prismDebug.log('[Prism Enroll] setup check threw: ' + (e && (e.message || String(e))));
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
    __prismDebug.log('[Prism Enroll] user tapped Enable — starting enrollment');
    var yesBtn = document.getElementById('prism-bio-yes');
    if (yesBtn) yesBtn.textContent = 'Setting up…';
      try {
        __prismDebug.log('[Prism Enroll] step 1: calling internalAuthenticate...');
        await Cap.nativePromise('BiometricAuthNative', 'internalAuthenticate', {
          reason: 'Register biometric login',
          allowDeviceCredential: true,
          iosFallbackTitle: 'Use Passcode'
        });
        __prismDebug.log('[Prism Enroll] step 1 done: internalAuthenticate succeeded');

        var deviceId = localStorage.getItem(DEV_ID_KEY);
        __prismDebug.log('[Prism Enroll] step 2 done: got device ID: ' + (deviceId ? 'found' : 'not found'));
        if (!deviceId) {
          var arr = new Uint8Array(16);
          crypto.getRandomValues(arr);
          arr[6] = (arr[6] & 0x0f) | 0x40;
          arr[8] = (arr[8] & 0x3f) | 0x80;
          var hex = Array.from(arr).map(function(b) { return b.toString(16).padStart(2,'0'); }).join('');
          deviceId = hex.slice(0,8)+'-'+hex.slice(8,12)+'-'+hex.slice(12,16)+'-'+hex.slice(16,20)+'-'+hex.slice(20);
          localStorage.setItem(DEV_ID_KEY, deviceId);
          __prismDebug.log('[Prism Enroll] step 2b done: device ID stored');
        }

        __prismDebug.log('[Prism Enroll] step 3: POSTing to register endpoint...');
        var resp = await fetch('/umbraco/prism/mobile/biometric/register', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({ deviceId: deviceId, platform: Cap.getPlatform() })
        });
        __prismDebug.log('[Prism Enroll] step 3 done: register response status ' + resp.status);

        if (!resp.ok) throw new Error('Register failed: ' + resp.status);
        var data = await resp.json();
        if (!data.biometricToken) throw new Error('No biometric token');

        await Cap.nativePromise('SecureStorage', 'internalSetItem', {
          prefixedKey: TOKEN_KEY,
          data: JSON.stringify(data.biometricToken),
          sync: false,
          access: 'whenUnlocked'
        });
        __prismDebug.log('[Prism Enroll] step 4 done: token stored in secure storage');

        var biometryInfo = await Cap.nativePromise('BiometricAuthNative', 'checkBiometry', {});
        var fp = [biometryInfo.biometryType,(biometryInfo.biometryTypes||[]).slice().sort().join(','),biometryInfo.isAvailable,biometryInfo.strongBiometryIsAvailable,biometryInfo.deviceIsSecure].join('|');
        localStorage.setItem(ENROLL_KEY, fp);

        var banner = document.getElementById('prism-bio-banner');
        if (banner) {
          banner.innerHTML = '<p style="margin:0;font-size:.9rem;font-weight:600;color:#16a34a;text-align:center;padding:4px 0;">&#10003; Biometric login enabled</p>';
          setTimeout(function () { banner.remove(); }, 2000);
        }
      } catch (e) {
        __prismDebug.log('[Prism Enroll] enrollment error: ' + (e && (e.message || String(e))));
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
