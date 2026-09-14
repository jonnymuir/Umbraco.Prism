// Prism mobile nav progress: shows a full-screen spinner the instant the user taps a same-
// document link or submits a form.
//
// Native only (gated on Capacitor.isNativePlatform()): a normal browser tab already avoids a
// blank flash between pages via "paint holding" (WebKit/Chromium hold the outgoing page's last
// frame on screen until the incoming page has something to paint) — a spinner there would be
// redundant, possibly janky, on top of behaviour that's already fine. This app's WKWebView,
// embedded the way Capacitor uses it, doesn't reliably get that same treatment, and every
// navigation here is a real full-page HTTP round trip (no SPA, no client-side routing — see
// docs), so there's a real gap with nothing rendered otherwise. @view-transition in
// prism-mobile-shell.css only cross-fades content that has already arrived; it can't paper over
// the round-trip time itself. This is the robust fix: always show *something*.
(function () {
  var Cap = window.Capacitor;
  if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return;

  var overlay = null;
  // Safety net, not a timing budget this is expected to need (same reasoning as
  // prism-biometric-signout.js's own SILENT_IFRAME_TIMEOUT_MS): a real navigation destroys this
  // whole document, overlay included, so nothing needs to hide it in the success case. This only
  // matters if a tap turns out not to actually navigate — e.g. client-side validation blocking a
  // form submit after this capture-phase listener already ran.
  var AUTO_HIDE_MS = 6000;

  function show() {
    if (overlay) return;
    overlay = document.createElement('div');
    overlay.id = 'prism-nav-progress';
    overlay.style.cssText = 'position:fixed;inset:0;z-index:999999;display:flex;' +
      'align-items:center;justify-content:center;background:var(--prism-surface,#fff);';
    overlay.innerHTML = '<div style="width:36px;height:36px;border-radius:50%;' +
      'border:3px solid var(--prism-border,#e5e7eb);border-top-color:var(--prism-primary,#2563eb);' +
      'animation:prism-nav-spin .7s linear infinite;"></div>' +
      '<style>@keyframes prism-nav-spin{to{transform:rotate(360deg)}}</style>';
    document.body.appendChild(overlay);
    setTimeout(function () {
      if (overlay) { overlay.remove(); overlay = null; }
    }, AUTO_HIDE_MS);
  }

  function isSameDocumentUrl(href) {
    if (!href || href.startsWith('#')) return false;
    if (href.startsWith('mailto:') || href.startsWith('tel:') || href.startsWith('javascript:')) return false;
    try {
      return new URL(href, window.location.href).origin === window.location.origin;
    } catch (e) {
      return false;
    }
  }

  document.addEventListener('click', function (event) {
    var target = event.target;
    if (!(target instanceof Element)) return;
    var anchor = target.closest('a');
    if (!anchor) return;
    if (anchor.target && anchor.target.toLowerCase() === '_blank') return;
    if (!isSameDocumentUrl(anchor.getAttribute('href'))) return;
    show();
  }, true);

  document.addEventListener('submit', function (event) {
    if (event.target instanceof HTMLFormElement) show();
  }, true);
})();
