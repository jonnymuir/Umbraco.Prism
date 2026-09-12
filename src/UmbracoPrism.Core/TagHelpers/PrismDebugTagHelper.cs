using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Security.Claims;
using UmbracoPrism.Core.Models;
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
                        <form method="post" action="/auth/logout" style="margin-top:10px;">
                            <input type="hidden" name="{antiforgeryTokens.FormFieldName}" value="{antiforgeryTokens.RequestToken}" />
                            <button type="submit" class="btn" style="background:#495057; border:none; cursor:pointer;">Sign Out</button>
                        </form>
                    </div>
                    """);

                // 2.5 Claims Section
                sb.Append($"""
                    <div class="card">
                        <h2>Attributes & Claims <button class="copy-btn" data-action="copy-to-clipboard" data-copy-target="prism-claims-data">Copy</button></h2>
                        <div id="prism-claims-data" style="max-height: 300px; overflow-y: auto;">
                            <table style="width:100%; font-size: 0.8rem; border-collapse: collapse;">
                                {string.Join("", ViewContext.HttpContext.User.Claims.Select(c =>
                                    $"<tr style='border-bottom:1px solid #eee'><td style='padding:4px;'>{c.Type.Split('/').Last()}</td><td><code>{c.Value}</code></td></tr>"))}
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
            var authMode = isPrismAuthGlobalEnabled ? "<b style=\"color:#0ca678;\">ACTIVE</b>" : "<b style=\"color:#f08c00;\">PASSIVE</b>";
            var schemesHtml = string.Join(" ", allSchemes.Select(s => $"<code>{s.Name}</code>"));
            var oidcOptions = ViewContext.HttpContext.RequestServices
                            .GetRequiredService<IOptionsSnapshot<OpenIdConnectOptions>>()
                            .Get("PrismEntraID");


            sb.Append($"""
                <div class="card" style="font-size: 0.85rem; color: #495057;">
                    <h2 style="font-size: 1rem;">🛠 System Diagnostics</h2>
                    <ul>
                        <li><strong>Vault URI:</strong> {(isPrismAuthGlobalEnabled ? vaultUri : "❌ Not Configured")}</li>
                        <li><strong>Prism Auth Mode:</strong> {authMode} <em>(Login flow control)</em></li>
                        <li><strong>Prism Mobile Request:</strong> {(isPrismMobileRequest ? "<b style=\"color:#0ca678;\">YES</b>" : "<b style=\"color:#868e96;\">NO</b>")}</li>
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
            sb.Append($"<div class='card' style='color:red;'>{ex.Message}</div>");
        }

        output.Content.SetHtmlContent(sb.ToString());
    }
}