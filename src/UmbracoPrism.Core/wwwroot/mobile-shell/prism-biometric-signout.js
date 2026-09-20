// Prism mobile sign-out: clears biometric auto sign-in alongside the IdP's own federated
// sign-out, without blocking it.
//
// Every Sign Out button in this codebase is a plain <button type="submit"> inside a
// <form action="/auth/logout">, never a link — this script listens for that form's submit
// (capture phase, so it runs before anything else), catching all of them (home hero, dashboard,
// the debug panel) with one generic selector, no per-page wiring needed.
//
// History: a prior version of this script POSTed into a hidden iframe instead of letting the
// form submit normally, trying to end Entra's session without ever showing its logout UI.
// Reported live: this opened a real system Safari window instead of staying silent. Root cause,
// confirmed via Microsoft's own documentation (not a guess): Entra ID unconditionally refuses to
// render ANY interactive prompt — login, consent, logout, all of it — inside an iframe
// (X-Frame-Options: DENY, by design, for clickjacking protection), and this isn't something a
// tenant or client can opt out of. A fetch()-based ping was considered as an alternative (a
// network request isn't framing, so X-Frame-Options wouldn't apply to it) but is independently
// unreliable for the same reason this whole file already exists to route around: WKWebView
// specifically drops Set-Cookie from cross-origin fetch/XHR responses under ITP, so a "silent"
// ping would likely just silently FAIL to clear Entra's session — worse than the visible popup,
// since the user would believe they'd signed out of the federated session when they hadn't
// (the exact "signed out that quietly un-signs-out itself" risk this file's very first version
// already worried about, now confirmed as a real WKWebView-specific limitation, not hypothetical).
//
// There is no reliable silent path here. So: let the form submit exactly as AccountController
// .Logout()'s SignOut() call already sends it — a normal, visible, top-level redirect through
// Entra's own end-session endpoint, same as the always-correct plain-web behaviour — this script
// no longer intercepts or reroutes that navigation at all. What it still does: fire the
// biometric-credential cleanup alongside it, best-effort, without ever blocking or delaying the
// navigation on that cleanup completing.
//
// clearIdentityProviderCookies() below is a different mechanism again, added for a gap the
// above doesn't cover: AccountController.Logout() skips the federated redirect entirely for a
// biometric-tagged session (no Entra session in this WebView for it to end — see its own
// remarks), which is correct, but it means Entra's own SSO cookie from whenever this device
// first did an interactive sign-in never gets cleared on any later biometric sign-out — reported
// live as "signed out of the app but still silently signed in to Entra underneath". Neither
// prior silent attempt above (iframe, fetch ping) can fix this either, and for the same reason:
// both ask Entra's own server to do the clearing, over a channel Entra/WKWebView blocks. Native
// cookie-store deletion asks Entra nothing — it deletes whatever's already sitting in this app's
// own WebView cookie jar, so neither X-Frame-Options nor ITP applies. Fired unconditionally
// alongside the rest, not just for biometric sessions — harmless no-op when there's nothing to
// clear (an ordinary session's own federated redirect, above, already clears this cookie as a
// side effect of the request itself).
(function () {
  var Cap = window.Capacitor;
  if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return;

  var TENANT_HOST = window.location.host;
  var SS_PFX = 'capacitor-storage_';
  var TOKEN_KEY = SS_PFX + 'prism_biometric_token_' + TENANT_HOST;
  var ENROLL_KEY = 'prism_biometric_enrollment_state_' + TENANT_HOST;
  var DEV_ID_KEY = 'prism_device_id';
  // Same key prism-biometric-enroll.js snoozes "Not now" under. Cleared on sign-out (not just
  // left to the 7-day snooze) so a decline doesn't outlive the session it was made in — found
  // live: the snooze alone meant the same decline kept suppressing the banner across a sign-out/
  // sign-in as a different-feeling "session", which read as the app permanently ignoring the
  // user's choice rather than a bounded 7-day snooze.
  var DECLINED_KEY = 'prism_biometric_declined_at_' + TENANT_HOST;
  var PUSH_TOKEN_KEY = 'prism_push_registered_token_' + TENANT_HOST;

  function clearLocalBiometricState() {
    // Best-effort and deliberately not awaited by the caller — the page is about to navigate
    // away for real (Entra's own end-session redirect chain), which can tear down pending JS
    // work at any point. The native SecureStorage call is dispatched to native code immediately
    // on the call below, independent of whether this Promise chain survives to resolve.
    Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: TOKEN_KEY, sync: false })
      .catch(function () {});
    localStorage.removeItem(ENROLL_KEY);
    localStorage.removeItem(DEV_ID_KEY);
    localStorage.removeItem(DECLINED_KEY);
  }

  function revokeServerSideCredential() {
    // Best-effort, not awaited — an unreachable server, or the navigation below cutting this
    // request off mid-flight, shouldn't block or delay sign-out.
    fetch('/umbraco/prism/mobile/biometric/revoke', { method: 'DELETE', credentials: 'include' })
      .catch(function () {});
  }

  function clearIdentityProviderCookies() {
    // Read off this script's own tag (Master.cshtml renders it server-side, per-tenant — see
    // that view's own remarks) rather than a global, so this file stays self-contained.
    var scriptEl = document.currentScript ||
      document.querySelector('script[src*="prism-biometric-signout.js"]');
    var hostsAttr = (scriptEl && scriptEl.getAttribute('data-prism-idp-hosts')) || '';
    var hosts = hostsAttr.split(',').map(function (h) { return h.trim(); }).filter(Boolean);

    // Best-effort, not awaited — same reasoning as every other call in this file. The Android
    // side of this plugin ignores `hosts` entirely and clears the WebView's whole cookie jar
    // (there's no per-host removal API on android.webkit.CookieManager, and nothing else in
    // this single-purpose WebView needs a cookie preserved across sign-out); iOS uses it to
    // target exactly the record(s) that belong to this tenant's own IdP host(s).
    Cap.nativePromise('PrismIdentityCookiePlugin', 'clearCookies', { hosts: hosts }).catch(function () {});
  }

  function revokeServerSidePushToken() {
    // Same reasoning as revokeServerSideCredential() above — a stale token left registered past
    // sign-out would keep sending this device notifications addressed to whichever member signs
    // in next, until it naturally expires or is overwritten. No-op on a build without push
    // notifications wired in: PUSH_TOKEN_KEY is simply never set, so this DELETE just clears a
    // token that was never registered, harmless either way.
    localStorage.removeItem(PUSH_TOKEN_KEY);
    fetch('/umbraco/prism/push/register', { method: 'DELETE', credentials: 'include' })
      .catch(function () {});
  }

  document.addEventListener('submit', function (event) {
    var form = event.target;
    if (!(form instanceof HTMLFormElement) || form.getAttribute('action') !== '/auth/logout') return;

    // Not preventDefault()'d — the form's own normal submission is exactly the right, correct
    // top-level navigation. This just fires alongside it.
    clearLocalBiometricState();
    revokeServerSideCredential();
    revokeServerSidePushToken();
    clearIdentityProviderCookies();
  }, true);
})();
