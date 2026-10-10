using System.Security.Claims;

namespace UmbracoPrism.MockBusinessApp.Services.Members;

public sealed record RegisterMemberRequest(string? Name, string? Phone, string? ContactPreference);

/// <summary>
/// Membership for a person who has just created an account at the identity provider. The business
/// app never sees a password and never creates the login: the person registers with the provider,
/// then asks for a membership with the token that registration produced. Who they are, and which
/// tenant they join, come only from that token. The body carries only what they are telling us
/// about themselves, so it cannot name a tenant, a role or another person's record. The rules are in
/// <see cref="MemberRegistrar"/>. See docs/walkthroughs/member-registration.md.
/// </summary>
public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMembers(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/backoffice/members", Register).RequireAuthorization();
        return app;
    }

    private static IResult Register(RegisterMemberRequest body, ClaimsPrincipal user, IConfiguration config, MemberRegistrar registrar)
    {
        var caller = CallerIdentity.From(user, config);
        if (caller is null)
        {
            return Results.Forbid();
        }

        var result = registrar.Register(caller, body);
        return result.Status switch
        {
            RegistrationStatus.Created => Results.Json(Describe(caller, result.Member!, created: true), statusCode: StatusCodes.Status201Created),
            RegistrationStatus.AlreadyMember => Results.Ok(Describe(caller, result.Member!, created: false)),
            RegistrationStatus.Invalid => Results.UnprocessableEntity(new { error = result.Message }),
            RegistrationStatus.Refused => Results.Json(new { error = result.Code, message = result.Message }, statusCode: StatusCodes.Status403Forbidden),
            _ => Results.Json(new { error = result.Code, message = result.Message }, statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }

    private static object Describe(CallerIdentity caller, BackOfficeMember member, bool created) => new
    {
        registered = true,
        created,
        tenant = caller.Tenant.DisplayName,
        tenantCode = member.TenantCode,
        backOfficeId = member.BackOfficeId,
        role = member.Role,
    };
}
