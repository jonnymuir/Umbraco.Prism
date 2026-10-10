using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.MockBusinessApp.Services.Profile;

/// <summary>
/// A member's contact details as the (mock) business system holds them. Keyed by tenant AND
/// email, so the same person registered under two tenants has two independent records.
/// </summary>
public sealed record MemberProfile(string Phone, string ContactPreference);

/// <summary>In-memory, process-lifetime store for the profile demo. A restart resets every record.</summary>
public sealed class ProfileStore
{
    private readonly ConcurrentDictionary<(string TenantCode, string Email), MemberProfile> _profiles = new();

    public MemberProfile Get(string tenantCode, string email) =>
        _profiles.GetValueOrDefault((tenantCode, email.ToLowerInvariant()), new MemberProfile("", "email"));

    public void Set(string tenantCode, string email, MemberProfile profile) =>
        _profiles[(tenantCode, email.ToLowerInvariant())] = profile;
}

public sealed record UpdateProfileRequest(string? Phone, string? ContactPreference);

/// <summary>
/// The authenticated half of the "update my details" example: the member's own bearer token is the
/// only thing identifying who is asking and which tenant they belong to. Nothing in the request
/// body or query names either, so a caller cannot read or write another member's record, or the
/// same email's record under another tenant. See docs/walkthroughs/authenticated-business-app-call.md.
/// </summary>
public static partial class ProfileEndpoints
{
    private static readonly string[] ContactPreferences = ["email", "phone", "post"];

    [GeneratedRegex(@"^\+?[0-9 ]{7,20}$")]
    private static partial Regex PhonePattern();

    public static IEndpointRouteBuilder MapProfile(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/backoffice/profile", GetProfile).RequireAuthorization();
        app.MapPut("/api/backoffice/profile", UpdateProfile).RequireAuthorization();
        return app;
    }

    private static IResult GetProfile(ClaimsPrincipal user, IConfiguration config, ProfileStore store)
    {
        var member = ResolveMember(user, config, out var tenant);
        if (member is null)
        {
            // Authenticated and in a known tenant, but not a registered member there: a normal
            // business outcome the journey routes on, not an error.
            return Results.Ok(new { registered = false, tenant = tenant?.DisplayName });
        }

        var profile = store.Get(member.TenantCode, member.Email);
        return Results.Ok(new
        {
            registered = true,
            tenant = tenant!.DisplayName,
            tenantCode = member.TenantCode,
            name = member.Email.Split('@')[0],
            email = member.Email,
            role = member.Role,
            phone = profile.Phone,
            contactPreference = profile.ContactPreference
        });
    }

    private static IResult UpdateProfile(UpdateProfileRequest body, ClaimsPrincipal user, IConfiguration config, ProfileStore store)
    {
        var member = ResolveMember(user, config, out _);
        if (member is null)
        {
            return Results.Forbid();
        }

        var phone = body.Phone?.Trim() ?? "";
        if (phone.Length > 0 && !PhonePattern().IsMatch(phone))
        {
            return Results.UnprocessableEntity(new { error = "Enter a telephone number in the correct format, like 01632 960 001." });
        }

        var preference = body.ContactPreference?.Trim().ToLowerInvariant() ?? "";
        if (!ContactPreferences.Contains(preference))
        {
            return Results.UnprocessableEntity(new { error = "Choose how we should contact you: email, phone or post." });
        }

        if (preference == "phone" && phone.Length == 0)
        {
            return Results.UnprocessableEntity(new { error = "Enter a telephone number if you want us to contact you by phone." });
        }

        store.Set(member.TenantCode, member.Email, new MemberProfile(phone, preference));
        return Results.Ok(new { reference = $"UPD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", tenantCode = member.TenantCode });
    }

    private static BackOfficeMember? ResolveMember(ClaimsPrincipal user, IConfiguration config, out BackOfficeTenant? tenant)
    {
        tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));
        var email = user.GetEmail();
        if (tenant is null || string.IsNullOrEmpty(email))
        {
            return null;
        }

        var tenantCode = tenant.Code;
        return config.GetSection("PrismBusinessApp:Members").Get<List<BackOfficeMember>>()?
            .FirstOrDefault(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && m.TenantCode == tenantCode);
    }
}
