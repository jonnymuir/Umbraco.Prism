using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Security.Claims;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using UmbracoPrism.Core.Extensions;

namespace UmbracoPrism.Core.TagHelpers;

[HtmlTargetElement("prism-debug")]
public class PrismDebugTagHelper(
    IPrismContext prismContext,
    IPrismUserContext prismUser,
    ITenantService tenantService,
    IUmbracoDatabaseFactory databaseFactory,
    IConfiguration config,
    IAuthenticationSchemeProvider schemeProvider,
    IWebHostEnvironment environment,
    IAntiforgery antiforgery) : TagHelper
{
    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // Phase 1 Security: Only render debug output in Development environment
        // or when explicitly enabled via Prism:EnableDebugPanel config
        var isDebugEnabled = environment.IsDevelopment() 
            || config.GetValue<bool>("Prism:EnableDebugPanel", false);
        
        if (!isDebugEnabled)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "prism-debug-root");

        var sb = new StringBuilder();

        try
        {
            var tenant = prismContext.CurrentTenant;
            var vaultUri = config["Prism:VaultUri"];
            var isPrismAuthGlobalEnabled = !string.IsNullOrEmpty(vaultUri);
            var isTenantConfigured = !string.IsNullOrEmpty(tenant?.EntraTenantId);
            var allSchemes = await schemeProvider.GetAllSchemesAsync();
            var tenantCacheMetrics = tenantService.GetCacheMetrics();
            var host = ViewContext.HttpContext.Request.Host;
            var path = ViewContext.HttpContext.Request.Path;
            var isPrismMobileRequest = PrismMobileRequestDetection.IsPrismMobileRequest(ViewContext.HttpContext);
            var mobileDetectionSource = PrismMobileRequestDetection.GetPrismMobileDetectionSource(ViewContext.HttpContext);

            // SEC-PT2-004: externalized (not spliced inline), same rationale as every other
            // formerly-inline asset in this repo — needs no CSP unsafe-inline/nonce exception.
            sb.Append("""
                <link rel="stylesheet" href="/App_Plugins/UmbracoPrism/diagnostics/prism-debug.css" />
                <script src="/App_Plugins/UmbracoPrism/diagnostics/prism-debug-copy.js"></script>
                <h1>Umbraco Prism Runtime</h1>
                """);

            // 1. Tenant Section
            sb.Append($"""
                <div class="card">
                    <h2>📡 Tenant Info <button class="copy-btn" data-action="copy-to-clipboard" data-copy-target="prism-tenant-data">Copy</button></h2>
                    <div id="prism-tenant-data">
                        <p><strong>Name:</strong> {tenant?.Name ?? "None"}</p>
                        <p><strong>Entra ID:</strong> <code>{tenant?.EntraTenantId ?? "N/A"}</code></p>
                        <p><strong>Host:</strong> <code>{host}</code></p>
                    </div>
                </div>
                """);

            // 1b. Branding overrides: the cached tenant (what /umbraco/prism/branding.css is built
            // from) next to a fresh read of the same DB row, so a stale cache and a stale save can
            // be told apart from what the browser actually received.
            sb.Append(BuildBrandingCard(tenant, isPrismMobileRequest));

            // 2. Identity Section
            if (prismUser.IsAuthenticated)
            {
                // AccountController.Logout requires POST + a valid antiforgery token (logout-CSRF
                // protection, SEC-PT2-003) — a plain <a href> GET link silently fails (no matching
                // route), so this renders the same real form/token pair homePage.cshtml's own
                // working Sign Out button uses, not a link.
                var antiforgeryTokens = antiforgery.GetAndStoreTokens(ViewContext.HttpContext);
                sb.Append($"""
                    <div class="card">
                        <h2>👤 Identity <button class="copy-btn" data-action="copy-to-clipboard" data-copy-target="prism-user-data">Copy</button></h2>
                        <div id="prism-user-data">
                            <p><strong>User:</strong> {prismUser.Name}</p>
                            <p><strong>Email:</strong> {prismUser.Email}</p>
                            <p><strong>TID:</strong> <code>{prismUser.EntraTenantId}</code></p>
                        </div>
                        <form method="post" action="/auth/logout" class="logout-form">
                            <input type="hidden" name="{antiforgeryTokens.FormFieldName}" value="{antiforgeryTokens.RequestToken}" />
                            <button type="submit" class="btn logout-btn">Sign Out</button>
                        </form>
                    </div>
                    """);

                // 2.5 Claims Section
                sb.Append($"""
                    <div class="card">
                        <h2>Attributes & Claims <button class="copy-btn" data-action="copy-to-clipboard" data-copy-target="prism-claims-data">Copy</button></h2>
                        <div id="prism-claims-data" class="claims-box">
                            <table class="claims-table">
                                {string.Join("", ViewContext.HttpContext.User.Claims.Select(c =>
                                    $"<tr class='claims-row'><td class='claims-cell'>{c.Type.Split('/').Last()}</td><td><code>{c.Value}</code></td></tr>"))}
                            </table>
                        </div>
                    </div>
                    """);
            }
            else
            {
                sb.Append($"""
                    <div class="card">
                        <h2>👤 Identity</h2>
                        <p>Guest Session</p>
                        {(isTenantConfigured ? "<a href='/auth/login' class='btn btn-login'>Sign In</a>" : "<em>Tenant not configured for Auth</em>")}
                    </div>
                    """);
            }

            // Manual reconstruction of the MSAL Home Account ID
            var oid = ViewContext.HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                      ?? ViewContext.HttpContext.User.FindFirst("oid")?.Value;
            var tid = ViewContext.HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
                      ?? ViewContext.HttpContext.User.FindFirst("tid")?.Value;

            sb.Append($"""
                <div class="card">
                    <h2>HttpContext Debug <button class="copy-btn" data-action="copy-to-clipboard" data-copy-target="prism-cache-debug">Copy</button></h2>
                    <div id="prism-cache-debug">
                        <p><strong>OID found:</strong> <code>{oid ?? "MISSING"}</code></p>
                        <p><strong>TID found:</strong> <code>{tid ?? "MISSING"}</code></p>
                    </div>
                </div>
                """);

            // 3. System Diagnostics
            var authMode = isPrismAuthGlobalEnabled ? "<b class=\"status-ok\">ACTIVE</b>" : "<b class=\"status-warn\">PASSIVE</b>";
            var schemesHtml = string.Join(" ", allSchemes.Select(s => $"<code>{s.Name}</code>"));
            var oidcOptions = ViewContext.HttpContext.RequestServices
                            .GetRequiredService<IOptionsSnapshot<OpenIdConnectOptions>>()
                            .Get("PrismEntraID");


            sb.Append($"""
                <div class="card diagnostics-card">
                    <h2 class="diagnostics-title">🛠 System Diagnostics</h2>
                    <ul>
                        <li><strong>Vault URI:</strong> {(isPrismAuthGlobalEnabled ? vaultUri : "❌ Not Configured")}</li>
                        <li><strong>Prism Auth Mode:</strong> {authMode} <em>(Login flow control)</em></li>
                        <li><strong>Prism Mobile Request:</strong> {(isPrismMobileRequest ? "<b class=\"status-ok\">YES</b>" : "<b class=\"status-muted\">NO</b>")}</li>
                        <li><strong>Mobile Detection Source:</strong> <code>{mobileDetectionSource}</code></li>
                        <li><strong>Active Schemes:</strong> {schemesHtml}</li>
                        <li><strong>Request Path:</strong> <code>{path}</code></li>
                        <li><strong>Scheme Authority:</strong> <code>{oidcOptions.Authority}</code></li>
                        <li><strong>Tenant Cache Hits:</strong> <code>{tenantCacheMetrics.Hits}</code></li>
                        <li><strong>Tenant Cache Misses:</strong> <code>{tenantCacheMetrics.Misses}</code></li>
                        <li><strong>Tenant Cache Invalidations:</strong> <code>{tenantCacheMetrics.Invalidations}</code></li>
                        <li><strong>Tenant DB Loads:</strong> <code>{tenantCacheMetrics.DatabaseLoads}</code></li>
                    </ul>
                </div>
                """);
        }
        catch (Exception ex)
        {
            sb.Append($"<div class='card error-card'>{ex.Message}</div>");
        }

        output.Content.SetHtmlContent(sb.ToString());
    }

    private string BuildBrandingCard(PrismTenant? tenant, bool includeMobileOverrides)
    {
        if (tenant is null)
        {
            return "<div class=\"card\"><h2>🎨 Branding Overrides</h2><p>No tenant resolved for this host.</p></div>";
        }

        var cached = tenant.BrandingOverrides ?? new Dictionary<string, string>();
        var cachedMobile = tenant.MobileBrandingOverrides ?? new Dictionary<string, string>();

        Dictionary<string, string>? stored = null;
        Dictionary<string, string>? storedMobile = null;
        string? dbError = null;
        try
        {
            using var db = databaseFactory.CreateDatabase();
            var row = db.SingleOrDefaultById<PrismTenantSchema>(tenant.Id);
            stored = ParseOverrides(row?.BrandingOverrides);
            storedMobile = ParseOverrides(row?.MobileBrandingOverrides);
        }
        catch (Exception ex)
        {
            dbError = ex.Message;
        }

        var servedCss = PrismBrandingCssBuilder.BuildCssOverrides(
            tenant.BrandingOverrides,
            includeMobileOverrides ? tenant.MobileBrandingOverrides : null,
            tenant.BrandingCssDeclarations,
            includeMobileOverrides ? tenant.MobileBrandingCssDeclarations : null);

        var enc = HtmlEncoder.Default;
        var sb = new StringBuilder();
        sb.Append("<div class=\"card\"><h2>🎨 Branding Overrides <button class=\"copy-btn\" data-action=\"copy-to-clipboard\" data-copy-target=\"prism-branding-data\">Copy</button></h2>");
        sb.Append("<div id=\"prism-branding-data\">");
        sb.Append($"<p><strong>Tenant:</strong> <code>#{tenant.Id} {enc.Encode(tenant.Name ?? "")} ({enc.Encode(tenant.Hostname ?? "")})</code></p>");

        if (dbError is not null)
        {
            sb.Append($"<p><strong>DB read failed:</strong> <code>{enc.Encode(dbError)}</code></p>");
        }
        else
        {
            var matches = SameOverrides(cached, stored!) && SameOverrides(cachedMobile, storedMobile!);
            sb.Append($"<p><strong>Cache vs DB:</strong> {(matches ? "<b class=\"status-ok\">MATCH</b>" : "<b class=\"status-warn\">DIFFERS (cached tenant is stale)</b>")}</p>");
        }

        AppendOverrideList(sb, enc, "Cached desktop overrides", cached, stored);
        AppendOverrideList(sb, enc, "Cached mobile overrides", cachedMobile, storedMobile);
        sb.Append($"<p><strong>Served CSS ({(includeMobileOverrides ? "desktop + mobile" : "desktop only")}, {servedCss.Length} chars):</strong></p>");
        sb.Append($"<pre><code>{enc.Encode(servedCss.Replace(";", ";\n"))}</code></pre>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static void AppendOverrideList(
        StringBuilder sb,
        HtmlEncoder enc,
        string title,
        Dictionary<string, string> cached,
        Dictionary<string, string>? stored)
    {
        sb.Append($"<p><strong>{title} ({cached.Count}):</strong></p>");
        if (cached.Count == 0)
        {
            sb.Append("<p><em>none</em></p>");
            return;
        }

        sb.Append("<table class=\"claims-table\">");
        foreach (var (name, value) in cached.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var dbNote = stored is null
                ? ""
                : !stored.TryGetValue(name, out var dbValue)
                    ? " <b class=\"status-warn\">not in DB</b>"
                    : dbValue == value ? "" : $" <b class=\"status-warn\">DB: {enc.Encode(dbValue)}</b>";
            sb.Append($"<tr class=\"claims-row\"><td class=\"claims-cell\">{enc.Encode(name)}</td><td><code>{enc.Encode(value)}</code>{dbNote}</td></tr>");
        }

        sb.Append("</table>");
    }

    private static Dictionary<string, string> ParseOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static bool SameOverrides(Dictionary<string, string> a, Dictionary<string, string> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);
}
