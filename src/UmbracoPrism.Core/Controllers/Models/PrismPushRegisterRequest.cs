namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// Request body for the push token registration endpoint.
/// </summary>
public class PrismPushRegisterRequest
{
    /// <summary>Gets or sets the Firebase Cloud Messaging device push token.</summary>
    public string PushToken { get; set; } = string.Empty;
}
