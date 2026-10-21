/*
 * Prism saved pages, the page side: registers prism-offline-sw.js in an app built with saved
 * pages, swaps in a fresher copy of the page at a quiet moment, and says so when the app has no
 * connection. In an app built without saved pages it removes any worker and saved pages left
 * from an earlier build. See Design/mobile-offline-pages.md.
 *
 * A host includes it on every page rendered for a native-tab app:
 *   <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-offline.js"
 *           data-prism-saved-pages="on|off" data-prism-signed-in="true|false"></script>
 */
(function () {
  'use strict';

  var WORKER = '/prism-offline-sw.js';
  var OFFLINE_TEXT = 'You’re offline. Some information may be out of date.';

  var script = document.currentScript;
  if (!script || !('serviceWorker' in navigator)) return;

  if (script.getAttribute('data-prism-saved-pages') !== 'on') {
    forgetSavedPages();
    return;
  }

  var signedIn = script.getAttribute('data-prism-signed-in') === 'true';

  // A fresher copy replaces the page only before the member starts using it, or when they come
  // back to it after leaving; never once they have typed something.
  var touched = false;
  var typed = false;
  var freshCopyWaiting = false;
  var shownWhileOffline = false;
  var notice = null;
  var spacer = null;

  // Styled from here rather than a stylesheet, which may not be on the device when offline. The
  // webview ends above the app's tab bar on both platforms, so the bottom edge is clear of the
  // site's own header.
  var NOTICE_STYLE = 'position:fixed;right:0;bottom:0;left:0;z-index:2147483647;box-sizing:border-box;'
    + 'margin:0;padding:12px 16px calc(12px + env(safe-area-inset-bottom, 0px));'
    + 'background:#0b0c0c;color:#ffffff;font-size:16px;line-height:1.25;text-align:center;';

  ['pointerdown', 'touchstart', 'keydown', 'wheel'].forEach(function (name) {
    window.addEventListener(name, function () { touched = true; }, { capture: true, passive: true });
  });
  ['input', 'change'].forEach(function (name) {
    window.addEventListener(name, function () { typed = true; }, { capture: true, passive: true });
  });

  navigator.serviceWorker.addEventListener('message', function (event) {
    var type = event.data && event.data.type;
    if (type === 'prism-fresh-copy') showFreshCopy();
    if (type === 'prism-offline') {
      shownWhileOffline = true;
      showOffline();
    }
  });

  window.addEventListener('offline', showOffline);
  window.addEventListener('online', function () {
    hideOffline();
    if (shownWhileOffline) showFreshCopy();
  });
  document.addEventListener('visibilitychange', function () {
    if (document.visibilityState === 'visible' && freshCopyWaiting && !typed) location.reload();
  });

  if (navigator.onLine === false) showOffline();

  navigator.serviceWorker.register(WORKER, { scope: '/' })
    .then(function () { return navigator.serviceWorker.ready; })
    .then(function (registration) {
      var worker = navigator.serviceWorker.controller || registration.active;
      if (worker) worker.postMessage({ type: 'prism-hello', signedIn: signedIn, userAgent: navigator.userAgent });
    })
    .catch(function (error) {
      console.warn('[Prism saved pages] worker not registered', error);
    });

  function showFreshCopy() {
    if (!touched) {
      location.reload();
      return;
    }
    freshCopyWaiting = true;
  }

  function showOffline() {
    if (notice) return;
    if (!document.body) {
      document.addEventListener('DOMContentLoaded', showOffline, { once: true });
      return;
    }

    // Keeps the end of the page scrollable above the notice.
    spacer = document.createElement('div');
    spacer.style.height = '72px';
    spacer.setAttribute('aria-hidden', 'true');
    document.body.appendChild(spacer);

    // Added empty, then filled, so screen readers announce it once as a status message.
    notice = document.createElement('div');
    notice.style.cssText = NOTICE_STYLE;
    notice.setAttribute('role', 'status');
    notice.setAttribute('data-prism-offline-notice', '');
    document.body.appendChild(notice);
    setTimeout(function () { if (notice) notice.textContent = OFFLINE_TEXT; }, 100);
  }

  function hideOffline() {
    if (!notice) return;
    notice.remove();
    spacer.remove();
    notice = null;
    spacer = null;
  }

  function forgetSavedPages() {
    navigator.serviceWorker.getRegistrations().then(function (registrations) {
      registrations
        .filter(function (registration) {
          var worker = registration.active || registration.waiting || registration.installing;
          return worker && new URL(worker.scriptURL).pathname === WORKER;
        })
        .forEach(function (registration) { registration.unregister(); });
    });
    if (window.caches) {
      caches.keys().then(function (names) {
        names
          .filter(function (name) { return name.indexOf('prism-saved-pages') === 0; })
          .forEach(function (name) { caches.delete(name); });
      });
    }
  }
})();
