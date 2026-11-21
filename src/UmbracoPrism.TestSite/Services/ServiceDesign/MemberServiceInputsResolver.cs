using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.Core.Services;
using Wayfinder.Engine.Models;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// Supplies the <c>member</c> service field for the two demo blueprints that read the signed-in
/// member's own records: the juggling licence (Society membership tier, name, email) and the money
/// modeller (the member's savings record). Every other blueprint's service fields keep whatever
/// value the request already holds, which Wayfinder supplies without this resolver.
/// </summary>
public sealed class MemberServiceInputsResolver(
    IJugglingSocietyMembershipClient membershipClient,
    IMemberSavingsRecordService memberRecordService,
    IHttpContextAccessor httpContextAccessor) : IServiceInputsResolver
{
    public IReadOnlyDictionary<string, object?>? Resolve(ServiceRequest request, ServiceBlueprint blueprint, StageDefinition stage)
    {
        if (string.Equals(blueprint.DefinitionKey, TestSiteSeedContract.JugglingLicenceBlueprintSlug, StringComparison.OrdinalIgnoreCase))
        {
            return Member(JugglingMember());
        }

        if (string.Equals(blueprint.DefinitionKey, TestSiteSeedContract.MoneyModellerBlueprintSlug, StringComparison.OrdinalIgnoreCase))
        {
            return Member(SavingsMember(request));
        }

        return null;
    }

    private static Dictionary<string, object?> Member(Dictionary<string, object?> member) => new() { ["member"] = member };

    /// <summary>
    /// The signed-in member's identity-provider-verified email, read from the live request rather than
    /// the scoped <see cref="IPrismUserContext"/>, which a singleton can't hold. The demo's systems of
    /// record are keyed by email, while <c>request.UserId</c> is the member's stable subject (or, for an
    /// anonymous visitor, an opaque correlation-cookie GUID that must never be suggested back as their
    /// email). Null with no live HttpContext (an automation callback resuming the request), which
    /// <c>defaultFrom</c> treats as "no suggestion".
    /// </summary>
    private string? VerifiedEmail() =>
        httpContextAccessor.HttpContext?.RequestServices.GetService<IPrismUserContext>()?.Email;

    private Dictionary<string, object?> JugglingMember()
    {
        var email = VerifiedEmail();
        var user = httpContextAccessor.HttpContext?.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
        return new()
        {
            ["tier"] = membershipClient.GetForUser(email ?? string.Empty).Tier,
            ["name"] = isAuthenticated ? user!.FindFirstValue("name") : null,
            ["email"] = email,
        };
    }

    private Dictionary<string, object?> SavingsMember(ServiceRequest request)
    {
        var record = memberRecordService.GetForUser(VerifiedEmail() ?? request.UserId);
        return new()
        {
            ["name"] = record.Name,
            ["active"] = record.Active,
            ["age"] = record.Age,
            ["salary"] = record.Salary,
            ["accruedPension"] = record.AccruedPension,
            ["accruedLump"] = record.AccruedLump,
            ["dcPot"] = record.DcPot,
        };
    }
}
