using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Wayfinder.Umbraco;
using Wayfinder.Umbraco.Controllers;
using Wayfinder.Umbraco.Models;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite.Controllers;

/// <summary>
/// Umbraco route-hijacking controller for the <c>publicServiceRequestPage</c> document type — TestSite's
/// own anonymous-first, in-Umbraco-only service blueprint demo's front-end runtime surface.
/// Ready to use as-is: unlike the business-workflow demo pattern (which needs a per-host
/// subclass, e.g. <see cref="StagePageController"/>, for claims-based field pre-population),
/// this demo drives its own service-sourced field defaults declaratively (see
/// <see cref="UmbracoProcessManagerEngine"/>'s <c>serviceInputsResolver</c>), so no per-host
/// override is needed here — a host only has to create a <c>publicServiceRequestPage</c> content node
/// and set its <c>blueprintKey</c> property.
/// </summary>
/// <remarks>
/// Anonymous-first: <see cref="RequiresAuthentication"/> is overridden to <see langword="false"/>
/// so an unauthenticated visitor is never redirected to login — this demo's journeys are public
/// by default (a GDS-style "apply for..." flow), while still resolving richer data for a
/// logged-in Prism Member via <see cref="Services.ServiceDesign.InProcessPublicVisitorProcessManagerClient"/>'s
/// identity resolution.
/// </remarks>
public class PublicServiceRequestPageController(
    ILogger<RenderController> logger,
    ICompositeViewEngine compositeViewEngine,
    IUmbracoContextAccessor umbracoContextAccessor,
    [FromKeyedServices(WayfinderUmbracoServiceKeys.InProcessQueueClient)] IBusinessAppProcessManagerClient workflowClient,
    IPublishedValueFallback publishedValueFallback,
    IAntiforgery antiforgery,
    IStageNonceService nonceService,
    IServiceRequestFieldValidator fieldValidator,
    IServiceRequestFileStorage fileStorage,
    IUploadTokenService uploadTokenService)
    : ServiceRequestPageController<ServiceRequestPageViewModel>(
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
