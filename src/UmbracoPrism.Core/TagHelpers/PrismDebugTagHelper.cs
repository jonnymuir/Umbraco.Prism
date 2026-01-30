using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Security.Claims;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace UmbracoPrism.Core.TagHelpers;

[HtmlTargetElement("prism-debug")]
public class PrismDebugTagHelper(
    IPrismContext prismContext,
    IPrismUserContext prismUser,
    IConfiguration config,
    IAuthenticationSchemeProvider schemeProvider) : TagHelper
{
    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
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
            var host = ViewContext.HttpContext.Request.Host;
            var path = ViewContext.HttpContext.Request.Path;

            sb.Append("""
                <style>
                    .prism-debug-root { font-family: -apple-system, system-ui, sans-serif; line-height: 1.6; padding: 2rem; background: #f0f2f5; color: #1c1e21; border-radius: 12px; margin: 20px 0; border: 1px solid #ddd; }
                    .prism-debug-root .card { background: white; padding: 1.5rem; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.05); margin-bottom: 1.5rem; border: 1px solid #e1e4e8; position: relative; }
                    .prism-debug-root h2 { font-size: 1.1rem; margin-top: 0; border-bottom: 1px solid #eee; padding-bottom: 0.5rem; display: flex; justify-content: space-between; align-items: center; }
                    .prism-debug-root .copy-btn { font-size: 0.7rem; background: #e9ecef; border: 1px solid #dee2e6; padding: 2px 8px; border-radius: 4px; cursor: pointer; color: #495057; }
                    .prism-debug-root .copy-btn:hover { background: #dee2e6; }
                    .prism-debug-root .status-badge { display: inline-block; padding: 0.3rem 0.8rem; border-radius: 20px; font-size: 0.75rem; font-weight: 700; text-transform: uppercase; }
                    .prism-debug-root .status-online { background: #e6fcf5; color: #0ca678; }
                    .prism-debug-root .btn { display: inline-block; padding: 0.6rem 1.2rem; border-radius: 6px; text-decoration: none; color: white; font-weight: 600; font-size: 0.9rem; }
                    .prism-debug-root .btn-login { background: #007bff; }
                    .prism-debug-root code { background: #f8f9fa; padding: 0.2rem 0.4rem; border-radius: 4px; border: 1px solid #dee2e6; font-size: 0.85rem; color: #d63384; }
                </style>
                <script>
                    function copyToPrismClipboard(btn, elementId) {
                        const text = document.getElementById(elementId).innerText;
                        navigator.clipboard.writeText(text).then(() => {
                            const original = btn.innerText;
                            btn.innerText = 'Copied!';
                            setTimeout(() => btn.innerText = original, 2000);
                        });
                    }
                </script>
                <h1>Umbraco Prism Runtime</h1>
                """);

            // 1. Tenant Section
            sb.Append($"""
                <div class="card">
                    <h2>📡 Tenant Info <button class="copy-btn" onclick="copyToPrismClipboard(this, 'prism-tenant-data')">Copy</button></h2>
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
                sb.Append($"""
                    <div class="card">
                        <h2>👤 Identity <button class="copy-btn" onclick="copyToPrismClipboard(this, 'prism-user-data')">Copy</button></h2>
                        <div id="prism-user-data">
                            <p><strong>User:</strong> {prismUser.Name}</p>
                            <p><strong>Email:</strong> {prismUser.Email}</p>
                            <p><strong>TID:</strong> <code>{prismUser.EntraTenantId}</code></p>
                        </div>
                        <a href="/auth/logout" class="btn" style="background:#495057; margin-top:10px;">Sign Out</a>
                    </div>
                    """);

                // 2.5 Claims Section
                sb.Append($"""
                    <div class="card">
                        <h2>Attributes & Claims <button class="copy-btn" onclick="copyToPrismClipboard(this, 'prism-claims-data')">Copy</button></h2>
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
                    <h2>HttpContext Debug <button class="copy-btn" onclick="copyToPrismClipboard(this, 'prism-cache-debug')">Copy</button></h2>
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
                        <li><strong>Active Schemes:</strong> {schemesHtml}</li>
                        <li><strong>Request Path:</strong> <code>{path}</code></li>
                        <li><strong>Scheme Authority:</strong> <code>{oidcOptions.Authority}</code></li>
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