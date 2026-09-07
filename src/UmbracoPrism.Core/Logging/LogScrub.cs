namespace UmbracoPrism.Core.Logging;

/// <summary>
/// Strips CR / LF from a value before it is passed as a log-message argument, so a
/// caller- or request-controlled string cannot inject forged lines into a plain-text log
/// sink (CodeQL <c>cs/log-forging</c>). Prism emits structured logs, but a console / file
/// sink still renders each argument onto one line, so the newline is what matters.
/// </summary>
internal static class LogScrub
{
    /// <summary>Returns <paramref name="value"/> with all carriage returns and line feeds replaced by spaces.</summary>
    public static string? Line(string? value) =>
        value is null
            ? null
            : value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
}
