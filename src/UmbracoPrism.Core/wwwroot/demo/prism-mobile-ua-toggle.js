// Wires up the mobile-UA-mock toggle widget rendered by PrismMobileUserAgentDemoTagHelper.
// Config comes from this script tag's own data-* attributes (read via document.currentScript) —
// see prism-mobile-ua-bootstrap.js's own comment for why.
(() => {
    const script = document.currentScript;
    const marker = script?.dataset.marker ?? 'PrismMobile';
    const storageKey = script?.dataset.storageKey ?? 'prism.demo.mobileUa';
    const cookieKey = script?.dataset.cookieKey ?? 'prism.mobile';
    const dismissKey = storageKey + '.dismissed';
    const toggle = document.getElementById('prism-mobile-ua-toggle');
    const status = document.getElementById('prism-mobile-ua-status');
    const closeBtn = document.getElementById('prism-mobile-ua-close');
    const widget = closeBtn?.closest('.prism-mobile-ua-demo');

    const writeServerCookie = (enabled) => {
        const maxAge = enabled ? '31536000' : '0';
        document.cookie = cookieKey + '=' + (enabled ? '1' : '0') + '; path=/; max-age=' + maxAge + '; samesite=lax';
    };

    const readServerCookie = () => {
        return document.cookie.split(';').some((part) => part.trim() === cookieKey + '=1');
    };

    try {
        if (sessionStorage.getItem(dismissKey) === '1' && widget instanceof HTMLElement) {
            widget.style.display = 'none';
        }
    } catch { }

    if (closeBtn instanceof HTMLButtonElement && widget instanceof HTMLElement) {
        closeBtn.addEventListener('click', () => {
            widget.style.display = 'none';
            try { sessionStorage.setItem(dismissKey, '1'); } catch { }
        });
    }

    if (!(toggle instanceof HTMLInputElement)) {
        return;
    }

    let enabled = false;
    try {
        enabled = localStorage.getItem(storageKey) === '1';
    } catch {
        enabled = readServerCookie();
    }

    toggle.checked = enabled;

    const updateStatus = () => {
        if (!(status instanceof HTMLParagraphElement)) {
            return;
        }

        const hasMarker = navigator.userAgent.includes(marker);
        if (window.__prismMobileUaMockFailed) {
            status.textContent = 'UA override failed in this browser. Use DevTools UA override instead.';
            return;
        }

        status.textContent = hasMarker
            ? 'Current UA contains ' + marker + '.'
            : 'Current UA does not contain ' + marker + '.';
    };

    updateStatus();

    toggle.addEventListener('change', () => {
        writeServerCookie(toggle.checked);
        try {
            localStorage.setItem(storageKey, toggle.checked ? '1' : '0');
        } catch { }
        window.location.reload();
    });
})();
