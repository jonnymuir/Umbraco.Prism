namespace UmbracoPrism.Core.Configuration;

/// <summary>
/// Controls the security response headers added by <see cref="UmbracoPrism.Core.Middleware.PrismSecurityHeadersMiddleware"/>.
/// Registered via <c>IOptions&lt;PrismSecurityHeadersOptions&gt;</c>; configure under <c>Prism:SecurityHeaders</c>.
///
/// SEC-PT2-004: security headers are applied automatically by PrismComposer.
/// Consumers can disable or tune per-environment via configuration.
/// </summary>
public class PrismSecurityHeadersOptions
{
    public const string SectionName = "Prism:SecurityHeaders";

    /// <summary>
    /// Set to false to disable all Prism security headers (e.g. for environments
    /// where a reverse proxy or WAF already supplies them).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true (default), the security headers middleware skips requests whose
    /// path starts with /umbraco/ to avoid interfering with the backoffice pipeline.
    /// </summary>
    public bool ExcludeBackoffice { get; set; } = true;

    /// <summary>
    /// Value for the <c>X-Frame-Options</c> header. Set to null to omit.
    /// Default: <c>SAMEORIGIN</c> (safer than DENY for Umbraco, which may use
    /// same-origin iframes in the backoffice media/content pickers).
    /// </summary>
    public string? FrameOptions { get; set; } = "SAMEORIGIN";

    /// <summary>
    /// Value for <c>X-Content-Type-Options</c>. Set to null to omit.
    /// Default: <c>nosniff</c> — prevents MIME-sniffing XSS amplification.
    /// </summary>
    public string? ContentTypeOptions { get; set; } = "nosniff";

    /// <summary>
    /// Value for <c>Referrer-Policy</c>. Set to null to omit.
    /// Default: <c>strict-origin-when-cross-origin</c>.
    /// </summary>
    public string? ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Value for <c>Strict-Transport-Security</c> (HTTPS only). Set to null to omit.
    /// Default: <c>max-age=31536000; includeSubDomains</c> (1 year).
    /// Only emitted on HTTPS requests.
    /// </summary>
    public string? HstsValue { get; set; } = "max-age=31536000; includeSubDomains";

    /// <summary>
    /// Value for <c>Permissions-Policy</c>. Set to null to omit.
    /// Default: restrictive — disables camera, microphone, geolocation, payment, USB.
    /// </summary>
    public string? PermissionsPolicy { get; set; } =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    /// <summary>
    /// Value for <c>Content-Security-Policy-Report-Only</c>. Set to null to omit.
    ///
    /// CSP ships as Report-Only by default because a strict enforced CSP requires
    /// careful tuning for: (a) Umbraco backoffice inline scripts/styles, (b) GOV.UK
    /// Frontend inline event attributes, (c) TestSite inline <script> blocks.
    /// Tune this per-deployment and promote to <c>Content-Security-Policy</c>
    /// once you are confident it does not break any page.
    ///
    /// Default starter policy: self + unsafe-inline (deliberately permissive —
    /// tighten before enforcing). See SEC-PT2-004 follow-up.
    /// </summary>
    public string? ContentSecurityPolicyReportOnly { get; set; } =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; frame-ancestors 'self'";

    /// <summary>
    /// Sources a host can append to a specific CSP directive, keyed by directive name (e.g.
    /// <c>connect-src</c>), without having to fork and hand-maintain Prism's whole policy string
    /// just to add one third-party origin (an analytics endpoint, a payment provider's iframe,
    /// their own API). Value is one or more space-separated sources, e.g.
    /// <c>{ ["connect-src"] = "https://api.example.com" }</c>. A directive Prism doesn't already
    /// emit is added as a new one. Applied to whichever CSP header(s) are active.
    /// </summary>
    public Dictionary<string, string> AdditionalContentSecurityPolicySources { get; set; } = new();
}
