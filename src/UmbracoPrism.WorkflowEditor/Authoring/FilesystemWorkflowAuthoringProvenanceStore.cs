using System.Text.Json;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// File-backed implementation of <see cref="IWorkflowAuthoringProvenanceStore"/>.
/// </summary>
public sealed class FilesystemWorkflowAuthoringProvenanceStore(string basePath) : IWorkflowAuthoringProvenanceStore
{
    public async Task<string?> SaveAsync(
        string workflowKey,
        ProposalEnvelope envelope,
        string approver,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(basePath);

        var utcStamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var fileName = $"{workflowKey}-{utcStamp}.json";
        var path = Path.Combine(basePath, fileName);

        var payload = new
        {
            workflowKey,
            envelopeId = envelope.Id,
            createdAt = envelope.CreatedAt,
            approver,
            agent = envelope.Agent,
            rationale = envelope.Rationale,
            ops = envelope.Ops
        };

        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, payload, WorkflowProjector.CanonicalOptions, ct);
        return path;
    }
}
