using System.Diagnostics.CodeAnalysis;
namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Serialises test classes that mutate process-wide environment variables.
/// Tests in this collection run sequentially with each other, preventing
/// env-var leakage from one parallel test class from affecting another that
/// reads those variables mid-execution (e.g. KEYCLOAK_BACKCHANNEL_URL +
/// ASPNETCORE_ENVIRONMENT affecting PrismSigningKeyCache.WarmAsync routing).
/// </summary>
[CollectionDefinition(Name)]
[SuppressMessage("Naming", "CA1711", Justification = "xUnit collection definitions are conventionally named ...Collection.")]
public sealed class EnvVarSensitiveTestCollection : ICollectionFixture<EnvVarSensitiveTestCollection>
{
    public const string Name = "EnvVarSensitive";
}
