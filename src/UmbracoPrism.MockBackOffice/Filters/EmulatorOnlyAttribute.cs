using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UmbracoPrism.MockBackOffice.Filters;

/// <summary>
/// Action filter that restricts endpoint access to Development environment only.
/// Returns 404 Not Found in non-Development environments to prevent accidental production exposure.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class EmulatorOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var env = context.HttpContext.RequestServices.GetService<IWebHostEnvironment>();
        
        if (env?.IsDevelopment() != true)
        {
            context.Result = new NotFoundResult();
            return;
        }

        base.OnActionExecuting(context);
    }
}
