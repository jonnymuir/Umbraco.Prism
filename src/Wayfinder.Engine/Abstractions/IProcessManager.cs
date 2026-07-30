using Wayfinder.Models.ServiceDesign;
using Wayfinder.Engine.Models;

namespace Wayfinder.Engine.Abstractions;

public interface IProcessManager
{
    ServiceRequestResponseEnvelope GetCurrent(
        string blueprintKey,
        string tenantId,
        string userId,
        string? instanceId = null,
        string? action = null);

    ServiceRequestResponseEnvelope GetCurrent(
        string blueprintKey,
        string tenantId,
        string userId,
        ActorProfile accessProfile,
        string? instanceId = null,
        string? action = null);

    ServiceRequestResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues);

    ServiceRequestResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        ActorProfile accessProfile,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues);

    ServiceRequestListEnvelope GetInstances(string tenantId, string userId);

    /// <summary>
    /// Re-keys every instance owned by <paramref name="fromUserId"/> onto
    /// <paramref name="toUserId"/> and marks each as authenticated — for a visitor who was
    /// browsing anonymously and has just signed in, so their in-progress instance survives
    /// as a resumable one instead of being orphaned under an identity nothing will ever
    /// resolve to again. An instance is left alone (not claimed) if <paramref name="toUserId"/>
    /// already owns an instance of that same blueprint — claiming would silently discard
    /// whichever the caller didn't return here. Returns the claimed instance ids.
    /// </summary>
    IReadOnlyList<string> ClaimInstances(string tenantId, string fromUserId, string toUserId);

    QueueWorkListEnvelope GetQueueWorkItems(ActorProfile accessProfile);

    IEnumerable<ServiceRequest> GetAllInstances();

    IEnumerable<ServiceBlueprint> GetAllDefinitions();

    ServiceBlueprint? GetDefinition(string key);

    /// <summary>Registers or updates a definition — an upsert. Always returns true.</summary>
    bool UpdateDefinition(string key, ServiceBlueprint updated);

    /// <summary>Removes a definition. Returns false if it wasn't registered.</summary>
    bool RemoveDefinition(string key);

    bool Reset(string instanceId);

    void ResetAll();
}
