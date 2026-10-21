using System.Globalization;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using UmbracoPrism.Core.Services.JourneyStatus;

namespace UmbracoPrism.Automate;

/// <summary>
/// Settings for <see cref="PrismUpdateJourneyStatusAction"/>.
/// </summary>
public sealed class PrismUpdateJourneyStatusSettings
{
    [Field(Label = "User id", Description = "The Prism member whose journey this is, as push notifications identify them.", SupportsBindings = true)]
    public string UserId { get; set; } = string.Empty;

    [Field(Label = "Tenant id", Description = "The Prism tenant the member belongs to.", SortOrder = 1, SupportsBindings = true)]
    public string TenantId { get; set; } = string.Empty;

    [Field(Label = "Journey key", Description = "One per journey, such as the service request's ID. Later statuses with the same key update the same one.", SortOrder = 2, SupportsBindings = true)]
    public string JourneyKey { get; set; } = string.Empty;

    [Field(Label = "Service name", Description = "The service, such as Apply for a juggling licence.", SortOrder = 3, SupportsBindings = true)]
    public string ServiceName { get; set; } = string.Empty;

    [Field(Label = "Status", Description = "One short line saying what kind of thing is happening, never an outcome or personal detail: Deciding your application, not Your licence was refused.", SortOrder = 4, SupportsBindings = true)]
    public string Status { get; set; } = string.Empty;

    [Field(Label = "Kind", Description = "Waiting while the member waits on something now, or Done when that wait is over.", SortOrder = 5)]
    public string Kind { get; set; } = nameof(PrismJourneyStatusKind.Waiting);

    [Field(Label = "Path", Description = "The site page tapping the status opens, such as /apply-for-a-juggling-licence.", SortOrder = 6, SupportsBindings = true)]
    public string Path { get; set; } = string.Empty;

    [Field(Label = "Step", Description = "Optional. The step reached, shown as Step 2 of 3 with Steps.", SortOrder = 7, SupportsBindings = true)]
    public string? Step { get; set; }

    [Field(Label = "Steps", Description = "Optional. How many steps there are.", SortOrder = 8, SupportsBindings = true)]
    public string? Steps { get; set; }
}

/// <summary>
/// A custom Umbraco Automate action that shows where one of a Prism member's journeys has got to,
/// on their phone's lock screen, through <see cref="IPrismJourneyStatusService"/>. The status is
/// queued and delivered in the background, so the automation never waits on it. Auto-discovered
/// by <c>[Action]</c>.
/// </summary>
[Action("prism.updateJourneyStatus", "Update journey status",
    Description = "Shows where one of a Prism member's journeys has got to, on their phone's lock screen.",
    Group = "Prism",
    Icon = "icon-time")]
public sealed class PrismUpdateJourneyStatusAction(
    ActionInfrastructure infrastructure,
    IPrismJourneyStatusService journeyStatus)
    : ActionBase<PrismUpdateJourneyStatusSettings>(infrastructure)
{
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PrismUpdateJourneyStatusSettings>();
        if (string.IsNullOrWhiteSpace(settings.UserId) || string.IsNullOrWhiteSpace(settings.TenantId))
        {
            return Task.FromResult(Invalid("Both userId and tenantId are required."));
        }

        if (!Enum.TryParse<PrismJourneyStatusKind>(settings.Kind, ignoreCase: true, out var kind))
        {
            return Task.FromResult(Invalid("Kind must be Waiting or Done."));
        }

        if (!TryParseStep(settings.Step, out var step) || !TryParseStep(settings.Steps, out var steps))
        {
            return Task.FromResult(Invalid("Step and Steps must be whole numbers."));
        }

        var status = new PrismJourneyStatus(
            settings.JourneyKey, settings.ServiceName, settings.Status, step, steps, settings.Path, kind);
        if (status.Problem() is { } problem)
        {
            return Task.FromResult(Invalid(problem));
        }

        journeyStatus.Publish(settings.UserId, settings.TenantId, status);
        return Task.FromResult(Success());
    }

    private static ActionResult Invalid(string message) =>
        ActionResult.Failed(new ArgumentException(message), StepRunErrorCategory.Validation);

    private static bool TryParseStep(string? value, out int? step)
    {
        step = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) return false;
        step = parsed;
        return true;
    }
}
