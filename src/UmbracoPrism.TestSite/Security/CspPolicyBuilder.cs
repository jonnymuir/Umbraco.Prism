using System.Text;

namespace UmbracoPrism.TestSite.Security;

/// <summary>
/// Merges additional CSP sources (<see cref="SecurityHeadersOptions
/// .AdditionalContentSecurityPolicySources"/>, and Prism's sign-in sources) into the base policy string, so a host
/// that needs e.g. an extra <c>connect-src</c> for a third-party API doesn't have to fork and
/// hand-maintain the whole policy — only the one directive it actually needs to extend.
/// </summary>
public static class CspPolicyBuilder
{
    public static string WithAdditionalSources(string basePolicy, IReadOnlyDictionary<string, string> additionalSources)
    {
        if (additionalSources.Count == 0)
        {
            return basePolicy;
        }

        // A CSP header value is `;`-separated directives, each `directive-name source1 source2 ...`.
        var directives = basePolicy
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directive =>
            {
                var parts = directive.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
                return (Name: parts[0], Sources: parts.Length > 1 ? parts[1] : "");
            })
            .ToDictionary(d => d.Name, d => d.Sources, StringComparer.OrdinalIgnoreCase);

        foreach (var (directiveName, extraSources) in additionalSources)
        {
            directives[directiveName] = directives.TryGetValue(directiveName, out var existing) && existing.Length > 0
                ? $"{existing} {extraSources}"
                : extraSources;
        }

        var sb = new StringBuilder();
        foreach (var (name, sources) in directives)
        {
            if (sb.Length > 0)
            {
                sb.Append("; ");
            }

            sb.Append(name);
            if (sources.Length > 0)
            {
                sb.Append(' ').Append(sources);
            }
        }

        return sb.ToString();
    }
}
