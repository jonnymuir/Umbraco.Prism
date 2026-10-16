using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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
            return Member(JugglingMember(request));
        }

        if (string.Equals(blueprint.DefinitionKey, TestSiteSeedContract.MoneyModellerBlueprintSlug, StringComparison.OrdinalIgnoreCase))
        {
            return Member(SavingsMember(request));
        }

        return null;
    }

    private static Dictionary<string, object?> Member(Dictionary<string, object?> member) => new() { ["member"] = member };

    private Dictionary<string, object?> JugglingMember(ServiceRequest request)
    {
        // Read from the live request rather than the scoped PrismUserContext, which a singleton
        // can't hold. Gated on IsAuthenticated because request.UserId is the applicant's real email
        // only for a signed-in member; for an anonymous visitor it's an opaque correlation-cookie
        // GUID, which must never be suggested back to them as their email. Both are null with no
        // live HttpContext (an automation callback resuming the request), which defaultFrom treats
        // as "no suggestion".
        var user = httpContextAccessor.HttpContext?.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
        return new()
        {
            ["tier"] = membershipClient.GetForUser(request.UserId).Tier,
            ["name"] = isAuthenticated ? user!.FindFirstValue("name") : null,
            ["email"] = isAuthenticated ? request.UserId : null,
        };
    }

    private Dictionary<string, object?> SavingsMember(ServiceRequest request)
    {
        var record = memberRecordService.GetForUser(request.UserId);
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
