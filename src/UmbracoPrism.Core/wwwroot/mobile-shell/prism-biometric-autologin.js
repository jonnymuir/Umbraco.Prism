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
  var LOG = '[Prism AutoLogin] ';
  var HOST = window.location.host;
  var TOKEN_KEY = 'capacitor-storage_prism_biometric_token_' + HOST;
  var ENROLL_KEY = 'prism_biometric_enrollment_state_' + HOST;

  function isNativeApp() {
    var Cap = window.Capacitor;
    return !!(Cap && Cap.isNativePlatform && Cap.isNativePlatform());
  }

  function native(plugin, method, options) {
    return window.Capacitor.nativePromise(plugin, method, options || {});
  }

  async function forgetCredential() {
    await native('SecureStorage', 'internalRemoveItem', { prefixedKey: TOKEN_KEY, sync: false });
    localStorage.removeItem(ENROLL_KEY);
  }

  async function readStoredToken() {
    var stored = await native('SecureStorage', 'internalGetItem', { prefixedKey: TOKEN_KEY, sync: false });
    return stored && stored.data ? JSON.parse(stored.data) : null;
  }

  // What the device's biometrics looked like at enrollment; a change (a new face or finger
  // enrolled on the device) means the stored credential must not be trusted.
  function biometryFingerprint(info) {
    return [
      info.biometryType,
      (info.biometryTypes || []).slice().sort().join(','),
      info.isAvailable,
      info.strongBiometryIsAvailable,
      info.deviceIsSecure
    ].join('|');
  }

  // The device fingerprint when the stored credential is still usable, or null after clearing
  // it: Keychain survives an app reinstall but localStorage does not, so a token with no
  // enrollment fingerprint is from a previous install.
  async function usableCredentialFingerprint() {
    if (!localStorage.getItem(ENROLL_KEY)) {
      console.log(LOG + 'stale token from previous install, clearing');
      await forgetCredential();
      return null;
    }
    var info = await native('BiometricAuthNative', 'checkBiometry');
    if (!info || !info.isAvailable) {
      console.log(LOG + 'biometry not available, skipping auto-login');
      return null;
    }
    var fingerprint = biometryFingerprint(info);
    if (localStorage.getItem(ENROLL_KEY) !== fingerprint) {
      console.log(LOG + 'biometric state changed, clearing credentials');
      await forgetCredential();
      return null;
    }
    return fingerprint;
  }

  async function exchange(token, fingerprint) {
    var response = await fetch('/umbraco/prism/mobile/biometric/exchange', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ biometricToken: token, deviceId: localStorage.getItem('prism_device_id') || '' })
    });
    if (response.ok) {
      localStorage.setItem(ENROLL_KEY, fingerprint);
      console.log(LOG + 'exchange successful');
      return true;
    }
    if (response.status === 401 || response.status === 403) {
      console.log(LOG + 'server rejected token, clearing credentials');
      await forgetCredential();
    } else {
      console.warn(LOG + 'exchange failed with status ' + response.status);
    }
    return false;
  }

  // Signs in with the device's stored biometric credential. True when the member is now signed
  // in; the caller decides where to go next.
  async function signInWithBiometrics() {
    if (!isNativeApp()) return false;
    try {
      // This only runs while signed out, so the server session is gone and any earlier push
      // registration can't be trusted (a redeploy may have wiped the server's device rows).
      // Forgetting the local note makes prism-push-register.js register again after sign-in.
      localStorage.removeItem('prism_push_registered_token_' + HOST);

      var token = await readStoredToken();
      if (!token) {
        console.log(LOG + 'no stored token, showing login page normally');
        return false;
      }
      var fingerprint = await usableCredentialFingerprint();
      if (!fingerprint) return false;

      await native('BiometricAuthNative', 'internalAuthenticate', {
        reason: 'Sign in with biometrics',
        allowDeviceCredential: true,
        iosFallbackTitle: 'Use Passcode'
      });
      return await exchange(token, fingerprint);
    } catch (e) {
      var cancelled = String(e).toLowerCase().includes('cancel');
      console.log(LOG + (cancelled ? 'user cancelled biometric' : 'error: ' + (e && (e.message || String(e)))));
      return false;
    }
  }

  window.PrismBiometricSignIn = signInWithBiometrics;

  var script = document.currentScript;
  if (!(script && script.hasAttribute('data-prism-on-demand'))) {
    signInWithBiometrics().then(function (signedIn) {
      if (signedIn) window.location.reload();
    });
  }
})();
