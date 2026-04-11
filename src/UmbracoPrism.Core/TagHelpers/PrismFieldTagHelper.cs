using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.TagHelpers;

[HtmlTargetElement("prism-field")]
public class PrismFieldTagHelper : TagHelper
{
    [HtmlAttributeName("field")]
    public FieldRenderPayload? Field { get; set; }

    [HtmlAttributeName("errors")]
    public IReadOnlyDictionary<string, string>? Errors { get; set; }

    [HtmlAttributeName("values")]
    public IReadOnlyDictionary<string, string>? Values { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Field == null)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;

        var fieldError = Errors?.ContainsKey(Field.FieldKey) == true ? Errors[Field.FieldKey] : null;
        var hasFieldError = !string.IsNullOrEmpty(fieldError);
        var hasHint = !string.IsNullOrEmpty(Field.Hint);
        var hintId = $"{Field.FieldKey}-hint";
        var errorId = $"{Field.FieldKey}-error";

        var describedByParts = new List<string>();
        if (hasHint) describedByParts.Add(hintId);
        if (hasFieldError) describedByParts.Add(errorId);
        var describedBy = describedByParts.Count > 0 ? $@" aria-describedby=""{string.Join(" ", describedByParts)}""" : string.Empty;

        var ariaRequired = Field.Required ? @" aria-required=""true""" : string.Empty;
        var ariaInvalid = hasFieldError ? @" aria-invalid=""true""" : string.Empty;
        var requiredAttr = Field.Required ? " required" : string.Empty;
        var fieldType = Field.FieldType?.ToLowerInvariant() ?? "text";

        var minLengthAttr = Field.MinLength.HasValue ? $@" minlength=""{Field.MinLength.Value}""" : string.Empty;
        var maxLengthAttr = Field.MaxLength.HasValue ? $@" maxlength=""{Field.MaxLength.Value}""" : string.Empty;
        var patternAttr = !string.IsNullOrEmpty(Field.Pattern) ? $@" pattern=""{Encode(Field.Pattern)}""" : string.Empty;
        var minAttr = Field.Min.HasValue ? $@" min=""{Field.Min.Value}""" : string.Empty;
        var maxAttr = Field.Max.HasValue ? $@" max=""{Field.Max.Value}""" : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($@"<div class=""prism-form-group {(hasFieldError ? "prism-form-group--error" : "")}"">");

        switch (fieldType)
        {
            case "boolean":
                RenderCheckbox(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, ariaRequired, ariaInvalid, describedBy);
                break;
            case "radio":
                RenderRadio(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, ariaRequired, ariaInvalid);
                break;
            case "checkboxlist":
                RenderCheckboxList(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, ariaRequired, ariaInvalid);
                break;
            case "select":
                RenderSelect(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, ariaRequired, ariaInvalid, describedBy);
                break;
            case "textarea":
                RenderTextarea(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, minLengthAttr, maxLengthAttr, ariaRequired, ariaInvalid, describedBy);
                break;
            default:
                RenderInput(sb, Field, fieldType, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, minLengthAttr, maxLengthAttr, patternAttr, minAttr, maxAttr, ariaRequired, ariaInvalid, describedBy);
                break;
        }

        sb.AppendLine("</div>");

        output.Content.SetHtmlContent(sb.ToString());
    }

    private void RenderCheckbox(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string ariaRequired, string ariaInvalid, string describedBy)
    {
        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var isChecked = submittedValue != null
            ? "true".Equals(submittedValue, StringComparison.OrdinalIgnoreCase)
            : (field.Value is true || "true".Equals(field.Value?.ToString(), StringComparison.OrdinalIgnoreCase));

        sb.AppendLine(@"    <div class=""prism-form-group__checkbox-wrapper"">");
        sb.Append($@"        <input class=""prism-checkbox"" type=""checkbox"" id=""{Encode(field.FieldKey)}"" name=""fields[{Encode(field.FieldKey)}]"" value=""true""");
        if (isChecked) sb.Append(" checked");
        sb.Append(requiredAttr);
        sb.Append(ariaRequired);
        sb.Append(ariaInvalid);
        sb.Append(describedBy);
        sb.AppendLine(" />");
        sb.Append($@"        <label class=""prism-label prism-label--inline"" for=""{Encode(field.FieldKey)}"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""prism-required"" aria-hidden=""true"">*</span>");
        sb.AppendLine("</label>");
        sb.AppendLine("    </div>");

        if (hasHint) sb.AppendLine($@"    <div class=""prism-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"    <p class=""prism-field-error"" id=""{errorId}"" role=""alert"">{Encode(fieldError!)}</p>");
    }

    private void RenderRadio(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string ariaRequired, string ariaInvalid)
    {
        sb.AppendLine(@"    <fieldset class=""prism-fieldset"">");
        sb.Append($@"        <legend class=""prism-legend"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""prism-required"" aria-hidden=""true"">*</span>");
        sb.AppendLine("</legend>");

        if (hasHint) sb.AppendLine($@"        <div class=""prism-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"        <p class=""prism-field-error"" id=""{errorId}"" role=""alert"">{Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = submittedValue ?? field.Value?.ToString();

        if (field.Options != null)
        {
            foreach (var option in field.Options)
            {
                var radioId = $"{field.FieldKey}-{option.ToLowerInvariant().Replace(" ", "-")}";
                var isChecked = option.Equals(currentValue, StringComparison.OrdinalIgnoreCase);

                sb.AppendLine(@"        <div class=""prism-radio-item"">");
                sb.Append($@"            <input class=""prism-radio"" type=""radio"" id=""{Encode(radioId)}"" name=""fields[{Encode(field.FieldKey)}]"" value=""{Encode(option)}""");
                if (isChecked) sb.Append(" checked");
                sb.Append(requiredAttr);
                sb.Append(ariaRequired);
                sb.Append(ariaInvalid);
                sb.AppendLine(" />");
                sb.AppendLine($@"            <label class=""prism-label prism-label--inline"" for=""{Encode(radioId)}"">{Encode(option)}</label>");
                sb.AppendLine("        </div>");
            }
        }

        sb.AppendLine("    </fieldset>");
    }

    private void RenderCheckboxList(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string ariaRequired, string ariaInvalid)
    {
        sb.AppendLine(@"    <fieldset class=""prism-fieldset"">");
        sb.Append($@"        <legend class=""prism-legend"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""prism-required"" aria-hidden=""true"">*</span>");
        sb.AppendLine("</legend>");

        if (hasHint) sb.AppendLine($@"        <div class=""prism-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"        <p class=""prism-field-error"" id=""{errorId}"" role=""alert"">{Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = submittedValue ?? field.Value?.ToString();
        var checkedValues = currentValue?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();

        if (field.Options != null)
        {
            foreach (var option in field.Options)
            {
                var cbId = $"{field.FieldKey}-{option.ToLowerInvariant().Replace(" ", "-")}";
                var isChecked = checkedValues.Contains(option, StringComparer.OrdinalIgnoreCase);

                sb.AppendLine(@"        <div class=""prism-checkbox-item"">");
                sb.Append($@"            <input class=""prism-checkbox"" type=""checkbox"" id=""{Encode(cbId)}"" name=""fields[{Encode(field.FieldKey)}]"" value=""{Encode(option)}""");
                if (isChecked) sb.Append(" checked");
                sb.Append(requiredAttr);
                sb.Append(ariaRequired);
                sb.Append(ariaInvalid);
                sb.AppendLine(" />");
                sb.AppendLine($@"            <label class=""prism-label prism-label--inline"" for=""{Encode(cbId)}"">{Encode(option)}</label>");
                sb.AppendLine("        </div>");
            }
        }

        sb.AppendLine("    </fieldset>");
    }

    private void RenderSelect(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string ariaRequired, string ariaInvalid, string describedBy)
    {
        sb.Append($@"    <label class=""prism-label"" for=""{Encode(field.FieldKey)}"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""prism-required"" aria-hidden=""true"">*</span>");
        sb.AppendLine("</label>");

        if (hasHint) sb.AppendLine($@"    <div class=""prism-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"    <p class=""prism-field-error"" id=""{errorId}"" role=""alert"">{Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = submittedValue ?? field.Value?.ToString();

        sb.Append($@"    <select class=""prism-select"" id=""{Encode(field.FieldKey)}"" name=""fields[{Encode(field.FieldKey)}]""");
        sb.Append(requiredAttr);
        sb.Append(ariaRequired);
        sb.Append(ariaInvalid);
        sb.Append(describedBy);
        sb.AppendLine(">");
        sb.AppendLine(@"        <option value="""">-- Select --</option>");

        if (field.Options != null)
        {
            foreach (var option in field.Options)
            {
                var isSelected = option.Equals(currentValue, StringComparison.OrdinalIgnoreCase);
                sb.Append($@"        <option value=""{Encode(option)}""");
                if (isSelected) sb.Append(" selected");
                sb.AppendLine($">{Encode(option)}</option>");
            }
        }

        sb.AppendLine("    </select>");
    }

    private void RenderTextarea(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string minLengthAttr, string maxLengthAttr, string ariaRequired, string ariaInvalid, string describedBy)
    {
        sb.Append($@"    <label class=""prism-label"" for=""{Encode(field.FieldKey)}"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""prism-required"" aria-hidden=""true"">*</span>");
        sb.AppendLine("</label>");

        if (hasHint) sb.AppendLine($@"    <div class=""prism-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"    <p class=""prism-field-error"" id=""{errorId}"" role=""alert"">{Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = submittedValue ?? field.Value?.ToString() ?? "";

        sb.Append($@"    <textarea class=""prism-textarea"" id=""{Encode(field.FieldKey)}"" name=""fields[{Encode(field.FieldKey)}]"" rows=""5""");
        sb.Append(requiredAttr);
        sb.Append(minLengthAttr);
        sb.Append(maxLengthAttr);
        sb.Append(ariaRequired);
        sb.Append(ariaInvalid);
        sb.Append(describedBy);
        sb.Append(">");
        sb.Append(Encode(currentValue));
        sb.AppendLine("</textarea>");
    }

    private void RenderInput(StringBuilder sb, FieldRenderPayload field, string fieldType, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string minLengthAttr, string maxLengthAttr, string patternAttr, string minAttr, string maxAttr, string ariaRequired, string ariaInvalid, string describedBy)
    {
        var inputType = fieldType switch
        {
            "email" => "email",
            "number" or "decimal" => "number",
            "date" => "date",
            "datetime" => "datetime-local",
            _ => "text"
        };
        var step = fieldType == "decimal" ? @" step=""any""" : string.Empty;

        sb.Append($@"    <label class=""prism-label"" for=""{Encode(field.FieldKey)}"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""prism-required"" aria-hidden=""true"">*</span>");
        sb.AppendLine("</label>");

        if (hasHint) sb.AppendLine($@"    <div class=""prism-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"    <p class=""prism-field-error"" id=""{errorId}"" role=""alert"">{Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = submittedValue ?? field.Value?.ToString() ?? "";

        var constraintAttrs = string.Empty;
        if (inputType == "text" || inputType == "email")
        {
            constraintAttrs = minLengthAttr + maxLengthAttr + patternAttr;
        }
        else if (inputType == "number")
        {
            constraintAttrs = minAttr + maxAttr;
        }

        sb.Append($@"    <input class=""prism-input"" type=""{inputType}"" id=""{Encode(field.FieldKey)}"" name=""fields[{Encode(field.FieldKey)}]"" value=""{Encode(currentValue)}""");
        sb.Append(step);
        sb.Append(requiredAttr);
        sb.Append(constraintAttrs);
        sb.Append(ariaRequired);
        sb.Append(ariaInvalid);
        sb.Append(describedBy);
        sb.AppendLine(" />");
    }

    private static string Encode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? "");
}
