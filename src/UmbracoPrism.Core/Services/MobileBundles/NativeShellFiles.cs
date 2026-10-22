namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// The files under <c>Templates/bundle/</c>: the native tab shell for each platform and the
/// scripts that add it to the generated projects. Each is written to the same path in the bundle,
/// with the app's own values filled in. An app built with journey statuses also gets the files
/// under <c>Templates/journey-status/</c>, the same way.
/// </summary>
internal static class NativeShellFiles
{
    /// <summary>Each file's path in the bundle and its content for this app.</summary>
    internal static IEnumerable<(string Path, string Content)> Build(MobileBundleSettings settings)
    {
        var files = FilesIn("bundle/", settings);
        return settings.JourneyStatusEnabled ? files.Concat(FilesIn("journey-status/", settings)) : files;
    }

    private static IEnumerable<(string Path, string Content)> FilesIn(string folder, MobileBundleSettings settings)
    {
        var startUrl = CapacitorProjectFiles.AddPrismMobileQueryFlag(settings.StartUrl);
        var host = new Uri(settings.StartUrl).Host;

        return BundleTemplates.FilesIn(folder).Select(template => (
            template[folder.Length..],
            BundleTemplates.Read(template)
                .Replace("@@PRISM_PACKAGE@@", settings.AppId)
                .Replace("@@PRISM_START_URL@@", startUrl)
                .Replace("@@PRISM_HOST@@", host)));
    }
}
