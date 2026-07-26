using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace UmbracoPrism.Core.TagHelpers;

[HtmlTargetElement("prism-touchpoint-form")]
public class PrismTouchpointFormTagHelper(IAntiforgery antiforgery) : TagHelper
{
    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    [HtmlAttributeName("instance-id")]
    public string InstanceId { get; set; } = string.Empty;

    [HtmlAttributeName("state-version")]
    public int StateVersion { get; set; }

    [HtmlAttributeName("blueprint-key")]
    public string BlueprintKey { get; set; } = string.Empty;

    [HtmlAttributeName("return-url")]
    public string ReturnUrl { get; set; } = string.Empty;

    [HtmlAttributeName("nonce")]
    public string Nonce { get; set; } = string.Empty;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "form";
        output.TagMode = TagMode.StartTagAndEndTag;

        output.Attributes.SetAttribute("class", "prism-touchpoint");
        output.Attributes.SetAttribute("method", "post");
        output.Attributes.SetAttribute("action", ReturnUrl);
        output.Attributes.SetAttribute("novalidate", "novalidate");
        // Always multipart, not just when the current stage happens to render a file-upload
        // field: without this, a stage containing <input type="file"> silently submits it empty
        // under the default urlencoded encoding — no validation error, no file ever reaches
        // Request.Form.Files, nothing to distinguish it from an ordinary field going through fine.
        output.Attributes.SetAttribute("enctype", "multipart/form-data");

        var tokens = antiforgery.GetAndStoreTokens(ViewContext.HttpContext);
        var antiforgeryHtml = $@"<input type=""hidden"" name=""__RequestVerificationToken"" value=""{tokens.RequestToken}"" />";

        var hiddenFields = $@"
{antiforgeryHtml}
    <input type=""hidden"" name=""InstanceId"" value=""{InstanceId}"" />
    <input type=""hidden"" name=""StateVersion"" value=""{StateVersion}"" />
    <input type=""hidden"" name=""BlueprintKey"" value=""{BlueprintKey}"" />
    <input type=""hidden"" name=""ReturnUrl"" value=""{ReturnUrl}"" />
    <input type=""hidden"" name=""Nonce"" value=""{Nonce}"" />";

        output.PreContent.SetHtmlContent(hiddenFields);

        var childContent = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(childContent);
    }
}
