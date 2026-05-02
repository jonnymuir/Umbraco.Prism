namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Serialises test classes that mutate process-wide environment variables.
/// Tests in this collection run sequentially with each other, preventing
/// env-var leakage from one parallel test class from affecting another that
/// reads those variables mid-execution (e.g. KEYCLOAK_BACKCHANNEL_URL +
/// ASPNETCORE_ENVIRONMENT affecting PrismSigningKeyCache.WarmAsync routing).
/// </summary>
[CollectionDefinition(Name)]
public sealed class EnvVarSensitiveTestCollection : ICollectionFixture<EnvVarSensitiveTestCollection>
{
    public const string Name = "EnvVarSensitive";
}
