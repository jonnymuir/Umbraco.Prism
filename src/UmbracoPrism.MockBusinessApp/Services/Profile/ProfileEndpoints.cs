using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;
using UmbracoPrism.MockBusinessApp.Services.Members;

namespace UmbracoPrism.MockBusinessApp.Services.Profile;

/// <summary>
/// A member's contact details as the (mock) business system holds them. Keyed by tenant AND the
/// member's back-office id, so the same person registered under two tenants has two independent
/// records, and a record never follows an email address from one person to another.
/// </summary>
public sealed record MemberProfile(string Phone, string ContactPreference);

/// <summary>In-memory, process-lifetime store for the profile demo. A restart resets every record.</summary>
public sealed class ProfileStore
{
    private readonly ConcurrentDictionary<(string TenantCode, string BackOfficeId), MemberProfile> _profiles = new();

    public MemberProfile Get(string tenantCode, string backOfficeId) =>
        _profiles.GetValueOrDefault((tenantCode, backOfficeId), new MemberProfile("", "email"));

    public void Set(string tenantCode, string backOfficeId, MemberProfile profile) =>
        _profiles[(tenantCode, backOfficeId)] = profile;
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

    /// <summary>The message to show when the details are not acceptable, or <see langword="null"/> when they are.</summary>
    public static string? ValidateContactDetails(string phone, string preference)
    {
        if (phone.Length > 0 && !PhonePattern().IsMatch(phone))
        {
            return "Enter a telephone number in the correct format, like 01632 960 001.";
        }

        if (!ContactPreferences.Contains(preference))
        {
            return "Choose how we should contact you: email, phone or post.";
        }

        return preference == "phone" && phone.Length == 0
            ? "Enter a telephone number if you want us to contact you by phone."
            : null;
    }

    private static IResult GetProfile(ClaimsPrincipal user, IConfiguration config, MemberDirectory directory, ProfileStore store)
    {
        var caller = CallerIdentity.From(user, config);
        if (caller is null)
        {
            return Results.Forbid();
        }

        var member = directory.Find(caller);
        if (member is null)
        {
            // Authenticated and in a known tenant, but not a registered member there: a normal
            // business outcome the journey routes on, not an error.
            return Results.Ok(new { registered = false, tenant = caller.Tenant.DisplayName });
        }

        var profile = store.Get(member.TenantCode, member.BackOfficeId);
        return Results.Ok(new
        {
            registered = true,
            tenant = caller.Tenant.DisplayName,
            tenantCode = member.TenantCode,
            name = directory.FindRegistered(caller)?.Name ?? member.Email.Split('@')[0],
            email = member.Email,
            role = member.Role,
            phone = profile.Phone,
            contactPreference = profile.ContactPreference
        });
    }

    private static IResult UpdateProfile(UpdateProfileRequest body, ClaimsPrincipal user, IConfiguration config, MemberDirectory directory, ProfileStore store)
    {
        var caller = CallerIdentity.From(user, config);
        var member = caller is null ? null : directory.Find(caller);
        if (member is null)
        {
            return Results.Forbid();
        }

        var phone = body.Phone?.Trim() ?? "";
        var preference = body.ContactPreference?.Trim().ToLowerInvariant() ?? "";
        if (ValidateContactDetails(phone, preference) is { } error)
        {
            return Results.UnprocessableEntity(new { error });
        }

        store.Set(member.TenantCode, member.BackOfficeId, new MemberProfile(phone, preference));
        return Results.Ok(new { reference = $"UPD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", tenantCode = member.TenantCode });
    }
}
