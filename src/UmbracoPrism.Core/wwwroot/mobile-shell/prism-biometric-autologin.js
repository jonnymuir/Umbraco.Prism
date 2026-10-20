// Prism biometric auto-login.
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
//   <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-biometric-autologin.js"></script>
// only when a mobile request is detected AND the current user is NOT authenticated — see
// docs/walkthroughs/building-a-mobile-app.md.
//
// It signs in as soon as it loads, unless its tag carries data-prism-on-demand: the app's own
// sign-in page (prism-system-sign-in.js) calls window.PrismBiometricSignIn() itself, before
// falling back to the system sign-in sheet.
(function () {
async function signInWithBiometrics() {
  try {
    var Cap = window.Capacitor;
    if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return false;

    var TENANT_HOST = window.location.host;

    // This script only runs on the unauthenticated login page, so the server session is gone
    // and any earlier push registration can't be trusted (e.g. a redeploy wiped the server's
    // device rows). Forget the local "already registered" note so prism-push-register.js
    // registers again on the next authenticated page load.
    localStorage.removeItem('prism_push_registered_token_' + TENANT_HOST);
    var SS_PREFIX = 'capacitor-storage_';
    var tokenKey = SS_PREFIX + 'prism_biometric_token_' + TENANT_HOST;
    var enrollKey = 'prism_biometric_enrollment_state_' + TENANT_HOST;

    console.log('[Prism AutoLogin] checking for stored biometric token');

    // 1. Check SecureStorage for a stored biometric token
    var storedResult = await Cap.nativePromise('SecureStorage', 'internalGetItem', {
      prefixedKey: tokenKey,
      sync: false
    });
    var storedToken = storedResult && storedResult.data ? JSON.parse(storedResult.data) : null;
    if (!storedToken) {
      console.log('[Prism AutoLogin] no stored token — showing login page normally');
      return false;
    }
    console.log('[Prism AutoLogin] stored token found — checking biometry');

    // Fresh install: Keychain persists across app deletion but localStorage is wiped.
    // A token with no enrollment fingerprint means the app was reinstalled — clear the
    // stale Keychain token so the user can go through enrollment again.
    if (!localStorage.getItem(enrollKey)) {
      console.log('[Prism AutoLogin] stale token from previous install — clearing');
      await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: tokenKey, sync: false });
      return false;
    }

    // 2. Verify biometry is available
    var biometryInfo = await Cap.nativePromise('BiometricAuthNative', 'checkBiometry', {});
    if (!biometryInfo || !biometryInfo.isAvailable) {
      console.log('[Prism AutoLogin] biometry not available — skipping auto-login');
      return false;
    }

    // 3. Check if enrollment fingerprint has changed (biometric data updated on device)
    var fingerprint = [
      biometryInfo.biometryType,
      (biometryInfo.biometryTypes || []).slice().sort().join(','),
      biometryInfo.isAvailable,
      biometryInfo.strongBiometryIsAvailable,
      biometryInfo.deviceIsSecure
    ].join('|');
    var storedFingerprint = localStorage.getItem(enrollKey);
    if (storedFingerprint && storedFingerprint !== fingerprint) {
      console.log('[Prism AutoLogin] biometric state changed — clearing credentials');
      await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: tokenKey, sync: false });
      localStorage.removeItem(enrollKey);
      return false;
    }

    // 4. Prompt biometric authentication
    console.log('[Prism AutoLogin] prompting biometric authentication');
    await Cap.nativePromise('BiometricAuthNative', 'internalAuthenticate', {
      reason: 'Sign in with biometrics',
      allowDeviceCredential: true,
      iosFallbackTitle: 'Use Passcode'
    });
    console.log('[Prism AutoLogin] biometric passed — exchanging token');

    // 5. Exchange biometric token for a session cookie
    var deviceId = localStorage.getItem('prism_device_id') || '';
    var resp = await fetch('/umbraco/prism/mobile/biometric/exchange', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ biometricToken: storedToken, deviceId: deviceId })
    });

    if (resp.ok) {
      // Signed in: the caller decides where to go next.
      localStorage.setItem(enrollKey, fingerprint);
      console.log('[Prism AutoLogin] exchange successful');
      return true;
    } else if (resp.status === 401 || resp.status === 403) {
      console.log('[Prism AutoLogin] server rejected token — clearing credentials');
      await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: tokenKey, sync: false });
      localStorage.removeItem(enrollKey);
    } else {
      console.warn('[Prism AutoLogin] exchange failed with status ' + resp.status);
    }
  } catch (e) {
    if (e && (String(e).toLowerCase().includes('cancel') || String(e).toLowerCase().includes('usercancel'))) {
      console.log('[Prism AutoLogin] user cancelled biometric — showing login page');
    } else {
      console.warn('[Prism AutoLogin] error: ' + (e && (e.message || String(e))));
    }
  }
  return false;
}

window.PrismBiometricSignIn = signInWithBiometrics;

var script = document.currentScript;
if (!(script && script.hasAttribute('data-prism-on-demand'))) {
  signInWithBiometrics().then(function (signedIn) {
    if (signedIn) window.location.reload();
  });
}
})();
