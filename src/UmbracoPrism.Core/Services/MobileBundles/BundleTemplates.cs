using System.Reflection;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>
/// Reads the large fixed pieces of the bundle (Swift, shell, JS, CSS, markdown) from the
/// <c>Templates/</c> files embedded in this assembly, so they are real files an editor can highlight
/// instead of string literals buried in C#. Line endings are normalised to LF so a Windows checkout
/// cannot leak CRLF into the generated bash scripts.
/// </summary>
internal static class BundleTemplates
{
    private const string ResourcePrefix = "MobileBundleTemplates/";

    public static string Read(string name)
    {
        using var stream = typeof(BundleTemplates).Assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"Mobile bundle template '{name}' is not embedded in {typeof(BundleTemplates).Assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
