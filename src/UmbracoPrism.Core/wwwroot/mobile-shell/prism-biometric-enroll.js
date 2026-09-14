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
  // "Not now" previously had no memory at all — its handler was just banner.remove(), so the
  // banner reappeared on every single page load where biometry is available and no token is
  // enrolled, found live: "every time you go back to the home page, it keeps asking". A snooze
  // rather than "never ask again" — the user might only be declining in the moment, not
  // permanently, and a fresh sign-in later is a reasonable point to re-offer.
  var DECLINED_KEY = 'prism_biometric_declined_at_' + TENANT_HOST;
  var DECLINE_SNOOZE_MS = 7 * 24 * 60 * 60 * 1000;

  __prismDebug.log('[Prism Enroll] enrollment script running for tenant: ' + TENANT_HOST);

  var Cap = window.Capacitor;
  if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) {
    __prismDebug.log('[Prism Enroll] not a native platform — enrollment script skipped');
    return;
  }

  // Clearing biometric credentials on logout is prism-biometric-signout.js's job, not this
  // file's — see that script's own remarks. (A click-listener used to live here, but it
  // matched a[href*="logout"]/button[data-action*="logout"], which no Sign Out button in this
  // codebase is: every one is a plain <button type="submit"> inside a <form action="/auth/
  // logout">, so the listener never actually fired — found live, via the exact bug it was
  // meant to prevent: biometric auto sign-in survived sign-out.)

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

      var declinedAt = Number(localStorage.getItem(DECLINED_KEY));
      if (declinedAt && (Date.now() - declinedAt) < DECLINE_SNOOZE_MS) {
        __prismDebug.log('[Prism Enroll] recently declined — skipping banner (snoozed)');
        return;
      }

      __prismDebug.log('[Prism Enroll] biometry available — showing enrollment banner');
      showEnrollBanner();
    } catch (e) {
      __prismDebug.log('[Prism Enroll] setup check threw: ' + (e && (e.message || String(e))));
    }
  })();

    // Styled from the tenant's own Prism branding custom properties (--prism-*, set in
    // prism-branding.css / a tenant's backoffice overrides) rather than hardcoded colors, so
    // this native-feeling prompt actually looks like part of the branded app it's enrolling
    // biometrics for, not a generic unstyled toast bolted on top of it — found live: hardcoded
    // #2563eb didn't match the tenant's actual --prism-primary blue at all.
    function showEnrollBanner() {
      if (document.getElementById('prism-bio-banner')) return;
      var banner = document.createElement('div');
      banner.id = 'prism-bio-banner';
      banner.style.cssText = 'position:fixed;bottom:0;left:0;right:0;z-index:99999;' +
        'padding:20px 20px calc(20px + env(safe-area-inset-bottom,0px));' +
        'background:var(--prism-surface,#fff);border-top:1px solid var(--prism-border,#e5e7eb);' +
        'border-radius:16px 16px 0 0;box-shadow:0 -4px 20px rgba(0,0,0,.15);' +
        'font-family:var(--prism-font-body,-apple-system,BlinkMacSystemFont,sans-serif);';
      // min-height:52px (not just padding) guarantees a proper touch target regardless of
      // font rendering — 13px padding + text alone measured visibly small/cramped live.
      // -webkit-appearance:none/appearance:none is load-bearing, not decorative: iOS WebKit
      // gives <button> native OS chrome by default, and that native chrome can silently override
      // CSS sizing (min-height included) with the platform's own intrinsic control height —
      // a well-documented WKWebView quirk. Found live: the diagnostic below measured these
      // buttons at 20px tall on a real device despite min-height:52px being confirmed served
      // and rendering correctly in an isolated (non-native-chrome) simulator repro — the
      // isolated repro never exercised real native button chrome, which only WebKit itself
      // applies, so it couldn't have caught this.
      var btnStyle = 'flex:1;min-height:52px;padding:14px 16px;border:none;border-radius:10px;' +
        'font-size:1.0625rem;font-weight:600;cursor:pointer;-webkit-appearance:none;appearance:none;';
      banner.innerHTML = '<p style="margin:0 0 6px;font-size:1.0625rem;font-weight:600;color:var(--prism-text,#111827);">Enable Face ID / Touch ID?</p>' +
        '<p style="margin:0 0 18px;font-size:.9rem;color:var(--prism-muted,#6b7280);">Sign in faster next time without entering your password.</p>' +
        '<div style="display:flex;gap:12px;">' +
          '<button id="prism-bio-yes" style="' + btnStyle + 'background:var(--prism-primary,#2563eb);color:var(--prism-primary-contrast,#fff);">Enable</button>' +
          '<button id="prism-bio-no" style="' + btnStyle + 'background:var(--prism-surface-alt,#f3f4f6);color:var(--prism-text,#374151);">Not now</button>' +
        '</div>';
      document.body.appendChild(banner);
      document.getElementById('prism-bio-no').addEventListener('click', function () {
        localStorage.setItem(DECLINED_KEY, String(Date.now()));
        banner.remove();
      });
      document.getElementById('prism-bio-yes').addEventListener('click', handleEnroll);

      // TEMPORARY diagnostic (2026-09-14): a real device reported these buttons looking tiny
      // even though this exact min-height:52px CSS is confirmed served (no-store header rules
      // out caching) and confirmed rendering at 52px in an isolated simulator repro (with and
      // without the WKWebView zoom-fix script's viewport override — identical either way).
      // Shows the ACTUAL measured size in the real page context, on the real device, so the
      // next report is a number, not a visual impression. Remove once resolved.
      setTimeout(function () {
        var rect = document.getElementById('prism-bio-yes').getBoundingClientRect();
        var diag = document.createElement('p');
        diag.id = 'prism-bio-diag';
        diag.style.cssText = 'margin:8px 0 0;font-size:.7rem;font-family:monospace;color:#dc2626;background:#fef2f2;padding:4px 6px;border-radius:4px;';
        diag.textContent = 'DIAG build=2026-09-14-c btn=' + rect.width.toFixed(0) + 'x' + rect.height.toFixed(0) +
          ' dPR=' + window.devicePixelRatio + ' vw=' + window.innerWidth;
        banner.appendChild(diag);
      }, 50);
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
          banner.innerHTML = '<p style="margin:0;font-size:.9rem;font-weight:600;color:var(--prism-accent,#16a34a);text-align:center;padding:4px 0;">&#10003; Biometric login enabled</p>';
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
          errEl.style.cssText = 'margin:8px 0 0;color:var(--prism-danger,#dc2626);font-size:.8rem;';
          banner.appendChild(errEl);
        }
        // The reason is shown inline (not just logged) so a real device failure is diagnosable
        // straight from a screenshot — without it, "Setup failed" alone (the previous copy)
        // gave no way to tell a plugin-not-installed error apart from a server/network one
        // without a Mac + cable + Safari's remote Web Inspector.
      errEl.textContent = 'Setup failed: ' + (msg || 'unknown error') + '. Please try again.';
    }
  }
})();
