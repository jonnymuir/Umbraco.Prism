using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace UmbracoPrism.Core;

/// <summary>
/// Configuration for the Prism Management API Swagger documentation.
/// </summary>
public class PrismManagementApiConfiguration : IConfigureOptions<SwaggerGenOptions>
{
    /// <summary>
    /// Configures the Swagger documentation for the Prism Management API.
    /// </summary>
    /// <param name="options"></param>
    public void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc("Prism", new OpenApiInfo 
        { 
            Title = "Prism Management API", 
            Version = "v1" 
        });

        options.OperationFilter<PrismSecurityFilter>();
    }
}