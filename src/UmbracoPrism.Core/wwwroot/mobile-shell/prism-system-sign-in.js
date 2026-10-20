// Prism app sign-in, served by /auth/login and /auth/register to a generated app that signs in
// through the system authentication session. Biometric sign-in comes first where the member has
// enrolled (prism-biometric-autologin.js, loaded on demand). Otherwise the app's
// PrismSystemSignIn plugin opens the system sheet at /umbraco/prism/mobile/auth/start, which
// ends with a one-time code on the callback URL; the code is redeemed here with the PKCE
// verifier only this page knows. The page then replaces itself with the member's page, so Back
// never returns to it.
(function () {
  var BASE = '/umbraco/prism/mobile/auth';
  var script = document.currentScript;
  var returnUrl = (script && script.getAttribute('data-prism-return-url')) || '/';
  var prompt = script && script.getAttribute('data-prism-prompt');

  function base64Url(bytes) {
    var text = '';
    for (var i = 0; i < bytes.length; i++) text += String.fromCharCode(bytes[i]);
    return btoa(text).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }

  // Cancelled or failed: back to where the member was.
  function leave() {
    if (window.history.length > 1) {
      window.history.back();
    } else {
      window.location.replace('/');
    }
  }

  async function signIn() {
    if (window.PrismBiometricSignIn && await window.PrismBiometricSignIn()) {
      window.location.replace(returnUrl);
      return;
    }

    var verifier = base64Url(crypto.getRandomValues(new Uint8Array(32)));
    var digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
    var start = new URL(BASE + '/start', window.location.origin);
    start.searchParams.set('challenge', base64Url(new Uint8Array(digest)));
    start.searchParams.set('returnUrl', returnUrl);
    if (prompt) start.searchParams.set('prompt', prompt);

    var result = await window.Capacitor.nativePromise('PrismSystemSignIn', 'authenticate', {
      url: start.href,
      callbackHost: window.location.hostname,
      callbackPath: BASE + '/callback'
    });

    var code = new URL(result.url).searchParams.get('code');
    var response = await fetch(BASE + '/redeem', {
      method: 'POST',
      credentials: 'same-origin',
      body: new URLSearchParams({ code: code || '', verifier: verifier })
    });

    // Redeem redirects to the member's page, or to the home page when the code is refused.
    window.location.replace(response.redirected ? response.url : '/');
  }

  signIn().catch(function (error) {
    console.log('[Prism SignIn] ' + (error && (error.message || String(error))));
    leave();
  });
})();
