// Prism mobile sign-out: clears biometric auto sign-in, and silently ends the IdP's own
// federated session too — without ever showing the IdP's own logout UI inside the app.
//
// Every Sign Out button in this codebase is a plain <button type="submit"> inside a
// <form action="/auth/logout">, never a link — this script intercepts that form's submit
// directly (capture phase, so it runs before anything else), catching all of them (home hero,
// dashboard, the debug panel) with one generic selector, no per-page wiring needed.
//
// On plain web, AccountController.Logout()'s SignOut() call is exactly right as a normal,
// visible, top-level redirect through the IdP's own end-session endpoint — that's the standard,
// expected way OIDC logout works, and it's the only way to fully end Entra's own SSO session.
// Skipping it there would leave that federated session alive with nothing telling the user so.
//
// Mobile's problem isn't that step existing — it's that the whole app runs inside ONE
// persistent WebView, so that same visible top-level redirect lands the user on Entra's own
// hosted logout page, complete with an account-chooser interstitial ("pick an account to sign
// out") that makes no sense on an app where there's only ever one signed-in account. And
// skipping that step entirely (signing out of PrismMemberCookie alone) isn't a safe fix either:
// Entra's own session cookie lives in the same WebView's cookie jar, so the very next Challenge()
// (hitting any [Authorize] route, or just tapping Sign In again) would silently SSO straight
// back in with no prompt at all — "signed out" that quietly un-signs-out itself.
//
// So this loads the exact same, already-correct sign-out URL (AccountController.Logout()'s own
// SignOut() call, unchanged — this script doesn't rebuild or duplicate that URL, it just changes
// WHERE it's requested from) into a hidden iframe instead of the top-level window. The HTTP
// redirect chain — Prism clears PrismMemberCookie, 302 to Entra's end-session endpoint (which
// clears Entra's own session cookie as a side effect of the request itself, regardless of
// whether the response ever gets *rendered*), 302 back to /signout-callback-oidc, 302 to / —
// runs and completes exactly as it does on web; only its visibility changes. Needs frame-src
// widened to the tenant's OIDC host(s) (see PrismSecurityHeadersMiddleware's own remarks) or the
// iframe navigation is blocked outright.
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
  // Generous but bounded: the redirect chain is a handful of fast server-to-server hops, not
  // anything a real network round-trip should ever approach — this is a safety net for a
  // load event that never fires (e.g. Entra's own page refusing to render inside a frame,
  // which doesn't stop the underlying request/redirect/cookie chain from completing, just its
  // own UI from painting) rather than a timing budget this is expected to need.
  var SILENT_IFRAME_TIMEOUT_MS = 4000;

  async function clearLocalBiometricState() {
    try {
      await Cap.nativePromise('SecureStorage', 'internalRemoveItem', { prefixedKey: TOKEN_KEY, sync: false });
    } catch (e) {
      // Best-effort — sign-out must proceed regardless of Keychain/Keystore availability.
    }
    localStorage.removeItem(ENROLL_KEY);
    localStorage.removeItem(DEV_ID_KEY);
    localStorage.removeItem(DECLINED_KEY);
  }

  async function revokeServerSideCredential() {
    try {
      await fetch('/umbraco/prism/mobile/biometric/revoke', { method: 'DELETE', credentials: 'include' });
    } catch (e) {
      // Best-effort — an unreachable server shouldn't block the user from signing out locally.
    }
  }

  function silentIdpSignOut(antiforgeryToken) {
    return new Promise(function (resolve) {
      var iframe = document.createElement('iframe');
      iframe.style.display = 'none';
      iframe.setAttribute('aria-hidden', 'true');
      var frameName = 'prism-silent-signout-' + Date.now();
      iframe.name = frameName;

      var settled = false;
      var timer = setTimeout(finish, SILENT_IFRAME_TIMEOUT_MS);
      function finish() {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        iframe.remove();
        resolve();
      }
      iframe.addEventListener('load', finish);
      iframe.addEventListener('error', finish);

      document.body.appendChild(iframe);

      var hiddenForm = document.createElement('form');
      hiddenForm.method = 'post';
      hiddenForm.action = '/auth/logout';
      hiddenForm.target = frameName;
      hiddenForm.style.display = 'none';

      var hiddenInput = document.createElement('input');
      hiddenInput.type = 'hidden';
      hiddenInput.name = '__RequestVerificationToken';
      hiddenInput.value = antiforgeryToken;
      hiddenForm.appendChild(hiddenInput);

      document.body.appendChild(hiddenForm);
      hiddenForm.submit();
      hiddenForm.remove();
    });
  }

  async function handleSignOut(form) {
    // Biometric cleanup needs no ordering relative to the session itself — do it alongside the
    // IdP round trip rather than serialized before it, so the user isn't waiting on two sequential
    // network round trips for something that can safely happen at the same time.
    var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
    var idpSignOut = tokenInput && tokenInput.value
      ? silentIdpSignOut(tokenInput.value)
      : Promise.resolve(); // No token to carry over — the local cleanup below still runs.

    await Promise.all([
      revokeServerSideCredential(),
      clearLocalBiometricState(),
      idpSignOut,
    ]);

    // PrismMemberCookie was already cleared as a side effect of the hidden iframe's own POST
    // (same origin, same cookie jar) well before this point — this is just the visible
    // navigation reflecting that, not what makes it true.
    window.location.href = '/';
  }

  document.addEventListener('submit', function (event) {
    var form = event.target;
    if (!(form instanceof HTMLFormElement) || form.getAttribute('action') !== '/auth/logout') return;

    event.preventDefault();
    handleSignOut(form);
  }, true);
})();
