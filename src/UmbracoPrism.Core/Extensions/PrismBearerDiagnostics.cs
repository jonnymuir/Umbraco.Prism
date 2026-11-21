using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Logging;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// What is logged when a bearer token is refused. The claims come from a token that has not been
/// validated, so they are attacker-controlled: they are scrubbed of line breaks and cut short, and
/// nothing about the server's own configuration is written out.
/// </summary>
internal static partial class PrismBearerDiagnostics
{
    internal const string CategoryName = "UmbracoPrism.Core.Auth.Bearer";

    private const int MaxClaimLength = 200;

    internal static string? ReadBearerToken(string? authorizationHeader) =>
        authorizationHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authorizationHeader["Bearer ".Length..]
            : null;

    internal static void LogFailure(ILogger logger, Exception exception, string? rawToken)
    {
        var (issuer, authorizedParty, keyId) = ReadUnvalidatedClaims(rawToken);
        LogRefused(logger, exception.GetType().Name, Clean(issuer), Clean(authorizedParty), Clean(keyId));
    }

    internal static (string? Issuer, string? AuthorizedParty, string? KeyId) ReadUnvalidatedClaims(string? rawToken)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return default;
        }

        try
        {
            var parts = rawToken.Split('.');
            if (parts.Length < 2)
            {
                return default;
            }

            using var header = JsonDocument.Parse(DecodeSegment(parts[0]));
            using var payload = JsonDocument.Parse(DecodeSegment(parts[1]));
            return (
                ReadString(payload.RootElement, "iss"),
                ReadString(payload.RootElement, "azp"),
                ReadString(header.RootElement, "kid"));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return default;
        }
    }

    internal static string? Clean(string? value)
    {
        var scrubbed = LogScrub.Line(value);
        return scrubbed is { Length: > MaxClaimLength } ? scrubbed[..MaxClaimLength] : scrubbed;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string DecodeSegment(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/') + new string('=', (4 - segment.Length % 4) % 4);
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Bearer token refused ({Reason}). Unvalidated claims: iss={Issuer} azp={AuthorizedParty} kid={KeyId}")]
    private static partial void LogRefused(ILogger logger, string reason, string? issuer, string? authorizedParty, string? keyId);
}
