using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Models.ServiceDesign;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.ServiceDesign;
using Wayfinder.Models.ServiceDesign;
using UmbracoPrism.TestSite.Models;

namespace UmbracoPrism.TestSite.Controllers;

/// <summary>
/// Umbraco route-hijacking controller for the <c>stagePage</c> document type.
/// Extends <see cref="PrismServiceRequestPageController{TViewModel}"/> to provide claims-based field pre-population.
/// </summary>
/// <remarks>
/// Requires an authenticated PrismMemberCookie session; unauthenticated requests are challenged by the framework.
/// </remarks>
public class StagePageController(
    ILogger<RenderController> logger,
    ICompositeViewEngine compositeViewEngine,
    IUmbracoContextAccessor umbracoContextAccessor,
    IBusinessAppProcessManagerClient processManagerClient,
    IPublishedValueFallback publishedValueFallback,
    IAntiforgery antiforgery,
    IStageNonceService nonceService,
    IServiceRequestFieldValidator fieldValidator,
    IServiceRequestFileStorage fileStorage,
    IUploadTokenService uploadTokenService)
    : PrismServiceRequestPageController<StageViewModel>(
        logger,
        compositeViewEngine,
        umbracoContextAccessor,
        processManagerClient,
        publishedValueFallback,
        antiforgery,
        nonceService,
        fieldValidator,
        fileStorage,
        uploadTokenService)
{
    /// <summary>
    /// Pre-populates stage fields from authenticated user claims.
    /// Sets DefaultValue and ReadOnly properties on email-address and full-name fields
    /// if the corresponding claims exist.
    /// </summary>
    protected override ServiceRequestResponseEnvelope PrePopulateFields(ServiceRequestResponseEnvelope envelope)
    {
        if (envelope.Render == null)
            return envelope;

        var email = HttpContext.User.FindFirstValue(ClaimTypes.Email)
            ?? HttpContext.User.FindFirstValue("email");
        var name = HttpContext.User.FindFirstValue(ClaimTypes.Name)
            ?? HttpContext.User.FindFirstValue("name");

        var updatedComponents = envelope.Render.Components
            .Select(component => component with
            {
                Fields = component.Fields.Select(field =>
                {
                    if (field.FieldKey == "email-address" && !string.IsNullOrWhiteSpace(email))
                    {
                        return field with
                        {
                            DefaultValue = email,
                            ReadOnly = true
                        };
                    }

                    if (field.FieldKey == "full-name" && !string.IsNullOrWhiteSpace(name))
                    {
                        return field with
                        {
                            DefaultValue = name,
                            ReadOnly = true
                        };
                    }

                    return field;
                }).ToList()
            }).ToList();

        var updatedRender = envelope.Render with
        {
            Components = updatedComponents
        };

        return envelope with { Render = updatedRender };
    }
}
