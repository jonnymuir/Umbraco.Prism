using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Extension methods for registering the Prism Workflow Engine services.
/// </summary>
public static class WorkflowBuilderExtensions
{
    /// <summary>
    /// Registers the Prism Workflow Engine services with the DI container.
    /// This is the primary integration point for the new Business App-centric workflow architecture.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    /// <returns>The Umbraco builder for method chaining.</returns>
    /// <remarks>
    /// Registers:
    /// - <see cref="IBusinessAppWorkflowClient"/> (scoped) — HTTP client for calling the Business App
    /// 
    /// The Business App is the authoritative source for all workflow state and definitions.
    /// Umbraco uses this client to ask "what's the next step?" and to submit collected data.
    /// 
    /// In development, configure <c>PrismBusinessApp:WorkflowApiBaseUrl</c> to the HTTP endpoint
    /// (e.g. <c>http://localhost:5163</c>) to avoid self-signed certificate issues with localhost HTTPS.
    /// </remarks>
    public static IUmbracoBuilder AddPrismWorkflowEngine(this IUmbracoBuilder builder)
    {
        builder.Services.AddHttpClient("PrismBusinessApp");
        builder.Services.AddScoped<IBusinessAppWorkflowClient, BusinessAppWorkflowClient>();

        return builder;
    }
}
