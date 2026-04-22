using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.TagHelpers;

/// <summary>
/// Renders a Prism workflow field by dispatching to a convention-based Razor partial.
/// </summary>
/// <remarks>
/// <para>
/// Usage: &lt;prism-field field="@field" errors="@Model.FieldErrors" values="@Model.FormValues" /&gt;
/// </para>
/// <para>
/// For a field with FieldType = "text", the tag helper looks for
/// ~/Views/Partials/PrismFields/_PrismField-Text.cshtml.
/// If that view does not exist, it falls back to
/// ~/Views/Partials/PrismFields/_PrismField-Default.cshtml.
/// </para>
/// <para>
/// To add a custom field type named "my-widget", create
/// Views/Partials/PrismFields/_PrismField-My-Widget.cshtml with
/// @model PrismFieldContext. No changes to Core are required.
/// </para>
/// </remarks>
[HtmlTargetElement("prism-field")]
public class PrismFieldTagHelper : TagHelper
{
    private const string PartialsBase    = "~/Views/Partials/PrismFields/";
    private const string FallbackPartial = $"{PartialsBase}_PrismField-Default.cshtml";

    private readonly IHtmlHelper          _htmlHelper;
    private readonly ICompositeViewEngine _viewEngine;

    public PrismFieldTagHelper(IHtmlHelper htmlHelper, ICompositeViewEngine viewEngine)
    {
        _htmlHelper  = htmlHelper;
        _viewEngine  = viewEngine;
    }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    [HtmlAttributeName("field")]
    public FieldRenderPayload? Field { get; set; }

    [HtmlAttributeName("errors")]
    public IReadOnlyDictionary<string, string>? Errors { get; set; }

    [HtmlAttributeName("values")]
    public IReadOnlyDictionary<string, string>? Values { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        if (Field is null)
        {
            output.SuppressOutput();
            return;
        }

        ((IViewContextAware)_htmlHelper).Contextualize(ViewContext);

        var fieldType = (Field.FieldType ?? "text").ToLowerInvariant();

        // Content-only field types rendered inline — they are not form controls
        // and do not need the govuk-form-group wrapper or the partial dispatch system.
        var inlineHtml = RenderInlineFieldType(fieldType);
        if (inlineHtml is not null)
        {
            output.Content.SetHtmlContent(inlineHtml);
            return;
        }

        var fieldError = Errors?.GetValueOrDefault(Field.FieldKey);
        var ctx        = PrismFieldContext.Build(Field, fieldError, Values);
        var partial    = ResolvePartial(fieldType);
        var content    = await _htmlHelper.PartialAsync(partial, ctx);

        output.Content.SetHtmlContent(content);
    }

    /// <summary>
    /// Resolves the partial name for a given field type using the naming convention.
    /// Falls back to _PrismField-Default.cshtml if no specific partial exists.
    /// </summary>
    private string ResolvePartial(string fieldType)
    {
        // Normalise: "text" -> "Text", "checkboxlist" -> "Checkboxlist"
        var typeName = string.IsNullOrEmpty(fieldType)
            ? "Default"
            : char.ToUpperInvariant(fieldType[0]) + fieldType[1..];

        var candidate = $"{PartialsBase}_PrismField-{typeName}.cshtml";

        // ICompositeViewEngine.GetView checks the physical file system first,
        // then falls through registered file providers (embedded resources etc.)
        var result = _viewEngine.GetView(
            executingFilePath: ViewContext.ExecutingFilePath,
            viewPath:          candidate,
            isMainPage:        false);

        return result.Success ? candidate : FallbackPartial;
    }

    /// <summary>
    /// Renders content-only field types that are not form controls.
    /// Returns null for standard field types that use the partial system.
    /// </summary>
    private string? RenderInlineFieldType(string fieldType)
    {
        var content = Field!.Content;

        return fieldType switch
        {
            "inset-text" when !string.IsNullOrEmpty(content) =>
                $@"<div class=""govuk-inset-text"">{System.Net.WebUtility.HtmlEncode(content)}</div>",

            "warning-text" when !string.IsNullOrEmpty(content) =>
                $@"<div class=""govuk-warning-text"">
  <span class=""govuk-warning-text__icon"" aria-hidden=""true"">!</span>
  <strong class=""govuk-warning-text__text"">
    <span class=""govuk-visually-hidden"">Warning</span>
    {System.Net.WebUtility.HtmlEncode(content)}
  </strong>
</div>",

            "inset-text" or "warning-text" => string.Empty, // content was null/empty — suppress

            _ => null // use the partial dispatch system
        };
    }
}
