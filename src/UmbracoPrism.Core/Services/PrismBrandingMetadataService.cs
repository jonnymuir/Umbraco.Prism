using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using UmbracoPrism.Core.Models.Branding;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for parsing CSS branding files and extracting variable metadata.
/// </summary>
public class PrismBrandingMetadataService : IPrismBrandingMetadataService
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IMemoryCache _memoryCache;
    private const string CacheKey = "PrismBrandingMetadata";

    public PrismBrandingMetadataService(IWebHostEnvironment webHostEnvironment, IMemoryCache memoryCache)
    {
        _webHostEnvironment = webHostEnvironment;
        _memoryCache = memoryCache;
    }

    public IEnumerable<BrandingSection> GetBrandingMetadata()
    {
        return _memoryCache.GetOrCreate(CacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return ParseBrandingFiles();
        }) ?? Enumerable.Empty<BrandingSection>();
    }

    private List<BrandingSection> ParseBrandingFiles()
    {
        var brandingPath = Path.Combine(_webHostEnvironment.WebRootPath, "branding");
        
        if (!Directory.Exists(brandingPath))
        {
            return new List<BrandingSection>();
        }

        var cssFiles = Directory.GetFiles(brandingPath, "*.css")
            .Where(f => !Path.GetFileName(f).Equals("prism-branding.css", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allVariables = new List<(BrandingVariableMetadata metadata, int order)>();
        var sectionOrder = new Dictionary<string, int>();
        var globalOrder = 0;

        foreach (var file in cssFiles)
        {
            var content = File.ReadAllText(file);
            var variables = ParseCssFile(content);
            
            foreach (var variable in variables)
            {
                var section = variable.metadata.Description?.Contains("section:") == true 
                    ? ExtractMetadataValue(variable.metadata.Description, "section") 
                    : "General";
                
                if (!sectionOrder.ContainsKey(section))
                {
                    sectionOrder[section] = globalOrder++;
                }
                
                allVariables.Add((variable.metadata, sectionOrder[section]));
            }
        }

        // Group by section and maintain order
        var sections = allVariables
            .GroupBy(v => {
                var desc = v.metadata.Description;
                return desc?.Contains("section:") == true 
                    ? ExtractMetadataValue(desc, "section") 
                    : "General";
            })
            .OrderBy(g => sectionOrder.GetValueOrDefault(g.Key, int.MaxValue))
            .Select(g => new BrandingSection
            {
                Name = g.Key,
                Variables = g.Select(x => x.metadata).ToList()
            })
            .ToList();

        return sections;
    }

    private List<(BrandingVariableMetadata metadata, string section)> ParseCssFile(string content)
    {
        var variables = new List<(BrandingVariableMetadata metadata, string section)>();
        
        // Extract @property declarations
        var propertyDeclarations = ExtractPropertyDeclarations(content);
        
        // Extract variable declarations with preceding comments
        var variablePattern = @"/\*\s*@prism\s+([^*]+)\*/\s*\n\s*(--[\w-]+)\s*:\s*([^;]+);";
        var matches = Regex.Matches(content, variablePattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var prismAnnotation = match.Groups[1].Value.Trim();
            var variableName = match.Groups[2].Value.Trim();
            var variableValue = match.Groups[3].Value.Trim();

            var metadata = new BrandingVariableMetadata
            {
                Variable = variableName,
                CurrentValue = variableValue
            };

            // Parse @prism annotation
            ParsePrismAnnotation(prismAnnotation, metadata);

            // Get @property syntax if available
            if (propertyDeclarations.TryGetValue(variableName, out var syntax))
            {
                metadata.Syntax = syntax;
                
                // Infer type from syntax if not explicitly set
                if (string.IsNullOrEmpty(metadata.Type) || metadata.Type == "text")
                {
                    metadata.Type = InferTypeFromSyntax(syntax);
                }
            }

            var section = ExtractMetadataValue(prismAnnotation, "section");
            variables.Add((metadata, section));
        }

        return variables;
    }

    private Dictionary<string, string> ExtractPropertyDeclarations(string content)
    {
        var declarations = new Dictionary<string, string>();
        var propertyPattern = @"@property\s+(--[\w-]+)\s*\{[^}]*syntax:\s*['""]([^'""]+)['""]";
        var matches = Regex.Matches(content, propertyPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value.Trim();
            var syntax = match.Groups[2].Value.Trim();
            declarations[variableName] = syntax;
        }

        return declarations;
    }

    private void ParsePrismAnnotation(string annotation, BrandingVariableMetadata metadata)
    {
        // Split by pipe and parse key:value pairs
        var parts = annotation.Split('|', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var colonIndex = part.IndexOf(':');
            if (colonIndex == -1) continue;

            var key = part.Substring(0, colonIndex).Trim();
            var value = part.Substring(colonIndex + 1).Trim();

            switch (key.ToLowerInvariant())
            {
                case "section":
                    // Section is handled separately, but store in description for reference
                    metadata.Description = part;
                    break;
                case "label":
                    metadata.Label = value;
                    break;
                case "description":
                    metadata.Description = metadata.Description == null 
                        ? part 
                        : metadata.Description + " | " + part;
                    break;
                case "type":
                    metadata.Type = value.ToLowerInvariant();
                    break;
            }
        }

        // Store full annotation in description if not already set
        if (string.IsNullOrEmpty(metadata.Description))
        {
            metadata.Description = annotation;
        }
    }

    private string ExtractMetadataValue(string annotation, string key)
    {
        var pattern = $@"{key}:\s*([^|]+)";
        var match = Regex.Match(annotation, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : (key == "section" ? "General" : string.Empty);
    }

    private string InferTypeFromSyntax(string syntax)
    {
        return syntax.ToLowerInvariant() switch
        {
            "<color>" => "color",
            "<url>" => "url",
            "<image>" => "image",
            "<length>" => "length",
            "<percentage>" => "length",
            "*" => "text",
            "<string>" => "text",
            _ => "text"
        };
    }
}
