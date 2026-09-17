// Prism mobile content-change watcher: tells the native paint-holding pipeline whenever the
// current page's DOM changes, so its cached "frozen frame" for the next navigation reflects what
// the user actually left on screen (typed values, scroll position, a selected option) rather than
// the page's just-loaded state.
//
// History: an earlier version tried to detect this via document-level input/change/scroll
// listeners injected as a native WKUserScript. Reported live: it never fired at all, on any page,
// across many builds. Root-caused by reading Capacitor's own vendored iOS source
// (CAPBridgeViewController.prepareWebView): the WKWebViewConfiguration.userContentController a
// host app's own webViewConfiguration(for:) override modifies gets discarded and replaced
// wholesale with Capacitor's own internal WKUserContentController one line later, before the real
// webview is ever built — so nothing added there was ever actually live, regardless of how
// correctly it was written or which events it listened for.
//
// This sidesteps that mechanism entirely: it's delivered as an ordinary server-rendered <script>
// tag (guaranteed to execute, exactly like prism-biometric-signout.js already does), and reports
// changes through Capacitor's own native plugin bridge (Cap.nativePromise — the same low-level
// call prism-biometric-signout.js already uses for SecureStorage) rather than a hand-rolled
// WKScriptMessageHandler.
//
// A MutationObserver, not just discrete event listeners, specifically because the earlier
// approach's raw event list (input/change/scroll/pointerup/touchend/click, widened repeatedly
// without success) still never explained the actual live symptom: pages whose interactive
// controls are custom components (sliders, radio groups) may update the DOM — position, an
// aria-* state, a displayed calculated value — without dispatching any of those standard events
// at all. Observing childList/attributes/characterData on the whole body catches the visible
// result of any such update, regardless of what triggered it.
//
// TEMPORARY diagnostics (diagPing calls below): reported live that the native plugin= counter
// this feeds stayed at 0 despite genuine on-page interaction, even though every step of the
// registration/JS-export/message-routing path was independently confirmed correct against
// Capacitor's own vendored iOS source. These pings report each stage of THIS script's own
// execution through a completely separate, already-proven-reliable WKScriptMessageHandler channel
// (the same one the pre-existing viewport-fix diagnostic uses) — deliberately not the still-
// unproven Capacitor plugin bridge this script is trying to diagnose — so which of
// ws=/wr=/wm=/we= climbs (or doesn't) on the native diagnostic label pinpoints exactly which link
// is broken. No-op (and never throws) on a build without mobile diagnostics compiled in, since
// window.webkit.messageHandlers.prismContentWatcherDiag simply won't exist there. Remove once
// root-caused.
(function () {
  function diagPing(kind) {
    try {
      if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.prismContentWatcherDiag) {
        window.webkit.messageHandlers.prismContentWatcherDiag.postMessage(kind);
      }
    } catch (e) {}
  }

  diagPing('script-started');

  var Cap = window.Capacitor;
  if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform() || !Cap.nativePromise) return;

  var pending = null;

  function notify() {
    if (pending) {
      clearTimeout(pending);
    }
    pending = setTimeout(function () {
      pending = null;
      diagPing('mutation');
      Cap.nativePromise('PrismContentWatcher', 'contentChanged', {}).catch(function () {
        diagPing('error');
      });
    }, 100);
  }

  // 1. Structural DOM changes (custom components, rendered views)
  new MutationObserver(notify).observe(document.body, {
    childList: true,
    subtree: true,
    attributes: true,
    characterData: true
  });

  // 2. Scrolling
  document.addEventListener('scroll', notify, true);
  window.addEventListener('scroll', notify, true);

  // 3. Live typing and text editing
  document.addEventListener('input', notify, true);

  // 4. Checkbox, radio, and select changes
  document.addEventListener('change', notify, true);

  // 5. Element blur (finishing edits)
  document.addEventListener('blur', notify, true);

  // 6. Text highlighting / cursor selection changes
  document.addEventListener('selectionchange', notify, true);

  diagPing('ready');
})();
