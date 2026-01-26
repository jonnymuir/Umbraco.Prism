using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using System.Text;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

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
                    .prism-debug-root { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; line-height: 1.6; padding: 2rem; background: #f0f2f5; color: #1c1e21; border-radius: 12px; margin: 20px 0; border: 1px solid #ddd; }
                    .prism-debug-root .card { background: white; padding: 1.5rem; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.05); margin-bottom: 1.5rem; border: 1px solid #e1e4e8; }
                    .prism-debug-root h1 { color: #007bff; margin-top: 0; font-size: 1.5rem; }
                    .prism-debug-root h2 { font-size: 1.1rem; margin-top: 0; border-bottom: 1px solid #eee; padding-bottom: 0.5rem; }
                    .prism-debug-root .status-badge { display: inline-block; padding: 0.3rem 0.8rem; border-radius: 20px; font-size: 0.75rem; font-weight: 700; text-transform: uppercase; }
                    .prism-debug-root .status-online { background: #e6fcf5; color: #0ca678; }
                    .prism-debug-root .status-offline { background: #fff5f5; color: #fa5252; }
                    .prism-debug-root .btn { display: inline-block; padding: 0.6rem 1.2rem; border-radius: 6px; text-decoration: none; color: white; font-weight: 600; }
                    .prism-debug-root .btn-login { background: #007bff; }
                    .prism-debug-root .btn-logout { background: #495057; }
                    .prism-debug-root code { background: #f8f9fa; padding: 0.2rem 0.4rem; border-radius: 4px; border: 1px solid #dee2e6; font-size: 0.9rem; color: #d63384; word-break: break-all; }
                    .prism-debug-root ul { list-style: none; padding: 0; }
                    .prism-debug-root .alert { padding: 1rem; border-radius: 8px; margin-top: 1rem; font-size: 0.9rem; background: #e7f5ff; border-left: 4px solid #339af0; color: #1971c2; }
                </style>
                <h1>Umbraco Prism Runtime</h1>
                """);

            // 1. Tenant Section
            sb.Append("<div class=\"card\"><h2>📡 Tenant Information</h2>");
            if (tenant != null)
            {
                sb.Append($"""
                    <p><strong>Resolved Name:</strong> {tenant.Name}</p>
                    <ul>
                        <li><strong>Database ID:</strong> <code>{tenant.Id}</code></li>
                        <li><strong>Hostname:</strong> <code>{host}</code></li>
                        <li><strong>Entra ID:</strong> <code>{(isTenantConfigured ? tenant.EntraTenantId : "Not Set")}</code></li>
                    </ul>
                    <span class="status-badge status-online">Tenant Resolved</span>
                    """);
            }
            else
            {
                sb.Append("""
                    <span class="status-badge status-offline">Tenant Not Resolved</span>
                    <p>Prism could not resolve a tenant for this domain. Check your <code>PrismTenants</code> table.</p>
                    """);
            }
            sb.Append("</div>");

            // 2. Identity Section
            sb.Append("<div class=\"card\"><h2>👤 Identity Status</h2>");
            if (prismUser.IsAuthenticated)
            {
                sb.Append($"""
                    <p><strong>Logged in as:</strong> {prismUser.Name} ({prismUser.Email})</p>
                    <p><strong>Entra Tenant (TID):</strong> <code>{prismUser.EntraTenantId}</code></p>
                    <div style="margin-top: 1.5rem;"><a href="/auth/logout" class="btn btn-logout">Sign Out</a></div>
                    """);
            }
            else
            {
                sb.Append("<p>You are currently browsing as a <strong>Guest</strong>.</p>");
                if (isPrismAuthGlobalEnabled && isTenantConfigured)
                {
                    sb.Append("<a href=\"/auth/login\" class=\"btn btn-login\">Sign In with Entra ID</a>");
                }
                else
                {
                    var reason = isPrismAuthGlobalEnabled ? "this specific tenant has no Entra ID configured." : "Prism:VaultUri is missing from appsettings.json.";
                    sb.Append($"""
                        <button class="btn" style="background: #ced4da; cursor: not-allowed;" disabled>Sign In Disabled</button>
                        <div class="alert"><strong>Auth Note:</strong> Sign-in is disabled because {reason}</div>
                        """);
                }
            }
            sb.Append("</div>");

            // 3. System Diagnostics
            var authMode = isPrismAuthGlobalEnabled ? "<b style=\"color:#0ca678;\">ACTIVE</b>" : "<b style=\"color:#f08c00;\">PASSIVE</b>";
            var schemesHtml = string.Join(" ", allSchemes.Select(s => $"<code>{s.Name}</code>"));

            sb.Append($"""
                <div class="card" style="font-size: 0.85rem; color: #495057;">
                    <h2 style="font-size: 1rem;">🛠 System Diagnostics</h2>
                    <ul>
                        <li><strong>Vault URI:</strong> {(isPrismAuthGlobalEnabled ? vaultUri : "❌ Not Configured")}</li>
                        <li><strong>Prism Auth Mode:</strong> {authMode} <em>(Login flow control)</em></li>
                        <li><strong>Active Schemes:</strong> {schemesHtml}</li>
                        <li><strong>Request Path:</strong> <code>{path}</code></li>
                    </ul>
                </div>
                """);
        }
        catch (Exception ex)
        {
            sb.Append($"<div class='card' style='border:2px solid red;'><h2>Critical Error</h2><pre>{ex.Message}</pre></div>");
        }

        output.Content.SetHtmlContent(sb.ToString());
    }
}