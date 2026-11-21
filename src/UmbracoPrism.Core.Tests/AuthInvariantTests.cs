using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Source-level invariants for the home-made authentication code. Each is a rule that a review found
/// broken once, written as a test so it cannot regress without someone noticing.
/// </summary>
public partial class AuthInvariantTests
{
    [GeneratedRegex(@"Validate(Issuer|Audience|Lifetime|IssuerSigningKey)\s*=\s*false|RequireSignedTokens\s*=\s*false")]
    private static partial Regex DisabledValidation();

    [Fact]
    public void NoTokenValidationCheck_IsTurnedOff_AnywhereInTheShippedCode()
    {
        var offenders = ShippedSource().Where(file => DisabledValidation().IsMatch(File.ReadAllText(file))).ToList();

        offenders.Should().BeEmpty("token validation must never be disabled; {0}", string.Join(", ", offenders));
    }

    [Fact]
    public void TheCorePackage_WritesNothingToTheConsole()
    {
        var offenders = CoreSource().Where(file => File.ReadAllText(file).Contains("Console.Write", StringComparison.Ordinal)).ToList();

        offenders.Should().BeEmpty("diagnostics go through ILogger with scrubbed values; found in {0}", string.Join(", ", offenders));
    }

    [Fact]
    public void TheCorePackage_NeverCreatesAnHttpClientDirectly()
    {
        var offenders = CoreSource().Where(file => File.ReadAllText(file).Contains("new HttpClient(", StringComparison.Ordinal)).ToList();

        offenders.Should().BeEmpty("use IHttpClientFactory so timeouts and handlers are managed; found in {0}", string.Join(", ", offenders));
    }

    private static readonly string[] ShippedProjects =
        ["UmbracoPrism.Core", "UmbracoPrism.uSync", "UmbracoPrism.Automate", "UmbracoPrism.TestSite", "UmbracoPrism.MockBusinessApp"];

    private static IEnumerable<string> CoreSource() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "UmbracoPrism.Core"), "*.cs", SearchOption.AllDirectories)
            .Where(NotGenerated);

    private static IEnumerable<string> ShippedSource() =>
        ShippedProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", project), "*.cs", SearchOption.AllDirectories))
            .Where(NotGenerated);

    private static bool NotGenerated(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UmbracoPrism.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root from the test output directory.");
    }
}
