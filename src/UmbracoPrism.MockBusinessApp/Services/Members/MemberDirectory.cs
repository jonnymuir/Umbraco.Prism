using System.Collections.Concurrent;

namespace UmbracoPrism.MockBusinessApp.Services.Members;

/// <summary>A member who registered themselves. Keyed by the identity provider's <c>sub</c>, never by email.</summary>
public sealed record RegisteredMember(string TenantCode, string Subject, string Email, string BackOfficeId, string Name);

/// <summary>
/// In-memory store of self-registered members for the process lifetime. Capped, so an authenticated
/// caller cannot grow it without bound; once full it refuses new members rather than evicting
/// existing ones, because evicting a member would silently de-register a real person.
/// </summary>
public sealed class MemberRegistry
{
    public const int MaxMembers = 500;

    private readonly ConcurrentDictionary<(string TenantCode, string Subject), RegisteredMember> _members = new();

    public RegisteredMember? Get(string tenantCode, string subject) =>
        _members.GetValueOrDefault((tenantCode, subject));

    /// <summary>Adds the member unless that person is already registered. <see langword="null"/> when the registry is full.</summary>
    public (RegisteredMember Member, bool Created)? GetOrAdd(RegisteredMember member)
    {
        if (_members.TryGetValue((member.TenantCode, member.Subject), out var existing))
        {
            return (existing, false);
        }

        if (_members.Count >= MaxMembers)
        {
            return null;
        }

        var stored = _members.GetOrAdd((member.TenantCode, member.Subject), member);
        return (stored, ReferenceEquals(stored, member));
    }
}

/// <summary>
/// Answers "is this caller a member, and which record is theirs?" from two sources: the directory
/// the business already holds (configuration here, a database in a real system) and people who
/// registered themselves. The two are matched differently on purpose. A pre-provisioned member is
/// matched by email, so only when the identity provider has verified that email; a self-registered
/// member is matched by the stable <c>sub</c>, so a later change of email at the provider cannot
/// hand their record to someone else.
/// </summary>
public sealed class MemberDirectory(IConfiguration config, MemberRegistry registry)
{
    public const string SelfRegisteredRole = "Member";

    public BackOfficeMember? Find(CallerIdentity caller)
    {
        if (caller.Subject.Length > 0 && registry.Get(caller.Tenant.Code, caller.Subject) is { } registered)
        {
            return ToBackOfficeMember(registered);
        }

        if (!caller.EmailVerified)
        {
            return null;
        }

        return config.GetSection("PrismBusinessApp:Members").Get<List<BackOfficeMember>>()?
            .FirstOrDefault(m => m.Email.Equals(caller.Email, StringComparison.OrdinalIgnoreCase) && m.TenantCode == caller.Tenant.Code);
    }

    public BackOfficeMember ToBackOfficeMember(RegisteredMember registered) =>
        new(registered.Email, registered.TenantCode, registered.BackOfficeId, SelfRegisteredRole);

    /// <summary>The self-registered record for this caller, when they have one (it carries the name they gave).</summary>
    public RegisteredMember? FindRegistered(CallerIdentity caller) =>
        caller.Subject.Length > 0 ? registry.Get(caller.Tenant.Code, caller.Subject) : null;

    /// <summary>Closed unless the tenant is listed in <c>PrismBusinessApp:SelfRegistration:Tenants</c>.</summary>
    public bool AllowsSelfRegistration(CallerIdentity caller) =>
        config.GetSection("PrismBusinessApp:SelfRegistration:Tenants").Get<string[]>()?
            .Contains(caller.Tenant.Code, StringComparer.Ordinal) == true;

    public (RegisteredMember Member, bool Created)? Register(CallerIdentity caller, string name) =>
        registry.GetOrAdd(new RegisteredMember(
            caller.Tenant.Code, caller.Subject, caller.Email, $"MBR-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", name));
}
