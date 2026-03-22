using Microsoft.AspNetCore.Authorization;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Authorization marker requirement that enforces Prism tenant boundary checks.
/// </summary>
public class PrismTenantRequirement : IAuthorizationRequirement { }