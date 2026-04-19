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

        // Readonly handling
        var readonlyAttr = Field.ReadOnly ? @" readonly aria-readonly=""true""" : string.Empty;
        var readonlyCssClass = Field.ReadOnly ? " govuk-input--readonly" : string.Empty;

        var minLengthAttr = Field.MinLength.HasValue ? $@" minlength=""{Field.MinLength.Value}""" : string.Empty;
        var maxLengthAttr = Field.MaxLength.HasValue ? $@" maxlength=""{Field.MaxLength.Value}""" : string.Empty;
        var patternAttr = !string.IsNullOrEmpty(Field.Pattern) ? $@" pattern=""{Encode(Field.Pattern)}""" : string.Empty;
        var minAttr = Field.Min.HasValue ? $@" min=""{Field.Min.Value}""" : string.Empty;
        var maxAttr = Field.Max.HasValue ? $@" max=""{Field.Max.Value}""" : string.Empty;

        // Conditional field attributes
        var isConditional = !string.IsNullOrEmpty(Field.ConditionalOn);
        var conditionalClass = isConditional ? " prism-field--conditional" : string.Empty;
        var conditionalAttrs = isConditional
            ? $@" data-conditional-on=""{Encode(Field.ConditionalOn)}"" data-visible-when=""{Encode(Field.VisibleWhen ?? "")}"""
            : string.Empty;
        var hiddenAttr = isConditional ? " hidden" : string.Empty;
        var ariaHiddenAttr = isConditional ? @" aria-hidden=""true""" : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($@"<div class=""govuk-form-group {(hasFieldError ? "govuk-form-group--error" : "")}{conditionalClass}""{conditionalAttrs}{hiddenAttr}{ariaHiddenAttr}>");

        switch (fieldType)
        {
            case "boolean":
                RenderCheckbox(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, ariaRequired, ariaInvalid, describedBy, readonlyAttr);
                break;
            case "radio":
                RenderRadio(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, ariaRequired, ariaInvalid);
                break;
            case "checkboxlist":
                RenderCheckboxList(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, ariaRequired, ariaInvalid);
                break;
            case "select":
                RenderSelect(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, ariaRequired, ariaInvalid, describedBy, readonlyAttr, readonlyCssClass);
                break;
            case "textarea":
                RenderTextarea(sb, Field, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, minLengthAttr, maxLengthAttr, ariaRequired, ariaInvalid, describedBy, readonlyAttr, readonlyCssClass);
                break;
            default:
                RenderInput(sb, Field, fieldType, hasHint, hintId, hasFieldError, fieldError, errorId, requiredAttr, minLengthAttr, maxLengthAttr, patternAttr, minAttr, maxAttr, ariaRequired, ariaInvalid, describedBy, readonlyAttr, readonlyCssClass);
                break;
        }

        sb.AppendLine("</div>");

        output.Content.SetHtmlContent(sb.ToString());
    }

    private void RenderCheckbox(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string ariaRequired, string ariaInvalid, string describedBy, string readonlyAttr)
    {
        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = !string.IsNullOrWhiteSpace(field.DefaultValue) ? field.DefaultValue : submittedValue;
        var isChecked = currentValue != null
            ? "true".Equals(currentValue, StringComparison.OrdinalIgnoreCase)
            : (field.Value is true || "true".Equals(field.Value?.ToString(), StringComparison.OrdinalIgnoreCase));

        sb.AppendLine(@"    <div class=""govuk-checkboxes"" data-module=""govuk-checkboxes"">");
        sb.AppendLine(@"        <div class=""govuk-checkboxes__item"">");
        sb.Append($@"            <input class=""govuk-checkboxes__input"" type=""checkbox"" id=""{Encode(field.FieldKey)}"" name=""fields[{Encode(field.FieldKey)}]"" value=""true"" data-label=""{Encode(field.Label)}""");
        if (isChecked) sb.Append(" checked");
        sb.Append(requiredAttr);
        sb.Append(ariaRequired);
        sb.Append(ariaInvalid);
        sb.Append(describedBy);
        if (field.ReadOnly) sb.Append(@" disabled");
        sb.AppendLine(" />");
        sb.Append($@"            <label class=""govuk-label govuk-checkboxes__label"" for=""{Encode(field.FieldKey)}"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""govuk-visually-hidden"">(required)</span>");
        sb.AppendLine("</label>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");

        if (hasHint) sb.AppendLine($@"    <div class=""govuk-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"    <p class=""govuk-error-message"" id=""{errorId}""><span class=""govuk-visually-hidden"">Error:</span> {Encode(fieldError!)}</p>");
    }

    private void RenderRadio(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string ariaRequired, string ariaInvalid)
    {
        sb.AppendLine(@"    <fieldset class=""govuk-fieldset"">");
        sb.Append($@"        <legend class=""govuk-fieldset__legend"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""govuk-visually-hidden"">(required)</span>");
        sb.AppendLine("</legend>");

        if (hasHint) sb.AppendLine($@"        <div class=""govuk-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"        <p class=""govuk-error-message"" id=""{errorId}""><span class=""govuk-visually-hidden"">Error:</span> {Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = submittedValue ?? field.Value?.ToString();

        sb.AppendLine(@"        <div class=""govuk-radios"" data-module=""govuk-radios"">");
        if (field.Options != null)
        {
            foreach (var option in field.Options)
            {
                var radioId = $"opt-{field.FieldKey}-{option.ToLowerInvariant().Replace(" ", "-")}";
                var isChecked = option.Equals(currentValue, StringComparison.OrdinalIgnoreCase);

                sb.AppendLine(@"            <div class=""govuk-radios__item"">");
                sb.Append($@"                <input class=""govuk-radios__input"" type=""radio"" id=""{Encode(radioId)}"" name=""fields[{Encode(field.FieldKey)}]"" value=""{Encode(option)}"" data-label=""{Encode(field.Label)}""");
                if (isChecked) sb.Append(" checked");
                sb.Append(requiredAttr);
                sb.Append(ariaRequired);
                sb.Append(ariaInvalid);
                sb.AppendLine(" />");
                sb.AppendLine($@"                <label class=""govuk-label govuk-radios__label"" for=""{Encode(radioId)}"">{Encode(option)}</label>");
                sb.AppendLine("            </div>");
            }
        }
        sb.AppendLine("        </div>");

        sb.AppendLine("    </fieldset>");
    }

    private void RenderCheckboxList(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string ariaRequired, string ariaInvalid)
    {
        sb.AppendLine(@"    <fieldset class=""govuk-fieldset"">");
        sb.Append($@"        <legend class=""govuk-fieldset__legend"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""govuk-visually-hidden"">(required)</span>");
        sb.AppendLine("</legend>");

        if (hasHint) sb.AppendLine($@"        <div class=""govuk-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"        <p class=""govuk-error-message"" id=""{errorId}""><span class=""govuk-visually-hidden"">Error:</span> {Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = submittedValue ?? field.Value?.ToString();
        var checkedValues = currentValue?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();

        sb.AppendLine(@"        <div class=""govuk-checkboxes"" data-module=""govuk-checkboxes"">");
        if (field.Options != null)
        {
            foreach (var option in field.Options)
            {
                var cbId = $"opt-{field.FieldKey}-{option.ToLowerInvariant().Replace(" ", "-")}";
                var isChecked = checkedValues.Contains(option, StringComparer.OrdinalIgnoreCase);

                sb.AppendLine(@"            <div class=""govuk-checkboxes__item"">");
                sb.Append($@"                <input class=""govuk-checkboxes__input"" type=""checkbox"" id=""{Encode(cbId)}"" name=""fields[{Encode(field.FieldKey)}]"" value=""{Encode(option)}"" data-label=""{Encode(field.Label)}""");
                if (isChecked) sb.Append(" checked");
                sb.Append(requiredAttr);
                sb.Append(ariaRequired);
                sb.Append(ariaInvalid);
                sb.AppendLine(" />");
                sb.AppendLine($@"                <label class=""govuk-label govuk-checkboxes__label"" for=""{Encode(cbId)}"">{Encode(option)}</label>");
                sb.AppendLine("            </div>");
            }
        }
        sb.AppendLine("        </div>");

        sb.AppendLine("    </fieldset>");
    }

    private void RenderSelect(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string ariaRequired, string ariaInvalid, string describedBy, string readonlyAttr, string readonlyCssClass)
    {
        sb.Append($@"    <label class=""govuk-label"" for=""{Encode(field.FieldKey)}"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""govuk-visually-hidden"">(required)</span>");
        sb.AppendLine("</label>");

        if (hasHint) sb.AppendLine($@"    <div class=""govuk-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"    <p class=""govuk-error-message"" id=""{errorId}""><span class=""govuk-visually-hidden"">Error:</span> {Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = !string.IsNullOrWhiteSpace(field.DefaultValue) ? field.DefaultValue : submittedValue ?? field.Value?.ToString();

        if (field.ReadOnly)
        {
            sb.AppendLine($@"    <div class=""govuk-body"">{Encode(currentValue ?? "")}</div>");
            sb.AppendLine($@"    <input type=""hidden"" name=""fields[{Encode(field.FieldKey)}]"" value=""{Encode(currentValue ?? "")}"">");
        }
        else
        {
            sb.Append($@"    <select class=""govuk-select{readonlyCssClass}"" id=""{Encode(field.FieldKey)}"" name=""fields[{Encode(field.FieldKey)}]"" data-label=""{Encode(field.Label)}""");
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
    }

    private void RenderTextarea(StringBuilder sb, FieldRenderPayload field, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string minLengthAttr, string maxLengthAttr, string ariaRequired, string ariaInvalid, string describedBy, string readonlyAttr, string readonlyCssClass)
    {
        sb.Append($@"    <label class=""govuk-label"" for=""{Encode(field.FieldKey)}"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""govuk-visually-hidden"">(required)</span>");
        sb.AppendLine("</label>");

        if (hasHint) sb.AppendLine($@"    <div class=""govuk-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"    <p class=""govuk-error-message"" id=""{errorId}""><span class=""govuk-visually-hidden"">Error:</span> {Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = !string.IsNullOrWhiteSpace(field.DefaultValue) ? field.DefaultValue : submittedValue ?? field.Value?.ToString() ?? "";

        sb.Append($@"    <textarea class=""govuk-textarea{readonlyCssClass}"" id=""{Encode(field.FieldKey)}"" name=""fields[{Encode(field.FieldKey)}]"" rows=""5"" data-label=""{Encode(field.Label)}""");
        sb.Append(requiredAttr);
        sb.Append(minLengthAttr);
        sb.Append(maxLengthAttr);
        sb.Append(ariaRequired);
        sb.Append(ariaInvalid);
        sb.Append(describedBy);
        sb.Append(readonlyAttr);
        sb.Append(">");
        sb.Append(Encode(currentValue));
        sb.AppendLine("</textarea>");
    }

    private void RenderInput(StringBuilder sb, FieldRenderPayload field, string fieldType, bool hasHint, string hintId, bool hasFieldError, string? fieldError, string errorId, string requiredAttr, string minLengthAttr, string maxLengthAttr, string patternAttr, string minAttr, string maxAttr, string ariaRequired, string ariaInvalid, string describedBy, string readonlyAttr, string readonlyCssClass)
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

        sb.Append($@"    <label class=""govuk-label"" for=""{Encode(field.FieldKey)}"">{Encode(field.Label)}");
        if (field.Required) sb.Append(@"<span class=""govuk-visually-hidden"">(required)</span>");
        sb.AppendLine("</label>");

        if (hasHint) sb.AppendLine($@"    <div class=""govuk-hint"" id=""{hintId}"">{Encode(field.Hint!)}</div>");
        if (hasFieldError) sb.AppendLine($@"    <p class=""govuk-error-message"" id=""{errorId}""><span class=""govuk-visually-hidden"">Error:</span> {Encode(fieldError!)}</p>");

        var submittedValue = Values?.GetValueOrDefault(field.FieldKey);
        var currentValue = !string.IsNullOrWhiteSpace(field.DefaultValue) ? field.DefaultValue : submittedValue ?? field.Value?.ToString() ?? "";

        var constraintAttrs = string.Empty;
        if (inputType == "text" || inputType == "email")
        {
            constraintAttrs = minLengthAttr + maxLengthAttr + patternAttr;
        }
        else if (inputType == "number")
        {
            constraintAttrs = minAttr + maxAttr;
        }

        sb.Append($@"    <input class=""govuk-input{readonlyCssClass}"" type=""{inputType}"" id=""{Encode(field.FieldKey)}"" name=""fields[{Encode(field.FieldKey)}]"" value=""{Encode(currentValue)}"" data-label=""{Encode(field.Label)}""");
        sb.Append(step);
        sb.Append(requiredAttr);
        sb.Append(constraintAttrs);
        sb.Append(ariaRequired);
        sb.Append(ariaInvalid);
        sb.Append(describedBy);
        sb.Append(readonlyAttr);
        sb.AppendLine(" />");
    }

    private static string Encode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? "");
}
