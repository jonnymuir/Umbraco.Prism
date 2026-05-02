using FluentAssertions;
using Xunit;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Regression tests for Codespaces URL derivation logic in AppHost Program.cs.
/// 
/// Historical context: Prior to this fix, when `gh codespace ports` succeeded but
/// port 7245 was not in the output (e.g., not forwarded yet), the AppHost fell back
/// to "https://localhost:7245" which doesn't work in Codespaces. The TestSite's
/// DownstreamDemoController then tried to call localhost:7245 and failed with 401.
/// 
/// The fix: DeriveCodespaceUrl() extracts the host pattern from a known forwarded
/// URL (e.g., Keycloak on port 8443) and substitutes the target port, producing
/// the correct app.github.dev URL even when `gh` doesn't list that port yet.
/// </summary>
public class CodespacesUrlDerivationTests
{
    [Theory]
    [InlineData("https://mycodespace-8443.app.github.dev", 7245, "https://mycodespace-7245.app.github.dev")]
    [InlineData("https://mycodespace-8443.app.github.dev/", 7245, "https://mycodespace-7245.app.github.dev")]
    [InlineData("https://v7ldkc4c-8443.uks1.app.github.dev", 7245, "https://v7ldkc4c-7245.uks1.app.github.dev")]
    [InlineData("https://abc123xyz-8443.eus.app.github.dev", 44345, "https://abc123xyz-44345.eus.app.github.dev")]
    [InlineData("https://foo-bar-8443.app.github.dev", 7245, "https://foo-bar-7245.app.github.dev")]
    public void DeriveCodespaceUrl_SubstitutesPortInHostname(string knownUrl, int targetPort, string expected)
    {
        // Act
        var result = DeriveCodespaceUrl(knownUrl, targetPort);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com", 7245)] // No dash-port pattern
    [InlineData("https://no-port-here.app.github.dev", 7245)] // Dash but no numeric port
    [InlineData("https://just-a-hostname", 7245)] // No dot at all
    public void DeriveCodespaceUrl_ReturnsInputWhenPatternDoesNotMatch(string knownUrl, int targetPort)
    {
        // Act
        var result = DeriveCodespaceUrl(knownUrl, targetPort);

        // Assert
        // Should return the hostname with https:// prefix, without crashing
        result.Should().StartWith("https://");
        result.Should().NotContain("localhost");
    }

    [Fact]
    public void DeriveCodespaceUrl_PreservesScheme()
    {
        // Arrange
        const string knownUrl = "https://mycodespace-8443.app.github.dev";

        // Act
        var result = DeriveCodespaceUrl(knownUrl, 7245);

        // Assert
        result.Should().StartWith("https://");
    }

    [Fact]
    public void DeriveCodespaceUrl_DoesNotFallBackToLocalhost()
    {
        // Arrange - any valid Codespaces URL
        const string knownUrl = "https://v7ldkc4c-8443.uks1.app.github.dev";

        // Act
        var result = DeriveCodespaceUrl(knownUrl, 7245);

        // Assert - The regression: should NEVER return localhost in Codespaces context
        result.Should().NotContain("localhost", "Codespaces URL derivation must not fall back to localhost");
        result.Should().Contain(".app.github.dev", "Derived URL should remain in the app.github.dev domain");
    }

    // Mirrors the DeriveCodespaceUrl static local function from AppHost/Program.cs.
    // This is a whitebox test: we're testing the implementation logic by copying it here.
    // If the AppHost logic changes, this test must be updated to match.
    private static string DeriveCodespaceUrl(string knownUrl, int targetPort)
    {
        var uri = new Uri(knownUrl);
        var hostname = uri.Host;

        // Find the last dash before the first dot (port separator in Codespaces URLs)
        var firstDot = hostname.IndexOf('.');
        if (firstDot == -1)
            return $"https://{hostname}"; // Unexpected format, return as-is

        var lastDash = hostname.LastIndexOf('-', firstDot);
        if (lastDash == -1)
            return $"https://{hostname}"; // Unexpected format, return as-is

        // Extract the port substring and validate it's actually a number
        var portSubstring = hostname.Substring(lastDash + 1, firstDot - lastDash - 1);
        if (!int.TryParse(portSubstring, out _))
            return $"https://{hostname}"; // Not a valid port, return as-is

        // Replace port: {prefix}-{oldPort}.{suffix} → {prefix}-{newPort}.{suffix}
        var prefix = hostname.Substring(0, lastDash);
        var suffix = hostname.Substring(firstDot);
        return $"https://{prefix}-{targetPort}{suffix}";
    }
}
