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
    /// Value for the enforced <c>Content-Security-Policy</c> header. Set to null to omit.
    ///
    /// Default: <c>self</c> for script-src/style-src — no <c>unsafe-inline</c>. This is safe to
    /// enforce because every page Prism itself renders (branding, mobile-shell, biometric, the
    /// TestSite reference host's own demo pages) serves its CSS/JS as real external resources,
    /// never spliced inline — CSP's inline restrictions only ever apply to literal inline
    /// <c>&lt;style&gt;</c>/<c>&lt;script&gt;</c> content and inline event-handler attributes
    /// (<c>onclick="..."</c>), never to externally-referenced <c>&lt;link href&gt;</c>/
    /// <c>&lt;script src&gt;</c>. A host adding its own inline content, or a third-party embed
    /// that needs one, should externalize it the same way (or add a source via
    /// <see cref="AdditionalContentSecurityPolicySources"/>/its own override) rather than
    /// reintroducing <c>unsafe-inline</c>. Backoffice paths are excluded from this middleware
    /// entirely by default (<see cref="ExcludeBackoffice"/>) — this policy governs the front end.
    ///
    /// Also sets <c>object-src</c>, <c>base-uri</c>, and <c>form-action</c> explicitly — found
    /// live (ZAP baseline, once CSP went from Report-Only to enforced): these directives don't
    /// fall back to <c>default-src</c> at all per spec, unlike most others, so leaving them unset
    /// is a real gap (rule 10055, "Failure to Define Directive with No Fallback"), not just a
    /// linter nit — a page could still load a plugin/applet via
    /// <c>&lt;object&gt;</c>/<c>&lt;embed&gt;</c>, have its relative-URL resolution hijacked via
    /// an injected <c>&lt;base&gt;</c> tag, or have an injected form submit credentials to an
    /// attacker-controlled origin, even with every other directive locked down.
    ///
    /// <c>img-src</c> deliberately omits a scheme-wildcard <c>https:</c> — nothing in Prism or
    /// the TestSite reference host renders a third-party HTTPS image (media comes from Umbraco's
    /// own, same-origin media library), so a blanket allow-any-https-host source was an
    /// unjustified wildcard (ZAP rule 10055, "CSP: Wildcard Directive"), not a real requirement.
    /// A host that genuinely needs one (a third-party avatar/CDN) should add it via
    /// <see cref="AdditionalContentSecurityPolicySources"/> rather than this default reintroducing
    /// it for everyone.
    /// </summary>
    public string? ContentSecurityPolicy { get; set; } =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";

    /// <summary>
    /// Value for the monitoring-only <c>Content-Security-Policy-Report-Only</c> header. Set to
    /// null (the default) to omit it entirely. A host tightening or extending its own policy
    /// beyond Prism's default can set this to a stricter draft policy to see what it would break
    /// in the browser console before promoting it to <see cref="ContentSecurityPolicy"/> — both
    /// headers can be active at once if useful, they're independent.
    /// </summary>
    public string? ContentSecurityPolicyReportOnly { get; set; }

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
