using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Umbraco.Cms.Api.Management.Controllers;
using UmbracoPrism.Core.Controllers;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// SECURITY REGRESSION — the authorization contract for every HTTP endpoint Prism ships
/// (UmbracoPrism.Core) and every endpoint its reference host adds (UmbracoPrism.TestSite).
///
/// Copper mandate / deny-by-default: every controller action must declare an explicit decision —
/// an [Authorize], an [AllowAnonymous] on <see cref="KnownAnonymous"/> with a written reason, or
/// a <see cref="KnownImperativeAuth"/> entry for the one case an attribute cannot express. And
/// per SEC-PT2-009, every state-changing action must declare its antiforgery disposition
/// (<see cref="ValidateAntiForgeryTokenAttribute"/> or an explicit
/// <see cref="IgnoreAntiforgeryTokenAttribute"/>).
///
/// Controllers are discovered by reflection over both assemblies, so a newly added one cannot
/// slip through. This systematises what <c>Phase1SecurityRegressionTests</c> asserts per-endpoint.
/// </summary>
public class AuthorizationContractTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(PrismComposer).Assembly,                     // UmbracoPrism.Core (published)
        typeof(global::UmbracoPrism.TestSite.TestSiteComposer).Assembly, // UmbracoPrism.TestSite (never ships)
    ];

    /// <summary>Controllers/actions allowed to be anonymous, each with the reason it is safe.</summary>
    private static readonly Dictionary<(Type Controller, string Action), string> KnownAnonymous = new()
    {
        [(typeof(AccountController), "*")] =
            "The Entra ID auth entry/exit surface. Login/Register issue an OIDC Challenge (there is " +
            "no session yet); Logout is a POST guarded by [ValidateAntiForgeryToken] (SEC-PT2-003 " +
            "logout-CSRF). None of the three has anything to authorize.",

        [(typeof(global::UmbracoPrism.TestSite.Controllers.PublicServiceRequestFileDownloadController), "*")] =
            "Public citizen file-download surface (never-ships TestSite). Deliberately no [Authorize] — " +
            "identity is resolved from WayfinderServiceDesignOptions (which a host may resolve for an " +
            "anonymous citizen journey) and the caller must own the instance: engine.TryGetOwnedFileReference " +
            "returns null for an unknown OR unowned instance, and the action returns 404 for both — no " +
            "distinction, no enumeration oracle. That ownership check is the access boundary.",

        [(typeof(global::UmbracoPrism.TestSite.Controllers.PublicServiceRequestFileUploadController), "*")] =
            "Public citizen file-upload surface (never-ships TestSite). Same anonymous-citizen rationale " +
            "as the download controller. Additionally: the action validates the antiforgery token by hand " +
            "(IAntiforgery.ValidateRequestAsync -> 400) and binds the upload to a single-use stage nonce, so " +
            "it can only be aimed at a field in the visitor's own current stage.",

        [(typeof(BiometricController), nameof(BiometricController.Exchange))] =
            "Biometric login: presents a signed BiometricToken JWT and receives a PrismMemberCookie " +
            "session. There is no session yet — the JWT itself is the credential (verified in the action), " +
            "so [AllowAnonymous] is required. Hardened by an IsCapacitorOrigin check and a dedicated " +
            "rate limiter (ExchangeRateLimitService).",
        [(typeof(BiometricController), "ExchangePreflight")] =
            "CORS preflight (OPTIONS) for the Exchange endpoint — a browser sends it before the credential " +
            "and it carries no auth.",

        [(typeof(global::UmbracoPrism.TestSite.Controllers.DownstreamDemoController), "GetSessionContract")] =
            "Never-ships TestSite dashboard diagnostic — reports the shape of the current session, no " +
            "sensitive values, gated to Development / explicit config like the rest of the controller.",
        [(typeof(global::UmbracoPrism.TestSite.Controllers.DownstreamDemoController), "GetSeedContractReady")] =
            "Never-ships TestSite dashboard diagnostic — a readiness flag for the demo's seed data.",

        [(typeof(PrismBrandingAssetsController), "*")] =
            "Serves a tenant's branding override CSS as a plain stylesheet (SEC-PT2-004 CSP " +
            "follow-up — externally-referenced resources need no CSP inline-content exception, " +
            "unlike the inline <style> this replaced). Read-only, no session, no capability: " +
            "the response is CSS text derived from the current tenant's own already-public " +
            "branding config, resolved the same way any other page on that tenant's host is.",
    };

    /// <summary>Controllers whose auth decision is a deliberate imperative check, not an attribute.</summary>
    private static readonly Dictionary<Type, string> KnownImperativeAuth = new()
    {
        [typeof(MemberDashboardController)] =
            "Index() redirects an unauthenticated caller to the PrismMemberCookie scheme's configured " +
            "login path (RenderController route-hijack; the [Authorize] challenge cannot build that " +
            "redirect for a route-hijacked doc type). Fail-closed redirect is asserted by a booted-host " +
            "test (Layer 2).",
    };

    /// <summary>State-changing actions that validate antiforgery imperatively rather than via the attribute.</summary>
    private static readonly Dictionary<(Type Controller, string Action), string> KnownImperativeAntiforgery = new()
    {
        [(typeof(global::UmbracoPrism.TestSite.Controllers.PublicServiceRequestFileUploadController), "Upload")] =
            "Calls IAntiforgery.ValidateRequestAsync in the action body and returns 400 on failure — a " +
            "multipart upload endpoint that also enforces a single-use stage nonce.",
    };

    private static IEnumerable<Type> AllControllers() =>
        Assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && (typeof(ControllerBase).IsAssignableFrom(t) || typeof(Controller).IsAssignableFrom(t)))
            .Where(t => t.Namespace?.StartsWith("UmbracoPrism") == true)
            .Distinct();

    public static TheoryData<Type> Controllers()
    {
        var data = new TheoryData<Type>();
        foreach (var c in AllControllers())
        {
            data.Add(c);
        }

        return data;
    }

    [Fact]
    public void AtLeastTheKnownControllersAreDiscovered()
    {
        AllControllers().Should().Contain(new[]
        {
            typeof(AccountController), typeof(MemberDashboardController), typeof(DeviceAdminController),
            typeof(TenantManagementController), typeof(BiometricController), typeof(PrismNotificationController),
        }, "reflection discovery is how a new controller is forced through this contract");
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void EveryActionDeclaresAnExplicitAuthorizationDecision(Type controller)
    {
        var classAuthorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
        var classAllowAnon = controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

        var actions = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(IsAction)
            .ToList();

        actions.Should().NotBeEmpty("{0} is a controller with no discoverable actions — the discovery filter is wrong", controller.Name);

        foreach (var action in actions)
        {
            var actionAuthorize = action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
            var actionAllowAnon = action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

            if (classAllowAnon || actionAllowAnon)
            {
                (KnownAnonymous.ContainsKey((controller, action.Name)) || KnownAnonymous.ContainsKey((controller, "*")))
                    .Should().BeTrue("{0}.{1} is [AllowAnonymous] — it must be on KnownAnonymous with a written reason",
                        controller.Name, action.Name);
                continue;
            }

            if (KnownImperativeAuth.ContainsKey(controller))
            {
                continue;
            }

            (classAuthorize || actionAuthorize).Should().BeTrue(
                "{0}.{1} has no [Authorize], no [AllowAnonymous], and no KnownImperativeAuth entry — " +
                "deny-by-default requires an explicit, reviewed decision at the HTTP boundary",
                controller.Name, action.Name);
        }
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void EveryStateChangingActionDeclaresItsAntiforgeryDisposition(Type controller)
    {
        // Management API controllers authenticate with a backoffice bearer token, not an ambient
        // cookie — no cross-site-forgeable credential, so cookie antiforgery is N/A by framework.
        if (typeof(ManagementApiControllerBase).IsAssignableFrom(controller))
        {
            return;
        }

        var classAutoValidate = controller.GetCustomAttribute<AutoValidateAntiforgeryTokenAttribute>() is not null;
        var classIgnores = controller.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>() is not null;

        foreach (var action in controller
                     .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(IsAction)
                     .Where(IsStateChanging))
        {
            if (KnownImperativeAntiforgery.ContainsKey((controller, action.Name)))
            {
                continue;
            }

            var validates = classAutoValidate
                            || action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null;
            var ignores = classIgnores
                          || action.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>() is not null;

            (validates || ignores).Should().BeTrue(
                "{0}.{1} changes state — SEC-PT2-009 requires an explicit [ValidateAntiForgeryToken] or " +
                "[IgnoreAntiforgeryToken] with a rationale",
                controller.Name, action.Name);
        }
    }

    private static bool IsAction(MethodInfo m) =>
        !m.IsSpecialName
        && m.GetCustomAttribute<NonActionAttribute>() is null
        && m.DeclaringType?.Namespace?.StartsWith("UmbracoPrism") == true;

    private static bool IsStateChanging(MethodInfo m)
    {
        var verbs = m.GetCustomAttributes<HttpMethodAttribute>()
            .SelectMany(a => a.HttpMethods)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Every Prism controller decorates its actions with explicit verb attributes; treat a
        // verb-less action as a read (the RenderController render leg).
        return verbs.Contains("POST") || verbs.Contains("PUT") || verbs.Contains("DELETE") || verbs.Contains("PATCH");
    }
}
