// Mocks navigator.userAgent for the mobile-vs-browser feature showcase — see
// PrismMobileUserAgentDemoTagHelper. Config comes from this script tag's own data-* attributes
// (read via document.currentScript), not spliced into the script body, so this file has no
// per-request/per-tenant variation at all and needs no CSP unsafe-inline/nonce exception.
(() => {
    const script = document.currentScript;
    const marker = script?.dataset.marker ?? 'PrismMobile';
    const storageKey = script?.dataset.storageKey ?? 'prism.demo.mobileUa';
    const queryParam = script?.dataset.queryParam ?? 'prismMobile';
    const cookieKey = script?.dataset.cookieKey ?? 'prism.mobile';
    const query = new URLSearchParams(window.location.search);

    const writeServerCookie = (enabled) => {
        const maxAge = enabled ? '31536000' : '0';
        document.cookie = cookieKey + '=' + (enabled ? '1' : '0') + '; path=/; max-age=' + maxAge + '; samesite=lax';
    };

    const readServerCookie = () => {
        return document.cookie.split(';').some((part) => part.trim() === cookieKey + '=1');
    };

    if (query.has(queryParam)) {
        const requested = query.get(queryParam) === '1';
        writeServerCookie(requested);
        try {
            localStorage.setItem(storageKey, requested ? '1' : '0');
        } catch { }
    }

    let shouldMockMobile = false;
    try {
        shouldMockMobile = localStorage.getItem(storageKey) === '1';
    } catch {
        shouldMockMobile = false;
    }

    if (!shouldMockMobile) {
        shouldMockMobile = readServerCookie();
    }

    if (!shouldMockMobile) return;

    const originalUserAgent = navigator.userAgent || '';
    const mockedUserAgent = originalUserAgent.includes(marker)
        ? originalUserAgent
        : originalUserAgent + ' ' + marker;

    const descriptor = {
        configurable: true,
        get: () => mockedUserAgent
    };

    try {
        Object.defineProperty(Navigator.prototype, 'userAgent', descriptor);
    } catch {
        try {
            Object.defineProperty(window.navigator, 'userAgent', descriptor);
        } catch {
            window.__prismMobileUaMockFailed = true;
        }
    }

    if (navigator.userAgent.includes(marker)) {
        document.documentElement.classList.add('prism-mobile');
    } else {
        document.documentElement.classList.remove('prism-mobile');
    }
})();
