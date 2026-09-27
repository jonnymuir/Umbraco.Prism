using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UmbracoPrism.Core.Composers;

/// <summary>
/// Registers the embedded GDS partials from UmbracoPrism.Core so that the
/// <c>&lt;prism-field&gt;</c> and <c>&lt;prism-component&gt;</c> tag helpers work out of the box after package install.
/// </summary>
/// <remarks>
/// <para>
/// Partials are embedded in the Core assembly at:
/// <c>Views/Partials/PrismFields/_Component-{Type}.cshtml</c>
/// <c>Views/Partials/PrismComponents/_PrismComponent-{Type}.cshtml</c>
/// <c>Views/Partials/_Stage-{StepType}.cshtml</c>
/// <c>Views/Partials/_ServiceRequestHub-{Type}.cshtml</c>
/// </para>
/// <para>
/// Physical files in the consuming project always take precedence — override any
/// built-in partial by placing your own file at the same path in your
/// <c>Views/Partials/</c> folder.
/// </para>
/// </remarks>
public class PrismPartialsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddTransient<IStartupFilter, PrismEmbeddedViewsStartupFilter>();
    }
}

/// <summary>
/// Startup filter that injects the embedded Prism partials into the
/// <see cref="Microsoft.AspNetCore.Hosting.IWebHostEnvironment.ContentRootFileProvider"/>
/// so Razor can locate them at runtime.
/// Physical files in the consuming project's Views/ folder are checked first
/// because they appear first in the <see cref="CompositeFileProvider"/>.
/// </summary>
internal sealed class PrismEmbeddedViewsStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
            var embedded = new EmbeddedFileProvider(
                typeof(PrismPartialsComposer).Assembly,
                "UmbracoPrism.Core");

            // Physical content root wins; embedded resources are the fallback.
            env.ContentRootFileProvider = new CompositeFileProvider(
                env.ContentRootFileProvider,
                embedded);

            next(app);
        };
    }
}
