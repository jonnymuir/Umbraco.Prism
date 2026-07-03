using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoPrism.Core;
using uSync.BackOffice;

namespace UmbracoPrism.uSync;

[ComposeAfter(typeof(PrismComposer))]
public class PrismUSyncComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AdduSync();
    }
}
