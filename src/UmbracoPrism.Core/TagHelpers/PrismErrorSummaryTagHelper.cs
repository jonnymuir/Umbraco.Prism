using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.TagHelpers;

[HtmlTargetElement("prism-error-summary")]
public class PrismErrorSummaryTagHelper : TagHelper
{
    [HtmlAttributeName("problems")]
    public IReadOnlyList<WorkflowProblem>? Problems { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Problems == null || Problems.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "prism-error-summary");
        output.Attributes.SetAttribute("role", "alert");
        output.Attributes.SetAttribute("aria-labelledby", "prism-error-summary-title");
        output.Attributes.SetAttribute("tabindex", "-1");

        var sb = new StringBuilder();
        sb.AppendLine(@"<h2 class=""prism-error-summary__title"" id=""prism-error-summary-title"">");
        sb.AppendLine("        There is a problem");
        sb.AppendLine("    </h2>");
        sb.AppendLine(@"    <ul class=""prism-error-summary__list"">");

        foreach (var problem in Problems)
        {
            sb.Append("        <li>");
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

        sb.AppendLine("    </ul>");

        output.Content.SetHtmlContent(sb.ToString());
    }
}
