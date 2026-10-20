namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// The files under <c>Templates/bundle/</c>: the native tab shell for each platform and the
/// scripts that add it to the generated projects. Each is written to the same path in the bundle,
/// with the app's own values filled in.
/// </summary>
internal static class NativeShellFiles
{
    private const string TemplateFolder = "bundle/";

    /// <summary>Each file's path in the bundle and its content for this app.</summary>
    internal static IEnumerable<(string Path, string Content)> Build(MobileBundleSettings settings)
    {
        var startUrl = CapacitorProjectFiles.AddPrismMobileQueryFlag(settings.StartUrl);
        var host = new Uri(settings.StartUrl).Host;

        return BundleTemplates.BundleFiles().Select(template => (
            template[TemplateFolder.Length..],
            BundleTemplates.Read(template)
                .Replace("@@PRISM_PACKAGE@@", settings.AppId)
                .Replace("@@PRISM_START_URL@@", startUrl)
                .Replace("@@PRISM_HOST@@", host)));
    }
}
