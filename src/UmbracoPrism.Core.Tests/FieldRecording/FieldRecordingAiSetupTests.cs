using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.AI.Agent.Core.Agents;
using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Models;
using Umbraco.AI.Core.Profiles;
using UmbracoPrism.TestSite.FieldRecording;

namespace UmbracoPrism.Core.Tests.FieldRecording;

/// <summary>
/// Which Gemini model the field recording profile uses. The profile is created once and then lives in
/// the database, so the configured model has to be applied to a profile that already exists: a setting
/// that only works on a first boot reads as ignored.
/// </summary>
public class FieldRecordingAiSetupTests
{
    private readonly Mock<IAIConnectionService> connections = new();
    private readonly Mock<IAIProfileService> profiles = new();
    private readonly Mock<IAIAgentService> agents = new();

    private AIProfile? savedProfile;

    public FieldRecordingAiSetupTests()
    {
        connections
            .Setup(c => c.GetConnectionByAliasAsync(FieldRecordingAiSetup.ConnectionAlias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIConnection { Alias = FieldRecordingAiSetup.ConnectionAlias, Name = "c", ProviderId = "google" });

        profiles
            .Setup(p => p.SaveProfileAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()))
            .Callback((AIProfile profile, CancellationToken _) => savedProfile = profile)
            .ReturnsAsync((AIProfile profile, CancellationToken _) => profile);

        agents
            .Setup(a => a.SaveAgentAsync(It.IsAny<AIAgent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIAgent agent, CancellationToken _) => agent);
    }

    private static AIProfile ProfileOn(string model) => new()
    {
        Alias = FieldRecordingAiSetup.ProfileAlias,
        Name = "Field recording vision",
        Capability = AICapability.Chat,
        Model = new AIModelRef("google", model),
        ConnectionId = Guid.NewGuid(),
    };

    private Task<Guid> Ensure(string? configuredModel, AIProfile? existingProfile)
    {
        profiles
            .Setup(p => p.GetProfileByAliasAsync(FieldRecordingAiSetup.ProfileAlias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProfile);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configuredModel is null
                ? []
                : new Dictionary<string, string?> { [FieldRecordingAiSetup.ModelConfigKey] = configuredModel })
            .Build();

        return new FieldRecordingAiSetup(
            connections.Object, profiles.Object, agents.Object, configuration, NullLogger<FieldRecordingAiSetup>.Instance)
            .EnsureAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ANewProfile_UsesTheConfiguredModel()
    {
        await Ensure("gemini-3.7-flash", existingProfile: null);

        savedProfile!.Model.ModelId.Should().Be("gemini-3.7-flash");
    }

    [Fact]
    public async Task ANewProfile_WithNoModelConfigured_UsesTheDefault()
    {
        await Ensure(configuredModel: null, existingProfile: null);

        savedProfile!.Model.ModelId.Should().Be(FieldRecordingAiSetup.DefaultModel);
    }

    [Fact]
    public async Task AnExistingProfileOnAnotherModel_IsMovedToTheConfiguredModel()
    {
        var existing = ProfileOn("gemini-3.8-flash");

        await Ensure("gemini-3.7-flash", existing);

        savedProfile.Should().BeSameAs(existing);
        savedProfile!.Model.ModelId.Should().Be("gemini-3.7-flash");
    }

    [Fact]
    public async Task AnExistingProfile_WithNoModelConfigured_IsLeftAlone()
    {
        // Someone may have picked a model in the backoffice; with nothing set in config, that choice stands.
        await Ensure(configuredModel: null, existingProfile: ProfileOn("gemini-3.1-flash-lite"));

        profiles.Verify(p => p.SaveProfileAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnExistingProfileAlreadyOnTheConfiguredModel_IsNotSavedAgain()
    {
        await Ensure("gemini-3.7-flash", ProfileOn("gemini-3.7-flash"));

        profiles.Verify(p => p.SaveProfileAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
