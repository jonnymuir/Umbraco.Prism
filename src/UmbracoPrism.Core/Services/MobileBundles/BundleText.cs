using System.Text;

namespace UmbracoPrism.Core.Services.MobileBundles;

/// <summary>Escaping and naming helpers for the text the bundle generates.</summary>
internal static class BundleText
{
    /// <summary>The trimmed value, or <paramref name="fallback"/> if it is missing or blank.</summary>
    internal static string OrDefault(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    internal static string ToSafeIdentifier(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length == 0 || builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "tenant" : result;
    }

    internal static string EscapeSingleQuotes(string value) => value.Replace("'", "\\'");

    internal static string ToJsonStringOrNull(string? value)
    {
      if (string.IsNullOrWhiteSpace(value)) return "null";
      var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
      return $"\"{escaped}\"";
    }

    internal static string ToJsonBoolean(bool value) => value ? "true" : "false";
}
