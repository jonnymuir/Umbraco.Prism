using Microsoft.AspNetCore.Http;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Stores and retrieves files uploaded against a workflow's <c>file-upload</c> fields. The
/// default implementation is disk-backed (<see cref="DiskWorkflowFileStorage"/>); a host can
/// register its own (blob storage, etc.) by replacing the DI registration.
/// </summary>
public interface IWorkflowFileStorage
{
    /// <summary>Saves an uploaded file and returns a reference to it.</summary>
    Task<WorkflowFileReference> SaveAsync(
        string instanceId,
        string fieldKey,
        IFormFile file,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a stream to read a previously-saved file's contents.</summary>
    Task<Stream> OpenReadAsync(WorkflowFileReference reference, CancellationToken cancellationToken = default);
}
