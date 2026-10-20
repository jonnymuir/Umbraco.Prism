// Prism app sign-in, served by /auth/login and /auth/register to a generated app that signs in
// through the system authentication session. Biometric sign-in comes first where the member has
// enrolled (prism-biometric-autologin.js, loaded on demand). Otherwise the app's
// PrismSystemSignIn plugin opens the system sheet at /umbraco/prism/mobile/auth/start, which
// ends with a one-time code on the callback URL; the code is redeemed here with the PKCE
// verifier only this page knows. Once signed in, the page reloads: /auth/login then redirects a
// signed-in member to the page they were going to, replacing this page in the history.
(function () {
  var BASE = '/umbraco/prism/mobile/auth';
  var script = document.currentScript;
  var prompt = script.getAttribute('data-prism-prompt');
  var antiforgeryHeader = script.getAttribute('data-prism-antiforgery-header');
  var antiforgeryToken = script.getAttribute('data-prism-antiforgery-token');

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

  async function openSignInSheet(challenge) {
    var start = new URL(BASE + '/start', window.location.origin);
    start.searchParams.set('challenge', challenge);
    if (prompt) start.searchParams.set('prompt', prompt);

    var result = await window.Capacitor.nativePromise('PrismSystemSignIn', 'authenticate', {
      url: start.href,
      callbackHost: window.location.hostname,
      callbackPath: BASE + '/callback'
    });
    return new URL(result.url).searchParams.get('code') || '';
  }

  async function redeem(code, verifier) {
    var headers = {};
    headers[antiforgeryHeader] = antiforgeryToken;
    var response = await fetch(BASE + '/redeem', {
      method: 'POST',
      credentials: 'same-origin',
      headers: headers,
      body: new URLSearchParams({ code: code, verifier: verifier })
    });
    return response.ok;
  }

  async function signIn() {
    if (window.PrismBiometricSignIn && await window.PrismBiometricSignIn()) return true;

    var verifier = base64Url(crypto.getRandomValues(new Uint8Array(32)));
    var digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
    var code = await openSignInSheet(base64Url(new Uint8Array(digest)));
    return redeem(code, verifier);
  }

  signIn().then(
    function (signedIn) {
      if (signedIn) {
        window.location.reload();
      } else {
        leave();
      }
    },
    function (error) {
      console.log('[Prism SignIn] ' + (error && (error.message || String(error))));
      leave();
    });
})();
