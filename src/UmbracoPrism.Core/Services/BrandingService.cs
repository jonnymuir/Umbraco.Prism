using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models.Branding;

namespace UmbracoPrism.Core.Services;

public class BrandingService : IBrandingService
{
    private const string CacheKey = "Prism_BrandingTabs";
    private const string OtherTabLabel = "Other Styles";

    private static readonly Regex CommentRegex = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);
    private static readonly Regex VariableRegex = new(@"(?<name>--[A-Za-z0-9\-_]+)\s*:\s*(?<value>[^;]+);", RegexOptions.Compiled);

    private readonly IWebHostEnvironment _environment;
    private readonly IAppPolicyCache _runtimeCache;

    public BrandingService(IWebHostEnvironment environment, AppCaches appCaches)
    {
        _environment = environment;
        _runtimeCache = appCaches.RuntimeCache;
    }

    public IReadOnlyList<PrismBrandingTab> GetBrandingTabs()
    {
        return _runtimeCache.GetCacheItem(CacheKey, () =>
        {
            var tabs = ScanForBrandingTabs();
            return tabs;
        }, TimeSpan.FromMinutes(10)) ?? new List<PrismBrandingTab>();
    }

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
        var contentRoot = _environment.ContentRootPath;
        var cssFiles = Directory.EnumerateFiles(contentRoot, "*.css", SearchOption.AllDirectories)
            .Where(path => !IsExcludedPath(path))
            .ToList();

        var tabs = new List<PrismBrandingTab>();

        foreach (var file in cssFiles)
        {
            var content = File.ReadAllText(file);
            content = CommentRegex.Replace(content, string.Empty);

            var matches = VariableRegex.Matches(content);
            if (matches.Count == 0) continue;

            var variables = new List<PrismBrandingVariable>();
            var seen = new HashSet<string>();

            foreach (Match match in matches)
            {
                var name = match.Groups["name"].Value.Trim();
                if (!seen.Add(name)) continue;

                var value = match.Groups["value"].Value.Trim();
                variables.Add(new PrismBrandingVariable
                {
                    Name = name,
                    DefaultValue = value
                });
            }

            if (variables.Count == 0) continue;

            tabs.Add(new PrismBrandingTab
            {
                Label = Path.GetFileName(file),
                Variables = variables
            });
        }

        return tabs;
    }

    private static bool IsExcludedPath(string path)
    {
        var normalized = path.Replace(Path.DirectorySeparatorChar, '/');
        return normalized.InvariantContains("/node_modules/")
               || normalized.InvariantContains("/bin/")
               || normalized.InvariantContains("/obj/")
               || normalized.InvariantContains("/.git/")
               || normalized.InvariantContains("/artifacts/")
               || normalized.InvariantContains("/wwwroot/umbraco/");
    }
}
