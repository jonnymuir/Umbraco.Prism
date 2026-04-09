namespace UmbracoPrism.TestSite.Models;

/// <summary>
/// Carries the form submission for the workflow Advance POST.
/// </summary>
public class WorkflowAdvanceRequest
{
    /// <summary>The running workflow instance identifier.</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Expected state version for optimistic concurrency.</summary>
    public int StateVersion { get; set; }

    /// <summary>Workflow definition key (echoed from form).</summary>
    public string WorkflowKey { get; set; } = string.Empty;

    /// <summary>The action to perform (e.g. "continue", "submit", "back").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>URL to redirect to after successful advance (PRG).</summary>
    public string ReturnUrl { get; set; } = "/";

    /// <summary>Field values submitted by the user, keyed by field alias.</summary>
    public Dictionary<string, string> FieldValues { get; set; } = new();
}
