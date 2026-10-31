using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models.Branding;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Discovers Prism branding CSS variables and projects tenant-specific overrides for editor workflows.
/// </summary>
public class BrandingService : IBrandingService
{
    private const string CacheKey = "Prism_BrandingTabs";
    private const string OtherTabLabel = "Other Styles";
    private const string PackagedBrandingPrefix = "PrismBranding/";

    private static readonly Regex CommentRegex = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);
    private static readonly Regex VariableRegex = new(@"(?<name>--[A-Za-z0-9\-_]+)\s*:\s*(?<value>[^;]+);", RegexOptions.Compiled);

    private readonly IWebHostEnvironment _environment;
    private readonly IAppPolicyCache _runtimeCache;

    /// <summary>
    /// Initializes the branding service with content-root and runtime cache dependencies.
    /// </summary>
    /// <param name="environment">Host environment used to locate CSS assets under the content root.</param>
    /// <param name="appCaches">Application cache container used to cache parsed branding tabs.</param>
    public BrandingService(IWebHostEnvironment environment, AppCaches appCaches)
    {
        _environment = environment;
        _runtimeCache = appCaches.RuntimeCache;
    }

    /// <summary>
    /// Gets discovered branding tabs and CSS variable defaults.
    /// </summary>
    /// <returns>A read-only list of parsed branding tabs.</returns>
    public IReadOnlyList<PrismBrandingTab> GetBrandingTabs()
    {
        return _runtimeCache.GetCacheItem(CacheKey, () =>
        {
            var tabs = ScanForBrandingTabs();
            return tabs;
        }, TimeSpan.FromMinutes(10)) ?? new List<PrismBrandingTab>();
    }

    /// <summary>
    /// Gets branding tabs with tenant-provided override values applied.
    /// </summary>
    /// <param name="overrides">Override values keyed by CSS variable name.</param>
    /// <returns>A read-only list containing known tabs and an "Other Styles" tab for unknown overrides.</returns>
    public IReadOnlyList<PrismBrandingTab> GetBrandingTabsWithOverrides(IReadOnlyDictionary<string, string> overrides)
    {
        var tabs = GetBrandingTabs().Select(tab => new PrismBrandingTab
        {
            Label = tab.Label,
            Variables = tab.Variables.Select(variable => new PrismBrandingVariable
            {
                Name = variable.Name,
                DefaultValue = variable.DefaultValue,
                OverrideValue = overrides.TryGetValue(variable.Name, out var value) ? value : null
            }).ToList()
        }).ToList();

        var knownVariables = new HashSet<string>(tabs.SelectMany(tab => tab.Variables.Select(v => v.Name)));
        var otherOverrides = overrides
            .Where(kvp => !knownVariables.Contains(kvp.Key))
            .Select(kvp => new PrismBrandingVariable
            {
                Name = kvp.Key,
                OverrideValue = kvp.Value
            })
            .ToList();

        if (otherOverrides.Any())
        {
            tabs.Add(new PrismBrandingTab
            {
                Label = OtherTabLabel,
                Variables = otherOverrides
            });
        }

        return tabs;
    }

    private List<PrismBrandingTab> ScanForBrandingTabs()
    {
        var hostFiles = Directory.EnumerateFiles(_environment.ContentRootPath, "*.css", SearchOption.AllDirectories)
            .Where(path => !IsExcludedPath(path))
            .ToList();

        // The starter design system ships in this assembly. A host file with the same name replaces it.
        var hostNames = hostFiles.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var packaged = PackagedBrandingFiles()
            .Where(file => !hostNames.Contains(file.Name))
            .Select(file => ParseTab(file.Name, file.Content));

        return packaged
            .Concat(hostFiles.Select(path => ParseTab(Path.GetFileName(path), File.ReadAllText(path))))
            .OfType<PrismBrandingTab>()
            .ToList();
    }

    private static IEnumerable<(string Name, string Content)> PackagedBrandingFiles()
    {
        var assembly = typeof(BrandingService).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(PackagedBrandingPrefix, StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            yield return (resource[PackagedBrandingPrefix.Length..], reader.ReadToEnd());
        }
    }

    private static PrismBrandingTab? ParseTab(string label, string content)
    {
        content = CommentRegex.Replace(content, string.Empty);

        var variables = new List<PrismBrandingVariable>();
        var seen = new HashSet<string>();

        foreach (Match match in VariableRegex.Matches(content))
        {
            var name = match.Groups["name"].Value.Trim();
            if (!seen.Add(name)) continue;

            variables.Add(new PrismBrandingVariable
            {
                Name = name,
                DefaultValue = match.Groups["value"].Value.Trim()
            });
        }

        return variables.Count == 0 ? null : new PrismBrandingTab { Label = label, Variables = variables };
    }

    private static bool IsExcludedPath(string path)
    {
        var normalized = path.Replace(Path.DirectorySeparatorChar, '/');
        return normalized.InvariantContains("/node_modules/")
               || normalized.InvariantContains("/bin/")
               || normalized.InvariantContains("/obj/")
               || normalized.InvariantContains("/.git/")
               || normalized.InvariantContains("/artifacts/")
               || normalized.InvariantContains("/wwwroot/umbraco/")
               // A published site's copy of the packaged design system, which is read from the assembly instead.
               || normalized.InvariantContains("/App_Plugins/UmbracoPrism/branding/");
    }
}
