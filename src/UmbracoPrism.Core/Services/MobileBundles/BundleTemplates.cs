using System.Reflection;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// Reads the large fixed pieces of the bundle (Swift, Java, shell, JS, CSS, markdown) from the
/// <c>Templates/</c> files embedded in this assembly, so they are real files an editor can highlight
/// and a linter can read, instead of string literals buried in C#. <c>Templates/bundle/</c> mirrors
/// the bundle itself: each file there is written to the same path in it (see
/// <see cref="NativeShellFiles"/>). Line endings are normalised to LF so a Windows checkout
/// cannot leak CRLF into the generated bash scripts.
/// </summary>
internal static class BundleTemplates
{
    private const string ResourcePrefix = "MobileBundleTemplates/";

    /// <summary>The files under a folder of <c>Templates/</c>, such as <c>bundle/</c>, by their template name.</summary>
    public static IEnumerable<string> FilesIn(string folder)
    {
        var folderPrefix = ResourcePrefix + folder;
        return typeof(BundleTemplates).Assembly.GetManifestResourceNames()
            .Where(name => name.Replace('\\', '/').StartsWith(folderPrefix, StringComparison.Ordinal))
            .Select(name => name.Replace('\\', '/')[ResourcePrefix.Length..])
            .Order(StringComparer.Ordinal);
    }

    public static string Read(string name)
    {
        var assembly = typeof(BundleTemplates).Assembly;
        using var stream = (assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? assembly.GetManifestResourceStream(ResourcePrefix + name.Replace('/', '\\')))
            ?? throw new InvalidOperationException($"Mobile bundle template '{name}' is not embedded in {typeof(BundleTemplates).Assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
