namespace UmbracoPrism.Core.Sessions;

/// <summary>Where Prism keeps member sessions. Set <c>Prism:Session:Storage</c>.</summary>
public enum PrismSessionStorageKind
{
    /// <summary>The Umbraco database. Nothing to set up.</summary>
    Database,

    /// <summary>The host's <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>, such as Redis.</summary>
    DistributedCache,
}
