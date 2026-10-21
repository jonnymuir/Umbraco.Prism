using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmbracoPrism.Core.Controllers.SavedPages;

/// <summary>
/// Serves the saved pages service worker (<c>prism-offline-sw.js</c>, beside this file) from the
/// site root, where its scope can cover the whole site; a worker served from <c>App_Plugins</c>
/// could only control pages under that path. Embedded in the assembly, so it is served the same
/// way however the host serves its static files.
/// </summary>
[AllowAnonymous]
public class PrismSavedPagesController : Controller
{
    private static readonly Lazy<byte[]> Worker = new(() =>
    {
        using var stream = typeof(PrismSavedPagesController).Assembly.GetManifestResourceStream("SavedPages/prism-offline-sw.js")
            ?? throw new InvalidOperationException("The saved pages worker is missing from the assembly.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    });

    [HttpGet("/prism-offline-sw.js")]
    public IActionResult ServiceWorker()
    {
        // Checked on every registration, so a new version reaches apps on their next launch.
        Response.Headers.CacheControl = "no-cache";
        return File(Worker.Value, "text/javascript");
    }
}
