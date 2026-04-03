using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace UmbracoPrism.Core.TagHelpers;

[HtmlTargetElement("prism-mobile-user-agent-demo")]
public class PrismMobileUserAgentDemoTagHelper : TagHelper
{
    public string Marker { get; set; } = "PrismMobile";

    public string StorageKey { get; set; } = "prism.demo.mobileUa";

    public string QueryParam { get; set; } = "prismMobile";

    public string Title { get; set; } = "Demo PrismMobile UserAgent";

    public bool ShowToggle { get; set; } = true;

    public bool Inline { get; set; }

    public bool Compact { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        var markerJs = EscapeJsString(Marker);
        var storageKeyJs = EscapeJsString(StorageKey);
        var queryParamJs = EscapeJsString(QueryParam);
        var cookieKeyJs = "prism.mobile";
        var titleHtml = System.Net.WebUtility.HtmlEncode(Title);

        var bootstrapHtml = BootstrapTemplate
            .Replace("__MARKER__", markerJs)
            .Replace("__STORAGE_KEY__", storageKeyJs)
            .Replace("__QUERY_PARAM__", queryParamJs)
            .Replace("__COOKIE_KEY__", cookieKeyJs);

        var sb = new StringBuilder();
        sb.Append(bootstrapHtml);

        if (ShowToggle)
        {
            var classes = "prism-mobile-ua-demo";
            if (Inline)
            {
                classes += " prism-mobile-ua-demo--inline";
            }

            if (Compact)
            {
                classes += " prism-mobile-ua-demo--compact";
            }

            var toggleHtml = ToggleTemplate
                .Replace("__MARKER__", markerJs)
                .Replace("__STORAGE_KEY__", storageKeyJs)
                .Replace("__COOKIE_KEY__", cookieKeyJs)
                .Replace("__TITLE__", titleHtml)
                .Replace("__CLASSES__", classes);

            sb.Append(toggleHtml);
        }

        output.Content.SetHtmlContent(sb.ToString());
    }

    private static string EscapeJsString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "")
            .Replace("\n", "");
    }

    private const string BootstrapTemplate = """
<style>
    .prism-mobile-ua-demo {
        position: fixed;
        right: 1rem;
        bottom: 1rem;
        background: white;
        border: 1px solid #d1d5db;
        border-radius: 8px;
        padding: 0.75rem 2.25rem 0.75rem 0.75rem;
        box-shadow: 0 2px 4px rgba(0,0,0,0.12);
        font-size: 0.85rem;
        z-index: var(--prism-demo-widget-z-index, 1001);
        min-width: 260px;
        font-family: -apple-system, system-ui, sans-serif;
    }

    html.prism-mobile .prism-mobile-ua-demo {
        bottom: calc(var(--prism-mobile-nav-height, 5rem) + 1.5rem);
    }

    .prism-mobile-ua-demo__row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-bottom: 0.35rem;
    }

    .prism-mobile-ua-demo__status {
        color: #4b5563;
        font-size: 0.8rem;
        margin: 0;
    }

    .prism-mobile-ua-demo__hint {
        color: #6b7280;
        font-size: 0.75rem;
        margin: 0.35rem 0 0;
    }

    .prism-mobile-ua-demo--inline {
        position: static;
        min-width: 0;
        background: rgba(255, 255, 255, 0.15);
        border-color: rgba(255, 255, 255, 0.4);
        color: #ffffff;
        margin: 0;
    }

    .prism-mobile-ua-demo--inline .prism-mobile-ua-demo__status,
    .prism-mobile-ua-demo--inline .prism-mobile-ua-demo__hint {
        color: rgba(255, 255, 255, 0.85);
    }

    .prism-mobile-ua-demo--compact .prism-mobile-ua-demo__status,
    .prism-mobile-ua-demo--compact .prism-mobile-ua-demo__hint {
        display: none;
    }

    .prism-mobile-ua-demo--compact .prism-mobile-ua-demo__row {
        margin-bottom: 0;
    }

    .prism-mobile-ua-demo__close {
        position: absolute;
        top: 0.4rem;
        right: 0.5rem;
        background: none;
        border: none;
        cursor: pointer;
        font-size: 1rem;
        line-height: 1;
        color: #6b7280;
        padding: 0.15rem 0.3rem;
        border-radius: 4px;
    }

    .prism-mobile-ua-demo__close:hover {
        color: #111827;
        background: rgba(0, 0, 0, 0.06);
    }

    .prism-mobile-ua-demo--inline .prism-mobile-ua-demo__close {
        color: rgba(255, 255, 255, 0.7);
    }

    .prism-mobile-ua-demo--inline .prism-mobile-ua-demo__close:hover {
        color: #ffffff;
        background: rgba(255, 255, 255, 0.15);
    }
</style>
<script>
    (() => {
        const marker = '__MARKER__';
        const storageKey = '__STORAGE_KEY__';
        const queryParam = '__QUERY_PARAM__';
        const cookieKey = '__COOKIE_KEY__';
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
</script>
""";

    private const string ToggleTemplate = """
<aside class="__CLASSES__" aria-live="polite">
    <button class="prism-mobile-ua-demo__close" id="prism-mobile-ua-close" aria-label="Dismiss">&#x2715;</button>
    <div class="prism-mobile-ua-demo__row">
        <input type="checkbox" id="prism-mobile-ua-toggle" />
        <label for="prism-mobile-ua-toggle"><strong>__TITLE__</strong></label>
    </div>
    <p class="prism-mobile-ua-demo__status" id="prism-mobile-ua-status"></p>
    <p class="prism-mobile-ua-demo__hint">Toggles a page-level UA mock and reloads this page.</p>
</aside>
<script>
    (() => {
        const marker = '__MARKER__';
        const storageKey = '__STORAGE_KEY__';
        const cookieKey = '__COOKIE_KEY__';
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
</script>
""";
}
