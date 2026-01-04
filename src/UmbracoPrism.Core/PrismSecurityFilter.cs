using Umbraco.Cms.Api.Management.OpenApi;

namespace UmbracoPrism.Core;

/// <summary>
/// Security filter for the Prism Management API.
/// </summary>
public class PrismSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
{
    protected override string ApiName => "Prism";
}