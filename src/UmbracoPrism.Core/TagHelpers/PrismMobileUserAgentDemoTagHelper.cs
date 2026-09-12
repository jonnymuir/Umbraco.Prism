using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace UmbracoPrism.Core.TagHelpers;

/// <summary>
/// Cookie name that Playwright (or any automation tool) can set to suppress the
/// mobile helper toggle widget for a whole session.  The UA bootstrap script is
/// still emitted so mobile-UA behaviour continues to work in tests that need it.
/// Set the cookie value to "1" to enter screenshot mode; omit or set to "0" to
/// leave it unset (manual usage is unaffected).
/// </summary>
public static class PrismScreenshotMode
{
    public const string CookieName = "prism-screenshot-mode";
}

/// <summary>
/// Renders the mobile-vs-browser feature-showcase widget as a real stylesheet link plus two real
/// external scripts (App_Plugins static-asset routing — same mechanism as
/// prism-mobile-nav.js/mobile-shell), never spliced inline. Per-usage config (Marker/StorageKey/
/// QueryParam/CookieKey) is passed via each script tag's own data-* attributes, read at runtime
/// via document.currentScript — so the JS payload itself has no per-request variation and needs
/// no CSP unsafe-inline/nonce exception (SEC-PT2-004 follow-up: this TagHelper used to emit
/// literal &lt;style&gt;/&lt;script&gt; blocks directly, the last inline content of its kind in
/// this repo).
/// </summary>
[HtmlTargetElement("prism-mobile-user-agent-demo")]
public class PrismMobileUserAgentDemoTagHelper : TagHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PrismMobileUserAgentDemoTagHelper(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string Marker { get; set; } = "PrismMobile";

    public string StorageKey { get; set; } = "prism.demo.mobileUa";

    public string QueryParam { get; set; } = "prismMobile";

    public string Title { get; set; } = "Demo PrismMobile UserAgent";

    public bool ShowToggle { get; set; } = true;

    public bool Inline { get; set; }

    public bool Compact { get; set; }

    private bool IsScreenshotMode =>
        _httpContextAccessor.HttpContext?.Request.Cookies[PrismScreenshotMode.CookieName] == "1";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        // Screenshot mode overrides ShowToggle so automation gets clean images
        // while the UA bootstrap (below) continues to run for any test that needs it.
        var effectiveShowToggle = ShowToggle && !IsScreenshotMode;

        var markerAttr = System.Net.WebUtility.HtmlEncode(Marker);
        var storageKeyAttr = System.Net.WebUtility.HtmlEncode(StorageKey);
        var queryParamAttr = System.Net.WebUtility.HtmlEncode(QueryParam);
        const string cookieKeyAttr = "prism.mobile";
        var titleHtml = System.Net.WebUtility.HtmlEncode(Title);

        var sb = new StringBuilder();
        sb.Append("""<link rel="stylesheet" href="/App_Plugins/UmbracoPrism/demo/prism-mobile-ua-demo.css" />""");
        sb.Append($"""
            <script src="/App_Plugins/UmbracoPrism/demo/prism-mobile-ua-bootstrap.js"
                    data-marker="{markerAttr}" data-storage-key="{storageKeyAttr}"
                    data-query-param="{queryParamAttr}" data-cookie-key="{cookieKeyAttr}"></script>
            """);

        if (effectiveShowToggle)
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

            sb.Append($"""
                <aside class="{classes}" aria-live="polite">
                    <button class="prism-mobile-ua-demo__close" id="prism-mobile-ua-close" aria-label="Dismiss">&#x2715;</button>
                    <div class="prism-mobile-ua-demo__row">
                        <input type="checkbox" id="prism-mobile-ua-toggle" />
                        <label for="prism-mobile-ua-toggle"><strong>{titleHtml}</strong></label>
                    </div>
                    <p class="prism-mobile-ua-demo__status" id="prism-mobile-ua-status"></p>
                    <p class="prism-mobile-ua-demo__hint">Toggles a page-level UA mock and reloads this page.</p>
                </aside>
                <script src="/App_Plugins/UmbracoPrism/demo/prism-mobile-ua-toggle.js"
                        data-marker="{markerAttr}" data-storage-key="{storageKeyAttr}"
                        data-cookie-key="{cookieKeyAttr}"></script>
                """);
        }

        output.Content.SetHtmlContent(sb.ToString());
    }
}
