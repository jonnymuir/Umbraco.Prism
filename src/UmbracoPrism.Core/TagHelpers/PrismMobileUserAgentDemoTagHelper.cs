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

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        var markerJs = EscapeJsString(Marker);
        var storageKeyJs = EscapeJsString(StorageKey);
        var queryParamJs = EscapeJsString(QueryParam);
        var titleHtml = System.Net.WebUtility.HtmlEncode(Title);

        var bootstrapHtml = BootstrapTemplate
            .Replace("__MARKER__", markerJs)
            .Replace("__STORAGE_KEY__", storageKeyJs)
            .Replace("__QUERY_PARAM__", queryParamJs);

        var sb = new StringBuilder();
        sb.Append(bootstrapHtml);

        if (ShowToggle)
        {
            var toggleHtml = ToggleTemplate
                .Replace("__MARKER__", markerJs)
                .Replace("__STORAGE_KEY__", storageKeyJs)
                .Replace("__TITLE__", titleHtml);

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
        padding: 0.75rem;
        box-shadow: 0 2px 4px rgba(0,0,0,0.12);
        font-size: 0.85rem;
        z-index: 1000;
        min-width: 260px;
        font-family: -apple-system, system-ui, sans-serif;
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
</style>
<script>
    (() => {
        const marker = '__MARKER__';
        const storageKey = '__STORAGE_KEY__';
        const queryParam = '__QUERY_PARAM__';
        const query = new URLSearchParams(window.location.search);

        if (query.has(queryParam)) {
            const requested = query.get(queryParam) === '1';
            try {
                localStorage.setItem(storageKey, requested ? '1' : '0');
            } catch {
                return;
            }
        }

        let shouldMockMobile = false;
        try {
            shouldMockMobile = localStorage.getItem(storageKey) === '1';
        } catch {
            return;
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
    })();
</script>
""";

    private const string ToggleTemplate = """
<aside class="prism-mobile-ua-demo" aria-live="polite">
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
        const toggle = document.getElementById('prism-mobile-ua-toggle');
        const status = document.getElementById('prism-mobile-ua-status');

        if (!(toggle instanceof HTMLInputElement) || !(status instanceof HTMLParagraphElement)) {
            return;
        }

        let enabled = false;
        try {
            enabled = localStorage.getItem(storageKey) === '1';
        } catch {
            status.textContent = 'localStorage is unavailable in this browser context.';
            return;
        }

        toggle.checked = enabled;

        const updateStatus = () => {
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
            localStorage.setItem(storageKey, toggle.checked ? '1' : '0');
            window.location.reload();
        });
    })();
</script>
""";
}
