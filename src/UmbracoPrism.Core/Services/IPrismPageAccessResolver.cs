using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Resolves whether a content node requires sign-in and/or is available to a given tenant. The
/// single source of truth every consumer (the enforcement filter, site navigation, ...) reads
/// from, instead of each deciding access independently.
/// </summary>
public interface IPrismPageAccessResolver
{
    /// <summary>
    /// Resolves the access state for a content node against one request's tenant/authentication
    /// state. A node with no configured policy resolves to <see cref="PrismPageAccessResult.Public"/>.
    /// </summary>
    PrismPageAccessResult Resolve(Guid contentKey, PrismTenant? tenant, bool isAuthenticated);

    /// <summary>
    /// Invalidates the cached policy set. Call after any create/update/delete against
    /// prismPageAccessPolicies so the next request sees the change immediately.
    /// </summary>
    void Invalidate(string reason = "unspecified");
}
