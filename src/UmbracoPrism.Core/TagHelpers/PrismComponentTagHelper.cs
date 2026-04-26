using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Net;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.TagHelpers;

/// <summary>
/// Renders a Prism workflow component (container) or field (input) by dispatching to a
/// convention-based Razor partial.
/// </summary>
/// <remarks>
/// <para>
/// Container usage: &lt;prism-component component="@comp" errors="@Model.FieldErrors" values="@Model.FormValues"
///   return-url="@Model.ReturnUrl" instance-id="@Model.InstanceId" state-version="@Model.StateVersion"
///   workflow-key="@Model.WorkflowKey" nonce="@Model.Nonce" /&gt;
/// </para>
/// <para>
/// Input field usage: &lt;prism-component field="@field" errors="@Model.FieldErrors" values="@Model.FormValues" /&gt;
/// (legacy &lt;prism-field&gt; element name is also supported for backwards compatibility).
/// </para>
/// <para>
/// For a component with Type = "fieldset", the tag helper looks for
/// ~/Views/Partials/PrismComponents/_PrismComponent-Fieldset.cshtml.
/// If that view does not exist, it falls back to
/// ~/Views/Partials/PrismComponents/_PrismComponent-Default.cshtml.
/// </para>
/// <para>
/// For a field with FieldType = "text", the tag helper looks for
/// ~/Views/Partials/PrismFields/_Component-Text.cshtml, falling back to
/// ~/Views/Partials/PrismFields/_Component-Default.cshtml.
/// </para>
/// <para>
/// Type normalisation: kebab-case is converted to PascalCase.
/// "summary-list" → "SummaryList", "notification-banner" → "NotificationBanner".
/// </para>
/// </remarks>
[HtmlTargetElement("prism-component")]
[HtmlTargetElement("prism-field")]
public class PrismComponentTagHelper : TagHelper
{
    private const string ComponentsBase        = "~/Views/Partials/PrismComponents/";
    private const string ComponentsFallback    = $"{ComponentsBase}_PrismComponent-Default.cshtml";
    private const string FieldsBase            = "~/Views/Partials/PrismFields/";
    private const string FieldsFallback        = $"{FieldsBase}_Component-Default.cshtml";

    private readonly IHtmlHelper          _htmlHelper;
    private readonly ICompositeViewEngine _viewEngine;

    public PrismComponentTagHelper(IHtmlHelper htmlHelper, ICompositeViewEngine viewEngine)
    {
        _htmlHelper  = htmlHelper;
        _viewEngine  = viewEngine;
    }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    [HtmlAttributeName("component")]
    public PrismComponentRenderPayload? Component { get; set; }

    [HtmlAttributeName("field")]
    public FieldRenderPayload? Field { get; set; }

    [HtmlAttributeName("errors")]
    public IReadOnlyDictionary<string, string>? Errors { get; set; }

    [HtmlAttributeName("values")]
    public IReadOnlyDictionary<string, string>? Values { get; set; }

    [HtmlAttributeName("return-url")]
    public string ReturnUrl { get; set; } = string.Empty;

    [HtmlAttributeName("instance-id")]
    public string InstanceId { get; set; } = string.Empty;

    [HtmlAttributeName("state-version")]
    public int StateVersion { get; set; }

    [HtmlAttributeName("workflow-key")]
    public string WorkflowKey { get; set; } = string.Empty;

    [HtmlAttributeName("nonce")]
    public string Nonce { get; set; } = string.Empty;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        if (Field is not null)
        {
            await ProcessFieldAsync(output);
            return;
        }

        if (Component is null)
        {
            output.SuppressOutput();
            return;
        }

        ((IViewContextAware)_htmlHelper).Contextualize(ViewContext);

        var ctx = new PrismComponentContext
        {
            Component    = Component,
            Errors       = Errors       ?? new Dictionary<string, string>(),
            Values       = Values       ?? new Dictionary<string, string>(),
            ReturnUrl    = ReturnUrl,
            InstanceId   = InstanceId,
            StateVersion = StateVersion,
            WorkflowKey  = WorkflowKey,
            Nonce        = Nonce
        };

        var partial = ResolveComponentPartial(Component.Type);
        var content = await _htmlHelper.PartialAsync(partial, ctx);

        output.Content.SetHtmlContent(content);
    }

    private async Task ProcessFieldAsync(TagHelperOutput output)
    {
        ((IViewContextAware)_htmlHelper).Contextualize(ViewContext);

        var fieldType = (Field!.FieldType ?? "text").ToLowerInvariant();

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
        var partial    = ResolveFieldPartial(fieldType);
        var content    = await _htmlHelper.PartialAsync(partial, ctx);

        output.Content.SetHtmlContent(content);
    }

    /// <summary>
    /// Resolves the partial name for a given component type using the naming convention.
    /// Falls back to _PrismComponent-Default.cshtml if no specific partial exists.
    /// </summary>
    private string ResolveComponentPartial(string? componentType)
    {
        var typeName  = KebabToPascalCase(componentType ?? "default");
        var candidate = $"{ComponentsBase}_PrismComponent-{typeName}.cshtml";
        return ViewExists(candidate) ? candidate : ComponentsFallback;
    }

    /// <summary>
    /// Resolves the partial name for a given field type using the naming convention.
    /// Falls back to _Component-Default.cshtml if no specific partial exists.
    /// </summary>
    private string ResolveFieldPartial(string fieldType)
    {
        var typeName = string.IsNullOrEmpty(fieldType)
            ? "Default"
            : char.ToUpperInvariant(fieldType[0]) + fieldType[1..];

        var candidate = $"{FieldsBase}_Component-{typeName}.cshtml";
        return ViewExists(candidate) ? candidate : FieldsFallback;
    }

    private bool ViewExists(string viewPath)
    {
        var result = _viewEngine.GetView(
            executingFilePath: ViewContext.ExecutingFilePath,
            viewPath:          viewPath,
            isMainPage:        false);
        return result.Success;
    }

    /// <summary>
    /// Renders content-only field types that are not form controls.
    /// Returns null for standard field types that use the partial system.
    /// </summary>
    private string? RenderInlineFieldType(string fieldType)
    {
        var content = Field!.Content;
        var encodedContent = string.IsNullOrEmpty(content) ? string.Empty : WebUtility.HtmlEncode(content);
        var encodedLabel = string.IsNullOrEmpty(Field.Label) ? string.Empty : WebUtility.HtmlEncode(Field.Label);
        var bannerTitleId = string.IsNullOrEmpty(Field.FieldKey)
            ? "prism-inline-banner-title"
            : $"prism-inline-banner-title-{SanitizeIdFragment(Field.FieldKey)}";

        return fieldType switch
        {
            "inset-text" when !string.IsNullOrEmpty(content) =>
                $@"<div class=""govuk-inset-text"">{encodedContent}</div>",

            "warning-text" when !string.IsNullOrEmpty(content) =>
                $@"<div class=""govuk-warning-text"">
  <span class=""govuk-warning-text__icon"" aria-hidden=""true"">!</span>
  <strong class=""govuk-warning-text__text"">
    <span class=""govuk-visually-hidden"">Warning</span>
    {encodedContent}
  </strong>
</div>",

            "details" when !string.IsNullOrEmpty(content) =>
                $@"<details class=""govuk-details"">
  <summary class=""govuk-details__summary"">
    <span class=""govuk-details__summary-text"">{(string.IsNullOrEmpty(encodedLabel) ? "More information" : encodedLabel)}</span>
  </summary>
  <div class=""govuk-details__text"">{encodedContent}</div>
</details>",

            "notification-banner" when !string.IsNullOrEmpty(content) =>
                $@"<div class=""govuk-notification-banner"" role=""region"" aria-labelledby=""{bannerTitleId}"">
  <div class=""govuk-notification-banner__header"">
    <h2 class=""govuk-notification-banner__title"" id=""{bannerTitleId}"">{(string.IsNullOrEmpty(encodedLabel) ? "Information" : encodedLabel)}</h2>
  </div>
  <div class=""govuk-notification-banner__content"">
    <p class=""govuk-body"">{encodedContent}</p>
  </div>
</div>",

            "body" when !string.IsNullOrEmpty(content) =>
                $@"<p class=""govuk-body"">{encodedContent}</p>",

            "heading" when !string.IsNullOrEmpty(content) =>
                $@"<h2 class=""govuk-heading-m"">{encodedContent}</h2>",

            "inset-text" or "warning-text" or "details" or "notification-banner" or "body" or "heading"
                => string.Empty, // content was null/empty — suppress

            _ => null // use the partial dispatch system
        };
    }

    private static string SanitizeIdFragment(string value) =>
        string.Concat(value.Select(c => char.IsLetterOrDigit(c) ? c : '-'));

    /// <summary>
    /// Converts a kebab-case string to PascalCase.
    /// "summary-list" → "SummaryList", "fieldset" → "Fieldset".
    /// </summary>
    private static string KebabToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return "Default";

        var parts = input.Split('-');
        return string.Concat(parts.Select(p =>
            string.IsNullOrEmpty(p) ? "" : char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
