using UmbracoPrism.MockBusinessApp.Services.Profile;

namespace UmbracoPrism.MockBusinessApp.Services.Members;

public enum RegistrationStatus { Created, AlreadyMember, Refused, Invalid, Full }

/// <summary>What happened to a registration. <see cref="Code"/> and <see cref="Message"/> are set when it was not accepted.</summary>
public sealed record RegistrationResult(RegistrationStatus Status, BackOfficeMember? Member = null, string Code = "", string Message = "");

/// <summary>
/// The rules for registering a person, kept apart from the HTTP layer so they read top to bottom:
/// an existing member is returned untouched, then the tenant's and the identity provider's
/// conditions, then the details, then the registry. The caller's identity is already resolved from
/// the validated token; nothing here takes a tenant, role or id from the request.
/// </summary>
public sealed class MemberRegistrar(MemberDirectory directory, ProfileStore profiles)
{
    private const int MaxNameLength = 80;

    public RegistrationResult Register(CallerIdentity caller, RegisterMemberRequest body)
    {
        // Safe to call again: a member who is already registered gets their record back, so a journey
        // that lost the response can simply retry. Nothing about an existing member is overwritten.
        if (directory.Find(caller) is { } existing)
        {
            return new RegistrationResult(RegistrationStatus.AlreadyMember, existing);
        }

        if (WhyNotAllowed(caller) is { } refusal)
        {
            return refusal;
        }

        var (name, phone, preference) = Tidy(body);
        if (WhyInvalid(name, phone, preference) is { } problem)
        {
            return new RegistrationResult(RegistrationStatus.Invalid, Message: problem);
        }

        if (directory.Register(caller, name) is not { } registration)
        {
            return new RegistrationResult(RegistrationStatus.Full, Code: "directory-full", Message: "We cannot take new members right now.");
        }

        var member = directory.ToBackOfficeMember(registration.Member);
        if (!registration.Created)
        {
            return new RegistrationResult(RegistrationStatus.AlreadyMember, member);
        }

        profiles.Set(member.TenantCode, member.BackOfficeId, new MemberProfile(phone, preference));
        return new RegistrationResult(RegistrationStatus.Created, member);
    }

    private static (string Name, string Phone, string Preference) Tidy(RegisterMemberRequest body) =>
        (body.Name?.Trim() ?? "", body.Phone?.Trim() ?? "", body.ContactPreference?.Trim().ToLowerInvariant() ?? "");

    private RegistrationResult? WhyNotAllowed(CallerIdentity caller)
    {
        // A provider that has not confirmed the email cannot be used to claim it: anyone can type
        // someone else's address into a registration form.
        if (!caller.EmailVerified)
        {
            return new RegistrationResult(RegistrationStatus.Refused, Code: "email-not-verified", Message: "Verify your email address, then try again.");
        }

        return directory.AllowsSelfRegistration(caller) && caller.Subject.Length > 0
            ? null
            : new RegistrationResult(RegistrationStatus.Refused, Code: "self-registration-closed", Message: "This organisation does not accept online registration.");
    }

    private static string? WhyInvalid(string name, string phone, string preference) =>
        name.Length is 0 or > MaxNameLength || name.Any(char.IsControl)
            ? $"Enter your name, up to {MaxNameLength} characters."
            : ProfileEndpoints.ValidateContactDetails(phone, preference);
}
