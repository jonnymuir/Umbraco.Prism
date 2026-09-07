using FluentAssertions;
using UmbracoPrism.Core.Logging;
using Xunit;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// <see cref="LogScrub"/> is what stops a caller- or request-controlled string from
/// injecting forged lines into a plain-text log sink (CodeQL cs/log-forging). If any of
/// these regress, the sanitisation applied at the ~16 call sites in UmbracoPrism.Core is
/// no longer effective.
/// </summary>
public class LogScrubTests
{
    [Theory]
    [InlineData("plain value", "plain value")]
    [InlineData("with\nlinefeed", "with linefeed")]
    [InlineData("with\rcarriage", "with carriage")]
    [InlineData("crlf\r\npair", "crlf pair")]
    [InlineData("multi\n\nline\r\r", "multi  line  ")]
    [InlineData("2026-01-01 00:00 [WARN] forged\nadmin: deleted everything", "2026-01-01 00:00 [WARN] forged admin: deleted everything")]
    public void Line_ReplacesEveryCrAndLfWithASpace(string input, string expected)
    {
        LogScrub.Line(input).Should().Be(expected);
    }

    [Fact]
    public void Line_PassesNullThrough()
    {
        LogScrub.Line(null).Should().BeNull();
    }

    [Fact]
    public void Line_LeavesAStringWithNoNewlinesUnchanged()
    {
        const string s = "device-abc_123 / tenant 7 (idempotent)";
        LogScrub.Line(s).Should().Be(s);
    }

    [Fact]
    public void Line_HandlesTheEmptyString()
    {
        LogScrub.Line(string.Empty).Should().Be(string.Empty);
    }
}
