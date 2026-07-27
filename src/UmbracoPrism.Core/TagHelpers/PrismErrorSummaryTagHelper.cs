using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.TagHelpers;

[HtmlTargetElement("prism-error-summary")]
public class PrismErrorSummaryTagHelper : TagHelper
{
    [HtmlAttributeName("problems")]
    public IReadOnlyList<ServiceRequestProblem>? Problems { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Problems == null || Problems.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "govuk-error-summary");
        output.Attributes.SetAttribute("data-module", "govuk-error-summary");

        var sb = new StringBuilder();
        sb.AppendLine(@"<div role=""alert"">");
        sb.AppendLine(@"    <h2 class=""govuk-error-summary__title"">There is a problem</h2>");
        sb.AppendLine(@"    <div class=""govuk-error-summary__body"">");
        sb.AppendLine(@"        <ul class=""govuk-list govuk-error-summary__list"">");

        foreach (var problem in Problems)
        {
            sb.Append("            <li>");
            if (!string.IsNullOrEmpty(problem.FieldKey))
            {
                sb.Append($@"<a href=""#{System.Net.WebUtility.HtmlEncode(problem.FieldKey)}"">{System.Net.WebUtility.HtmlEncode(problem.Message)}</a>");
            }
            else
            {
                sb.Append(System.Net.WebUtility.HtmlEncode(problem.Message));
            }
            sb.AppendLine("</li>");
        }

        sb.AppendLine("        </ul>");
        sb.AppendLine("    </div>");
        sb.AppendLine("</div>");

        output.Content.SetHtmlContent(sb.ToString());
    }
}
