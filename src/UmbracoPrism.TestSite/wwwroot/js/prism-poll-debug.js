// TEMPORARY diagnostic overlay for the join-gateway wait screen (Wayfinder.Rendering.GovUk's
// wayfinder-poll.js). Reported live: the automatic re-poll on foreground/resume worked once, then
// stopped working on a later attempt, with no obvious cause. wayfinder-poll.js keeps its own
// polling state (retries, pendingPoll, inFlight) entirely closured inside its own IIFE — nothing
// is exposed for an outside script to read — so the only way to see what it's actually doing
// without a devtools-attached debug build is to observe it from the outside: patch window.fetch
// before its own <script> runs (this script is placed in <head>, before @RenderBody(), so it
// always executes first) and independently track Page Visibility / Capacitor resume events.
//
// One concrete thing already worth watching for: wayfinder-poll.js's Mode B gives up permanently
// after maxRetries (default 100, and nothing in Wayfinder.Umbraco's own _Stage-Waiting.cshtml ever
// sets data-wayfinder-poll-max-retries to anything else) — and every foreground/resume event also
// counts as a retry, not just the timer-driven ones. At this blueprint's 2-second poll interval
// that's a 100-poll budget that can run out in well under its nominal ~200-second span if the app
// is backgrounded/foregrounded repeatedly while waiting, and once it's gone, nothing (not even a
// later foreground event) re-arms it — only a real page reload does. This overlay's own poll
// counter is the fastest way to confirm or rule that out.
//
// Gated on Capacitor native platform only — an ordinary browser tab already has real devtools.
// Remove once the intermittent report above is diagnosed; this was never meant to ship long-term.
(function () {
  var Cap = window.Capacitor;
  if (!Cap || !Cap.isNativePlatform || !Cap.isNativePlatform()) {
    return;
  }

  var KNOWN_MAX_RETRIES = 100; // wayfinder-poll.js's own hardcoded default, see remarks above.

  var pollCount = 0;
  var foregroundCount = 0;
  var log = [];
  var panel = null;
  var pollUrlPrefix = null;

  function timestamp() {
    return new Date().toTimeString().slice(0, 8);
  }

  function addLog(line) {
    log.unshift(timestamp() + ' ' + line);
    log = log.slice(0, 8);
    render();
  }

  function render() {
    if (!panel) {
      return;
    }
    panel.textContent =
      '[poll debug] polls=' + pollCount + '/' + KNOWN_MAX_RETRIES +
      ' foreground-events=' + foregroundCount +
      ' hidden=' + document.hidden + '\n' + log.join('\n');
  }

  function attachPanel() {
    panel = document.createElement('div');
    panel.style.cssText =
      'position:fixed;bottom:0;left:0;right:0;z-index:2147483647;' +
      'background:rgba(0,0,0,0.85);color:#39ff14;font:11px monospace;padding:8px;' +
      'max-height:40vh;overflow-y:auto;white-space:pre-wrap;pointer-events:none;';
    document.body.appendChild(panel);
    render();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', attachPanel);
  } else {
    attachPanel();
  }

  // The poll URL isn't known until _Stage-Waiting.cshtml renders #wayfinder-waiting-data — this
  // script runs earlier (in <head>), so it re-checks on DOMContentLoaded rather than assuming a
  // wait screen is even present on this page at all.
  document.addEventListener('DOMContentLoaded', function () {
    var waitingEl = document.getElementById('wayfinder-waiting-data');
    if (!waitingEl) {
      return;
    }
    var fullUrl = waitingEl.getAttribute('data-wayfinder-poll-url');
    pollUrlPrefix = fullUrl ? fullUrl.split('&')[0] : null;
    addLog('wait screen detected, pollUrl=' + (fullUrl || '(none)'));
  });

  var originalFetch = window.fetch;
  window.fetch = function (input) {
    var url = typeof input === 'string' ? input : (input && input.url);
    if (pollUrlPrefix && url && url.indexOf(pollUrlPrefix) === 0) {
      pollCount++;
      var thisPoll = pollCount;
      addLog('poll #' + thisPoll + ' sent');
      return originalFetch.apply(this, arguments).then(
        function (response) {
          response
            .clone()
            .json()
            .then(function (data) {
              addLog('poll #' + thisPoll + ' -> ' + response.status + ' changed=' + (data && data.changed));
            })
            .catch(function () {
              addLog('poll #' + thisPoll + ' -> ' + response.status + ' (non-JSON body)');
            });
          return response;
        },
        function (error) {
          addLog('poll #' + thisPoll + ' -> FAILED ' + error);
          throw error;
        }
      );
    }
    return originalFetch.apply(this, arguments);
  };

  document.addEventListener('visibilitychange', function () {
    addLog('visibilitychange -> hidden=' + document.hidden);
  });

  if (Cap.Plugins && Cap.Plugins.App && Cap.Plugins.App.addListener) {
    Cap.Plugins.App.addListener('resume', function () {
      foregroundCount++;
      addLog('Capacitor resume event (#' + foregroundCount + ')');
    });
  }
})();
