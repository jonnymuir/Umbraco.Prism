using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Builds downloadable mobile-shell starter bundles for a configured tenant.
/// </summary>
public interface IMobileBundleService
{
    /// <summary>
    /// Generates a ZIP bundle containing Capacitor shell files and tenant-specific settings.
    /// </summary>
    /// <param name="tenant">Tenant configuration used for host and identity defaults.</param>
    /// <param name="request">User-selected mobile bundle customization options.</param>
    /// <param name="cancellationToken">Cancellation token for bundle generation.</param>
    /// <returns>A byte array representing the generated ZIP archive.</returns>
    Task<byte[]> BuildBundleAsync(PrismTenantSchema tenant, PrismMobileBundleRequest request, CancellationToken cancellationToken = default);
}
