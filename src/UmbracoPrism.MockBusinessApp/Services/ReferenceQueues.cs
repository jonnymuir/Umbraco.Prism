using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;
using UmbracoPrism.ProcessManager.Abstractions;

namespace UmbracoPrism.MockBusinessApp.Services;

public static class ReferenceQueues
{
    public const string WebUser = "web-user";
    public const string BusinessUser = "business-user";

    /// <summary>
    /// Component types MockBusinessApp's admin web page actually renders for a business-user
    /// state — its own honest contract about itself, matching exactly what its admin rendering
    /// code implements (see Program.cs's admin workflow routes). Kept in sync with that code by
    /// hand since it's a small, deliberately-scoped set, not the full component catalog.
    /// </summary>
    private static readonly IReadOnlyList<string> BusinessUserSupportedComponentTypes =
        ["body", "panel", "fieldset", "text", "decimal", "textarea", "summary-list"];

    public static ActorProfile WebUserProfile() => new()
    {
        VisibleQueues = [WebUser],
        StartableQueues = [WebUser],
        ActionableQueues = [WebUser]
    };

    public static ActorProfile BusinessUserProfile() => new()
    {
        VisibleQueues = [BusinessUser],
        ActionableQueues = [BusinessUser],
        RestrictToInstanceOwner = false
    };

    /// <summary>
    /// Declares both queues' render capabilities as an explicit contract, not a guess: web-user
    /// is served by UmbracoPrism.TestSite (a different process entirely), but since Prism's
    /// component catalog is a closed, compile-time-fixed set declared on PrismComponent in
    /// UmbracoPrism.Shared — a package both TestSite and MockBusinessApp already reference —
    /// "what a stock Prism-Core web host renders" is provable locally via
    /// PrismComponentTypeCatalog, not something that requires calling back into TestSite's
    /// process. business-user is MockBusinessApp's own contract about itself, since it owns and
    /// writes that admin-rendering code directly.
    /// </summary>
    public static IQueueCapabilitiesProvider CapabilitiesProvider() => new StaticQueueCapabilitiesProvider(
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [WebUser] = PrismComponentTypeCatalog.AllDiscriminators,
            [BusinessUser] = BusinessUserSupportedComponentTypes
        });
}
