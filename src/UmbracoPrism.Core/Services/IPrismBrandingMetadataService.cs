using UmbracoPrism.Core.Models.Branding;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for extracting metadata from CSS branding files.
/// </summary>
public interface IPrismBrandingMetadataService
{
    /// <summary>
    /// Gets branding metadata from all CSS files in the branding directory.
    /// </summary>
    /// <returns>Collection of branding sections with variable metadata.</returns>
    IEnumerable<BrandingSection> GetBrandingMetadata();
}
