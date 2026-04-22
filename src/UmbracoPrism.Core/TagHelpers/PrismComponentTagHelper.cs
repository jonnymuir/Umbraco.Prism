using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.TagHelpers;

/// <summary>
/// Renders a Prism workflow component by dispatching to a convention-based Razor partial.
/// </summary>
/// <remarks>
/// <para>
/// Usage: &lt;prism-component component="@comp" errors="@Model.FieldErrors" values="@Model.FormValues"
///   return-url="@Model.ReturnUrl" instance-id="@Model.InstanceId" state-version="@Model.StateVersion"
///   workflow-key="@Model.WorkflowKey" nonce="@Model.Nonce" /&gt;
/// </para>
/// <para>
/// For a component with Type = "fieldset", the tag helper looks for
/// ~/Views/Partials/PrismComponents/_PrismComponent-Fieldset.cshtml.
/// If that view does not exist, it falls back to
/// ~/Views/Partials/PrismComponents/_PrismComponent-Default.cshtml.
/// </para>
/// <para>
/// Type normalisation: kebab-case is converted to PascalCase.
/// "summary-list" → "SummaryList", "notification-banner" → "NotificationBanner".
/// </para>
/// </remarks>
[HtmlTargetElement("prism-component")]
public class PrismComponentTagHelper : TagHelper
{
    private const string PartialsBase    = "~/Views/Partials/PrismComponents/";
    private const string FallbackPartial = $"{PartialsBase}_PrismComponent-Default.cshtml";

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

        var partial = ResolvePartial(Component.Type);
        var content = await _htmlHelper.PartialAsync(partial, ctx);

        output.Content.SetHtmlContent(content);
    }

    /// <summary>
    /// Resolves the partial name for a given component type using the naming convention.
    /// Falls back to _PrismComponent-Default.cshtml if no specific partial exists.
    /// </summary>
    private string ResolvePartial(string? componentType)
    {
        var typeName = KebabToPascalCase(componentType ?? "default");
        var candidate = $"{PartialsBase}_PrismComponent-{typeName}.cshtml";

        var result = _viewEngine.GetView(
            executingFilePath: ViewContext.ExecutingFilePath,
            viewPath:          candidate,
            isMainPage:        false);

        return result.Success ? candidate : FallbackPartial;
    }

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
