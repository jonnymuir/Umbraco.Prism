using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Wayfinder.Umbraco.Controllers;
using Wayfinder.Umbraco.Models;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Umbraco route-hijacking controller for the <c>cmsServiceRequestPage</c> document type — the CMS
/// Workflow implementation's front-end runtime surface. Ready to use as-is: unlike the
/// business-workflow demo pattern (which needs a per-host subclass, e.g. TestSite's
/// <c>WorkflowPageController</c>, for claims-based field pre-population), a CMS Workflow
/// definition drives its own service-sourced field defaults declaratively (see
/// <see cref="Wayfinder.Umbraco.Services.UmbracoProcessManagerEngine"/>'s <c>serviceInputsResolver</c>), so no per-host override is
/// needed here — a host only has to create a <c>cmsServiceRequestPage</c> content node and set its
/// <c>blueprintKey</c> property.
/// </summary>
/// <remarks>
/// Anonymous-first: <see cref="RequiresAuthentication"/> is overridden to <see langword="false"/>
/// so an unauthenticated visitor is never redirected to login — CMS Workflow journeys are public
/// by default (a GDS-style "apply for..." flow), while still resolving richer data for a
/// logged-in Prism Member via <see cref="Services.ServiceDesign.InProcessCmsProcessManagerClient"/>'s identity resolution.
/// </remarks>
public class CmsServiceRequestPageController(
    ILogger<RenderController> logger,
    ICompositeViewEngine compositeViewEngine,
    IUmbracoContextAccessor umbracoContextAccessor,
    [FromKeyedServices("cms")] IBusinessAppProcessManagerClient workflowClient,
    IPublishedValueFallback publishedValueFallback,
    IAntiforgery antiforgery,
    IStageNonceService nonceService,
    IServiceRequestFieldValidator fieldValidator,
    IServiceRequestFileStorage fileStorage,
    IUploadTokenService uploadTokenService)
    : ServiceRequestPageController<PrismServiceRequestViewModel>(
        logger,
        compositeViewEngine,
        umbracoContextAccessor,
        workflowClient,
        publishedValueFallback,
        antiforgery,
        nonceService,
        fieldValidator,
        fileStorage,
        uploadTokenService)
{
    protected override bool RequiresAuthentication => false;
}
