using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

public interface IMobileBundleService
{
    Task<byte[]> BuildBundleAsync(PrismTenantSchema tenant, PrismMobileBundleRequest request, CancellationToken cancellationToken = default);
}
