// Prism mobile shell guard.
// Previously spliced inline into every response by PrismBrandingMiddleware whenever a Prism
// mobile request was detected; now a plain static asset a host references explicitly
// (SEC-PT2-004 CSP follow-up — content never changes, so it needs no server involvement at
// all, and never needs `unsafe-inline`/a nonce). Hosts that support the Prism mobile shell
// include this unconditionally in their layout, e.g.:
//   <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-mobile-shell-guard.js"></script>
// See docs/walkthroughs/building-a-mobile-app.md.
(function () {
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
        if (!href || href.startsWith('#') || href.startsWith('javascript:')) return;

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
        if (typeof url === 'string' && url.length > 0) {
            window.location.assign(url);
        }
        return null;
    };
})();
