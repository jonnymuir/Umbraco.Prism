using System.Text;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Builds the <c>:root{...}</c> CSS custom-property override block for a tenant's branding
/// overrides. Extracted from <c>PrismBrandingMiddleware</c> (SEC-PT2-004 CSP follow-up): the
/// overrides used to be spliced inline into every HTML response as a <c>&lt;style&gt;</c>
/// element; they are now served as their own resource by
/// <see cref="Controllers.PrismBrandingAssetsController"/>, so this logic needed to stand on
/// its own rather than living as a private method on the middleware.
/// </summary>
public static class PrismBrandingCssBuilder
{
    /// <summary>
    /// Builds the CSS text for a tenant's branding overrides. Precomputed declaration strings
    /// (<paramref name="overrideDeclarations"/>/<paramref name="mobileOverrideDeclarations"/>,
    /// validated at save time by <c>TenantService</c>) are preferred over rebuilding from the
    /// raw dictionaries when present. Returns an empty string when there is nothing to override
    /// — callers always get a valid, if empty, CSS document rather than needing to special-case
    /// "no branding configured".
    /// </summary>
    public static string BuildCssOverrides(
        IReadOnlyDictionary<string, string>? overrides,
        IReadOnlyDictionary<string, string>? mobileOverrides,
        string? overrideDeclarations,
        string? mobileOverrideDeclarations)
    {
        var hasOverrides = (overrides is { Count: > 0 }) || (mobileOverrides is { Count: > 0 });
        if (!hasOverrides)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(":root{");

        if (!string.IsNullOrWhiteSpace(overrideDeclarations))
        {
            builder.Append(overrideDeclarations);
        }
        else
        {
            AppendOverrides(builder, overrides);
        }

        if (!string.IsNullOrWhiteSpace(mobileOverrideDeclarations))
        {
            builder.Append(mobileOverrideDeclarations);
        }
        else
        {
            AppendOverrides(builder, mobileOverrides);
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendOverrides(StringBuilder builder, IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides == null || overrides.Count == 0)
        {
            return;
        }

        foreach (var (name, value) in overrides)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) continue;
            var trimmedName = name.Trim();
            var trimmedValue = value.Trim();
            if (!PrismBrandingCssSafety.IsSafePropertyName(trimmedName) || !PrismBrandingCssSafety.IsSafeValue(trimmedValue))
            {
                continue;
            }

            builder.Append(trimmedName);
            builder.Append(':');
            builder.Append(trimmedValue);
            builder.Append(';');
        }
    }
}
