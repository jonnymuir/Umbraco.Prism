namespace UmbracoPrism.Core.Auth;

internal static class PrismReturnUrl
{
    private const string DefaultReturnUrl = "/";

    internal static string Normalize(string? returnUrl)
    {
        return IsLocalUrl(returnUrl) ? returnUrl! : DefaultReturnUrl;
    }

    internal static bool IsLocalUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl)
            && Microsoft.AspNetCore.Http.HttpResults.RedirectHttpResult.IsLocalUrl(returnUrl);
    }
}
