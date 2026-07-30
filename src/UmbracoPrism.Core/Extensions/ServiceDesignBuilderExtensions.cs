using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoPrism.Core.Services;
using Wayfinder.Umbraco.Extensions;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Extension methods for registering the Prism Workflow Engine services.
/// </summary>
public static class ServiceDesignBuilderExtensions
{
    /// <summary>
    /// Registers the Prism Workflow Engine services with the DI container.
    /// This is the primary integration point for the new Business App-centric workflow architecture.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    /// <returns>The Umbraco builder for method chaining.</returns>
    /// <remarks>
    /// Registers <see cref="IBusinessAppProcessManagerClient"/> (scoped) — Prism's own HTTP client
    /// for calling a remote Business App — plus <see cref="WayfinderUmbracoServiceCollectionExtensions.AddWayfinderUmbraco"/>,
    /// the generic Umbraco-hosted service-design infrastructure (nonce, field validation, file
    /// upload, content sanitization) every <c>ServiceRequestPageController{TViewModel}</c> flow
    /// needs regardless of which client implementation a host uses.
    ///
    /// The Business App is the authoritative source for all workflow state and definitions.
    /// Umbraco uses this client to ask "what's the next step?" and to submit collected data.
    ///
    /// In development, configure <c>PrismBusinessApp:ApiBaseUrl</c> to the local HTTPS endpoint
    /// (e.g. <c>https://localhost:7245</c>) so browser and server-side flows share the same trusted origin.
    /// </remarks>
    public static IUmbracoBuilder AddPrismProcessManager(this IUmbracoBuilder builder)
    {
        builder.Services.AddHttpClient("PrismBusinessApp");
        builder.Services.AddScoped<IBusinessAppProcessManagerClient, BusinessAppProcessManagerClient>();

        builder.Services.AddWayfinderUmbraco();

        return builder;
    }
}
