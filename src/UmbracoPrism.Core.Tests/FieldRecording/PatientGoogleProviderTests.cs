using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Umbraco.AI.Core.EditableModels;
using Umbraco.AI.Core.Providers;
using Umbraco.AI.Google;
using UmbracoPrism.TestSite.FieldRecording;

namespace UmbracoPrism.Core.Tests.FieldRecording;

/// <summary>
/// The stand-in for Umbraco.AI's Google provider that gives a slow free-tier Gemini model longer than
/// .NET's default 100 seconds. It has to take the stock provider's place exactly: same id, so the
/// existing connection and profile resolve to it, and only the patient chat capability.
/// </summary>
public class PatientGoogleProviderTests
{
    private sealed class ActivatorCapabilityFactory : IAICapabilityFactory
    {
        public TCapability Create<TCapability>(IAIProvider provider) where TCapability : class, IAICapability =>
            (TCapability)Activator.CreateInstance(typeof(TCapability), provider)!;
    }

    private static PatientGoogleProvider Provider()
    {
        var infrastructure = new Mock<IAIProviderInfrastructure>();
        infrastructure.SetupGet(i => i.CapabilityFactory).Returns(new ActivatorCapabilityFactory());
        infrastructure.SetupGet(i => i.SchemaBuilder).Returns(Mock.Of<IAIEditableModelSchemaBuilder>());
        return new PatientGoogleProvider(infrastructure.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public void It_answers_to_the_google_provider_id()
    {
        Provider().Id.Should().Be("google");
    }

    [Fact]
    public void Its_only_capability_is_the_patient_chat_capability()
    {
        Provider().GetCapabilities().Should().ContainSingle()
            .Which.Should().BeOfType<PatientGoogleChatCapability>();
    }

    [Fact]
    public void It_is_still_a_google_provider_so_the_chat_capability_accepts_it()
    {
        Provider().Should().BeAssignableTo<GoogleProvider>();
    }

    [Fact]
    public void The_timeout_outlasts_dotnets_default_and_stays_inside_an_automate_step()
    {
        PatientGoogleChatCapability.RequestTimeout.Should().BeGreaterThan(TimeSpan.FromSeconds(100))
            .And.BeLessThan(TimeSpan.FromMinutes(5));
    }
}
