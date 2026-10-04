using System.IO.Compression;
using System.Text;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services.MobileBundles;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Produces Prism mobile starter bundles with tenant-specific runtime and identity configuration.
/// The content of each file lives in <c>MobileBundles/</c>, grouped by what it is for.
/// </summary>
public class MobileBundleService : IMobileBundleService
{
    /// <summary>
    /// Builds a ZIP archive containing a Capacitor app scaffold for a tenant.
    /// </summary>
    /// <param name="tenant">Tenant record used to derive default host and Entra settings.</param>
    /// <param name="request">Bundle generation options provided from the backoffice workflow.</param>
    /// <param name="cancellationToken">Cancellation token for bundle generation.</param>
    /// <returns>ZIP archive bytes for download.</returns>
    /// <exception cref="ArgumentException">Thrown when request input contains invalid app identifiers or URLs.</exception>
    public Task<byte[]> BuildBundleAsync(PrismTenantSchema tenant, PrismMobileBundleRequest request, CancellationToken cancellationToken = default)
    {
        var settings = MobileBundleSettings.Resolve(tenant, request);

        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "README.md", BundleDocumentation.BuildReadme(settings));
            AddEntry(archive, "package.json", CapacitorProjectFiles.BuildPackageJson(settings));
            AddEntry(archive, "AGENT_PROMPT.md", BundleDocumentation.BuildAgentPrompt(settings));
            AddEntry(archive, "capacitor.config.ts", CapacitorProjectFiles.BuildCapacitorConfig(settings));
            AddEntry(archive, ".gitignore", "node_modules\nandroid\nios\n.DS_Store\n");
            AddEntry(archive, "www/index.html", WebShellFiles.BuildPlaceholderIndex(settings));
            AddEntry(archive, "www/mobile-overrides.css", WebShellFiles.BuildMobileOverrideTemplate());
            AddEntry(archive, "scripts/doctor-mobile.sh", CapacitorProjectFiles.BuildDoctorScript(settings));
            AddEntry(archive, "scripts/bootstrap-ios.sh", IosProjectFiles.BuildBootstrapIosScript(settings));
            AddEntry(archive, "scripts/bootstrap-android.sh", AndroidProjectFiles.BuildBootstrapAndroidScript(settings));
            AddEntry(archive, "scripts/trust-ios-localhost-cert.sh", IosProjectFiles.BuildTrustIosLocalhostCertScript(settings));
            AddEntry(archive, "resources/mobile-assets.json", CapacitorProjectFiles.BuildAssetsManifest(settings));

            // Default app icon (an opaque-background Umbraco Prism mark — @capacitor/assets
            // requires no alpha channel for the iOS App Store icon specifically). Without this,
            // every generated app ships Capacitor's own generic default icon, found live on the
            // first real TestFlight build. A tenant/implementer overrides it by replacing this
            // file (any square, opaque-background source @capacitor/assets accepts — PNG or SVG)
            // before running the bootstrap script; the generate step re-runs against whatever's
            // there.
            AddEntry(archive, "resources/icon.svg", WebShellFiles.BuildDefaultAppIconSvg());

            if (settings.BiometricAuthEnabled)
            {
                AddEntry(archive, "resources/ios-info-plist-additions.xml", IosProjectFiles.BuildIosInfoPlistAdditions(settings));
                AddEntry(archive, "resources/android-manifest-additions.xml", AndroidProjectFiles.BuildAndroidManifestAdditions());
            }
        }

        return Task.FromResult(memory.ToArray());
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
