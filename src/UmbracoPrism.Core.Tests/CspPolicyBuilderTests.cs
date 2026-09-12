using FluentAssertions;
using UmbracoPrism.Core.Configuration;

namespace UmbracoPrism.Core.Tests;

public class CspPolicyBuilderTests
{
    [Fact]
    public void WithAdditionalSources_ReturnsBasePolicyUnchanged_WhenNoAdditionalSourcesGiven()
    {
        const string basePolicy = "default-src 'self'; script-src 'self'";

        var result = CspPolicyBuilder.WithAdditionalSources(basePolicy, new Dictionary<string, string>());

        result.Should().Be(basePolicy);
    }

    [Fact]
    public void WithAdditionalSources_AppendsToAnExistingDirective()
    {
        const string basePolicy = "default-src 'self'; img-src 'self' data:";

        var result = CspPolicyBuilder.WithAdditionalSources(basePolicy, new Dictionary<string, string>
        {
            ["img-src"] = "https://images.example.com"
        });

        result.Should().Contain("img-src 'self' data: https://images.example.com");
        result.Should().Contain("default-src 'self'", "unrelated directives are left alone");
    }

    [Fact]
    public void WithAdditionalSources_AddsANewDirective_WhenNotAlreadyPresent()
    {
        const string basePolicy = "default-src 'self'";

        var result = CspPolicyBuilder.WithAdditionalSources(basePolicy, new Dictionary<string, string>
        {
            ["frame-src"] = "https://payments.example.com"
        });

        result.Should().Contain("frame-src https://payments.example.com");
        result.Should().Contain("default-src 'self'");
    }

    [Fact]
    public void WithAdditionalSources_MatchesDirectiveNamesCaseInsensitively()
    {
        const string basePolicy = "Img-Src 'self'";

        var result = CspPolicyBuilder.WithAdditionalSources(basePolicy, new Dictionary<string, string>
        {
            ["img-src"] = "https://images.example.com"
        });

        result.Should().Contain("https://images.example.com");
        // Exactly one img-src directive, not two separately-cased ones.
        result.Split(';', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(1);
    }
}
