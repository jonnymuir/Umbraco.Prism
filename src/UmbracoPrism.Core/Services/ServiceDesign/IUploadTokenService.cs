using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Binds a file already saved by the async upload endpoint to an opaque, short-lived token a
/// client carries in a hidden field — so the stage's eventual whole-page submission
/// (<c>PrismServiceRequestPageController.HandlePost</c>) can recognize "this field is already
/// satisfied" without re-uploading the bytes. Mirrors <see cref="IStageNonceService"/>'s
/// shape exactly, for the same reason: a random, server-issued, cache-backed token that only
/// resolves to something meaningful for the requester who was just issued it.
/// </summary>
public interface IUploadTokenService
{
    /// <summary>Caches an uploaded file's reference, scoped to the instance/field it belongs to, and returns a fresh token.</summary>
    Task<string> CreateAsync(string instanceId, string fieldKey, ServiceRequestFileReference reference, CancellationToken ct = default);

    /// <summary>
    /// Resolves a token back to its binding. Returns <see langword="null"/> if the token has
    /// expired or never existed. Callers must still confirm the returned binding's
    /// <c>InstanceId</c>/<c>FieldKey</c> match the current request before trusting it — this
    /// only proves the token is real, not that it belongs to this exact field.
    /// </summary>
    Task<UploadTokenBinding?> ResolveAsync(string token, CancellationToken ct = default);
}

/// <summary>What an upload token actually grants — the file, and which instance/field it was uploaded for.</summary>
public sealed record UploadTokenBinding
{
    public required string InstanceId { get; init; }
    public required string FieldKey { get; init; }
    public required ServiceRequestFileReference Reference { get; init; }
}
