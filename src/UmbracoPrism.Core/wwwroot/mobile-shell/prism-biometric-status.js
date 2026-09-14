// Prism biometric status indicator.
// Rewrites a dashboard's own "Biometric Authentication" card to reflect this device's real
// enrollment state, replacing whatever static default markup a host renders — found live: a
// card that always said "Not configured / Set up via mobile app" regardless of actual state,
// even moments after a real, successful Face ID enrollment and sign-in, because nothing had
// ever wired it up to an actual check (the "active"/green styling already existed in CSS —
// only the JS to ever apply it was missing).
//
// A no-op outside the native app (Capacitor absent, or Cap.isNativePlatform() false) — the
// default static markup ("Not configured — Set up via mobile app") is exactly the right
// message on desktop web, where there's no biometric hardware to check at all.
//
// Hosts wire this in explicitly on whatever page renders the card, alongside data-prism-*
// hooks (see docs/walkthroughs/building-a-mobile-app.md):
//   <div data-prism-biometric-status>
//     <div data-prism-biometric-dot class="dash-bio-status__dot dash-bio-status__dot--inactive"></div>
//     <strong data-prism-biometric-label>Not configured</strong>
//     <span data-prism-biometric-hint>Set up via mobile app</span>
//   </div>
//   <div data-prism-biometric-steps>...</div>
//   <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-biometric-status.js"></script>
// The dot's "active" CSS class name is read from a data attribute rather than hardcoded here,
// so a host free to rename/restyle it (this file only ever adds/removes "--inactive"/"--active"
// suffixes on whatever base class the host's own markup already carries).
(function () {
  var Cap = window.Capacitor;
  if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) return;

  var statusEl = document.querySelector('[data-prism-biometric-status]');
  if (!statusEl) return;

  var TENANT_HOST = window.location.host;
  var SS_PFX = 'capacitor-storage_';
  var TOKEN_KEY = SS_PFX + 'prism_biometric_token_' + TENANT_HOST;
  var ENROLL_KEY = 'prism_biometric_enrollment_state_' + TENANT_HOST;

  // Same existing-enrollment check prism-biometric-enroll.js itself uses (a valid Keychain/
  // Keystore token AND a matching localStorage enrollment fingerprint from *this* install —
  // either alone can be stale, e.g. after a reinstall).
  (async function () {
    try {
      var storedResult = await Cap.nativePromise('SecureStorage', 'internalGetItem', { prefixedKey: TOKEN_KEY, sync: false });
      var hasToken = storedResult && storedResult.data && storedResult.data !== 'null';
      var enrolled = hasToken && !!localStorage.getItem(ENROLL_KEY);

      if (!enrolled) return; // the static "Not configured" markup is already correct

      var dot = statusEl.querySelector('[data-prism-biometric-dot]');
      var label = statusEl.querySelector('[data-prism-biometric-label]');
      var hint = statusEl.querySelector('[data-prism-biometric-hint]');
      var steps = document.querySelector('[data-prism-biometric-steps]');

      if (dot) {
        // The first class token is treated as the base (e.g. "dash-bio-status__dot"), matching
        // how the host's own markup is written: base class first, then a "--inactive"/"--active"
        // modifier. Strips the inactive modifier and adds the active one, leaving any other
        // classes the host may have added untouched.
        var classes = dot.className.split(' ').filter(function (c) { return c.indexOf('--inactive') === -1; });
        var base = classes[0];
        if (base && classes.indexOf(base + '--active') === -1) {
          classes.push(base + '--active');
        }
        dot.className = classes.join(' ');
      }
      if (label) label.textContent = 'Configured on this device';
      if (hint) hint.textContent = 'Face ID / Touch ID sign-in is enabled';
      if (steps) steps.hidden = true;
    } catch (e) {
      // Best-effort status indicator — leave the default static markup on any failure.
    }
  })();
})();
