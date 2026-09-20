// Prism push notification device registration.
// Wired in explicitly, only when a mobile request is detected AND the current user is
// authenticated — same gating as prism-biometric-enroll.js, see Master.cshtml — via:
//   <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-push-register.js"></script>
// Only runs when the bundle was generated with --push-notifications true (MobileBundleService.cs
// wires @capacitor-firebase/messaging into the native project in that case); on any other build
// window.Capacitor exposes no "FirebaseMessaging" plugin, so the nativePromise calls below just
// throw and are caught, a harmless no-op.
//
// @capacitor-firebase/messaging, not @capacitor/push-notifications — PrismNotificationService
// sends via FirebaseAdmin.Messaging (FCM-only). @capacitor/push-notifications alone would hand
// iOS a raw APNs token FCM can't target; this plugin bridges the APNs token it receives through
// Firebase's own SDK into an FCM token (see the AppDelegate.swift patch in
// MobileBundleService.BuildBootstrapIosScript), so getToken() below already returns an
// FCM-compatible token on both platforms.
(function () {
  var Cap = window.Capacitor;
  if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return;

  var TENANT_HOST = window.location.host;
  // Not a secret, not re-sent unless it changes — just this device's fingerprint of "the token
  // we last successfully registered with the server", so a re-run on every authenticated page
  // load (cheap, idempotent, same pattern prism-biometric-enroll.js's own token-presence check
  // already uses) doesn't re-POST identical state every time.
  var REGISTERED_TOKEN_KEY = 'prism_push_registered_token_' + TENANT_HOST;

  // Routes a tapped notification to whatever page PrismNotificationService's caller asked for
  // (MulticastMessage.Data["url"], see PrismSendPushNotificationAction's optional DeepLinkPath)
  // instead of just leaving the WebView on whatever page it happened to be on when backgrounded.
  // Same Cap.Plugins.<Plugin>.addListener bridge wayfinder-poll.js's own onForeground() uses for
  // @capacitor/app's 'resume' event — the one already proven to work in this exact stack.
  if (Cap.Plugins && Cap.Plugins.FirebaseMessaging && Cap.Plugins.FirebaseMessaging.addListener) {
    Cap.Plugins.FirebaseMessaging.addListener('notificationActionPerformed', function (event) {
      try {
        var url = event && event.notification && event.notification.data && event.notification.data.url;
        if (url) {
          window.location.href = url;
        }
      } catch (e) {
        console.log('[Prism Push] notification tap handling threw: ' + (e && (e.message || String(e))));
      }
    });
  }

  (async function () {
    try {
      var permission = await Cap.nativePromise('FirebaseMessaging', 'requestPermissions', {});
      if (!permission || permission.receive !== 'granted') {
        console.log('[Prism Push] permission not granted (' + (permission && permission.receive) + ') — skipping registration');
        return;
      }

      var tokenResult = await Cap.nativePromise('FirebaseMessaging', 'getToken', {});
      var token = tokenResult && tokenResult.token;
      if (!token) {
        console.log('[Prism Push] getToken returned no token — skipping registration');
        return;
      }

      if (localStorage.getItem(REGISTERED_TOKEN_KEY) === token) {
        return;
      }

      var resp = await fetch('/umbraco/prism/push/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ pushToken: token })
      });

      if (!resp.ok) {
        console.log('[Prism Push] register failed: ' + resp.status);
        return;
      }

      localStorage.setItem(REGISTERED_TOKEN_KEY, token);
      console.log('[Prism Push] device token registered');
    } catch (e) {
      console.log('[Prism Push] registration threw: ' + (e && (e.message || String(e))));
    }
  })();
})();
