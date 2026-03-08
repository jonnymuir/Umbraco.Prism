namespace UmbracoPrism.Core.Controllers.Models;

public class PrismMobileBundleRequest
{
    public string? AppName { get; set; }
    public string? AppId { get; set; }
    public string? Version { get; set; }
    public string? StartUrl { get; set; }
    public string? UserAgentMarker { get; set; }
    public string? IconUrl { get; set; }
    public string? SplashUrl { get; set; }
    public string? ErrorBackgroundColor { get; set; }
    public string? ErrorTextColor { get; set; }
    public string? ErrorTitle { get; set; }
    public string? ErrorMessage { get; set; }
    public bool? ShowErrorDiagnostics { get; set; }
}
