namespace Wayfinder.Umbraco;

/// <summary>
/// Named authorization policies Wayfinder.Umbraco's own controllers reference by name, rather
/// than hardcoding an authentication scheme — a host registers each policy to mean whatever
/// scheme (or requirement) makes sense for it. Keeps this package free of any opinion about
/// how a host authenticates its members.
/// </summary>
public static class WayfinderUmbracoAuthorizationPolicies
{
    /// <summary>
    /// Required to call <see cref="Controllers.ServiceRequestPollController"/>'s polling
    /// endpoint. A host must register this policy (e.g. requiring its own member cookie
    /// scheme) — see the host's own composition for how Prism's "PrismMemberCookie" scheme
    /// is wired to it.
    /// </summary>
    public const string ServiceRequestPolling = "Wayfinder:ServiceRequestPolling";
}
