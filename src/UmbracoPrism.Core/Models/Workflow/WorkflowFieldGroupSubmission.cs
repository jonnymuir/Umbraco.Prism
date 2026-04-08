namespace UmbracoPrism.Core.Models.Workflow;

/// <summary>
/// Represents a submitted field group for a workflow instance.
/// Stores validated user input with version pinning.
/// </summary>
public class WorkflowFieldGroupSubmission
{
    /// <summary>
    /// Gets or sets the unique submission identifier.
    /// </summary>
    public string SubmissionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance this submission belongs to.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field group key this submission corresponds to.
    /// </summary>
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID who submitted this field group.
    /// </summary>
    public string SubmittedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the submitted field values as JSON.
    /// </summary>
    public string SubmissionJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the UTC timestamp when the submission occurred.
    /// </summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>
    /// Gets or sets whether this is the latest submission for the field group.
    /// </summary>
    public bool IsLatest { get; set; } = true;
}
