using Microsoft.AspNetCore.Authorization;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Authorization marker requirement for Prism administrative actions.
/// </summary>
public class PrismAdminRequirement : IAuthorizationRequirement
{
}
