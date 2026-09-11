// Prism mobile shell guard.
// Previously spliced inline into every response by PrismBrandingMiddleware whenever a Prism
// mobile request was detected; now a plain static asset a host references explicitly
// (SEC-PT2-004 CSP follow-up — content never changes, so it needs no server involvement at
// all, and never needs `unsafe-inline`/a nonce). Hosts that support the Prism mobile shell
// include this unconditionally in their layout, e.g.:
//   <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-mobile-shell-guard.js"></script>
// See docs/walkthroughs/building-a-mobile-app.md.
(function () {
    // Any scheme capable of encoding executable content — not just javascript:. Checking one
    // and not the others is an incomplete guard (CWE-020/184): this function decides whether
    // to force a click/window.open into a same-window location.assign() instead of leaving
    // default browser handling alone, so an unchecked data:/vbscript: URL would otherwise get
    // forced into the current window rather than staying isolated the way default handling
    // (a new window, or no interception at all) would have kept it.
    function isDangerousScheme(url) {
        var normalized = url.trim().toLowerCase();
        return normalized.startsWith('javascript:')
            || normalized.startsWith('data:')
            || normalized.startsWith('vbscript:');
    }

    var root = document.documentElement;
    if (!root.classList.contains('prism-mobile')) {
        root.classList.add('prism-mobile');
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (!(target instanceof Element)) return;

        var anchor = target.closest('a');
        if (!anchor) return;

        var href = anchor.getAttribute('href');
        if (!href || href.startsWith('#') || isDangerousScheme(href)) return;

        if (href.startsWith('mailto:') || href.startsWith('tel:')) {
            event.preventDefault();
            return;
        }

        var forceInWebView = anchor.target && anchor.target.toLowerCase() === '_blank';
        if (!forceInWebView) return;

        event.preventDefault();
        window.location.assign(anchor.href);
    }, true);

    window.open = function (url) {
        if (typeof url === 'string' && url.length > 0 && !isDangerousScheme(url)) {
            window.location.assign(url);
        }
        return null;
    };
})();
