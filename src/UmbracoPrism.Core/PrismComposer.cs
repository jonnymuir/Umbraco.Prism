using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using UmbracoPrism.Core.Middleware;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoPrism.Core;

/// <summary>
/// Composer for registering Prism services, middleware, and migrations.
/// </summary>
public class PrismComposer : IComposer
{
    /// <summary>
    /// Compose method to register services, middleware, and migrations.
    /// </summary>
    /// <param name="builder"></param>
    public void Compose(IUmbracoBuilder builder)
    {
        // 1. Register Services
        builder.Services.AddScoped<IPrismContext, PrismContext>();
        builder.Services.AddSingleton<ITenantService, TenantService>();

        // 2. Register Middleware (This adds it to the pipeline)
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter(
                "PrismTenantResolution",
                applicationBuilder => applicationBuilder.UseMiddleware<PrismTenantMiddleware>()
            ));
        });

        // 3. Register Custom Database Tables (Migrations)
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, PrismMigrationHandler>();
    }
}