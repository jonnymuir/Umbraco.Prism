using System.Net.Mail;
using System.Text.RegularExpressions;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Validates a workflow form submission against its authoritative field definitions.
/// Checks field key whitelist, required, type coercion, options whitelist, and constraints.
/// </summary>
public class WorkflowFieldValidator : IWorkflowFieldValidator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    /// <summary>
    /// Validates the submitted form values against the step's authoritative field definitions.
    /// </summary>
    /// <param name="authoritative">Field definitions from the nonce cache (server-authoritative).</param>
    /// <param name="submitted">Form values submitted by the client, keyed by field key.</param>
    public WorkflowValidationResult Validate(
        IReadOnlyList<FieldRenderPayload> authoritative,
        IReadOnlyDictionary<string, string> submitted)
    {
        var errors = new Dictionary<string, string>();

        // Build authoritative field key set (including checkboxlist variations)
        var authoritativeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in authoritative)
        {
            authoritativeKeys.Add(field.FieldKey);
            if (field.FieldType.Equals("checkboxlist", StringComparison.OrdinalIgnoreCase))
            {
                authoritativeKeys.Add($"{field.FieldKey}[]");
            }
        }

        // 1. Field key whitelist — reject unknown fields
        foreach (var submittedKey in submitted.Keys)
        {
            var normalizedKey = submittedKey.EndsWith("[]") ? submittedKey[..^2] : submittedKey;
            if (!authoritativeKeys.Contains(normalizedKey) && !authoritativeKeys.Contains(submittedKey))
            {
                errors[submittedKey] = $"{submittedKey}: Unknown field";
            }
        }

        // 2. Validate each authoritative field
        foreach (var field in authoritative)
        {
            // Already has an error from whitelist check? Skip.
            if (errors.ContainsKey(field.FieldKey))
            {
                continue;
            }

            // Get submitted value (handle checkboxlist suffix)
            var raw = GetSubmittedValue(field, submitted);

            // a. Required check
            if (field.Required && string.IsNullOrWhiteSpace(raw))
            {
                errors[field.FieldKey] = $"{field.Label} is required.";
                continue; // Don't cascade errors
            }

            // Skip further validation if value is empty (and not required)
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            // b. Type validation
            var typeError = ValidateType(field, raw);
            if (typeError != null)
            {
                errors[field.FieldKey] = typeError;
                continue; // Don't cascade errors
            }

            // c. Options whitelist
            var optionsError = ValidateOptions(field, raw);
            if (optionsError != null)
            {
                errors[field.FieldKey] = optionsError;
                continue; // Don't cascade errors
            }

            // d. Constraint checks
            var constraintError = ValidateConstraints(field, raw);
            if (constraintError != null)
            {
                errors[field.FieldKey] = constraintError;
                continue; // Don't cascade errors
            }
        }

        return errors.Count == 0
            ? WorkflowValidationResult.Pass()
            : WorkflowValidationResult.Fail(errors);
    }

    private static string GetSubmittedValue(FieldRenderPayload field, IReadOnlyDictionary<string, string> submitted)
    {
        if (submitted.TryGetValue(field.FieldKey, out var value))
        {
            return value;
        }

        // Check for checkboxlist suffix
        if (field.FieldType.Equals("checkboxlist", StringComparison.OrdinalIgnoreCase) &&
            submitted.TryGetValue($"{field.FieldKey}[]", out var suffixedValue))
        {
            return suffixedValue;
        }

        return string.Empty;
    }

    private static string? ValidateType(FieldRenderPayload field, string raw)
    {
        switch (field.FieldType.ToLowerInvariant())
        {
            case "number":
                if (!decimal.TryParse(raw, out _))
                {
                    return $"{field.Label} must be a number.";
                }
                break;

            case "email":
                try
                {
                    var addr = new MailAddress(raw);
                    if (addr.Address != raw)
                    {
                        return $"{field.Label} must be a valid email address.";
                    }
                }
                catch (FormatException)
                {
                    return $"{field.Label} must be a valid email address.";
                }
                break;

            case "date":
                if (!DateTime.TryParse(raw, out _))
                {
                    return $"{field.Label} must be a valid date.";
                }
                break;

            case "datetime":
                if (!DateTime.TryParse(raw, out _))
                {
                    return $"{field.Label} must be a valid date and time.";
                }
                break;
        }

        return null;
    }

    private static string? ValidateOptions(FieldRenderPayload field, string raw)
    {
        if (field.Options == null || field.Options.Count == 0)
        {
            return null;
        }

        var fieldType = field.FieldType.ToLowerInvariant();
        if (fieldType != "select" && fieldType != "radio" && fieldType != "checkboxlist")
        {
            return null;
        }

        var submittedValues = fieldType == "checkboxlist"
            ? raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { raw };

        foreach (var value in submittedValues)
        {
            if (!field.Options.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return $"{field.Label} contains an invalid selection.";
            }
        }

        return null;
    }

    private static string? ValidateConstraints(FieldRenderPayload field, string raw)
    {
        // MinLength check
        if (field.MinLength.HasValue && raw.Length < field.MinLength.Value)
        {
            return $"{field.Label} must be at least {field.MinLength.Value} characters.";
        }

        // MaxLength check
        if (field.MaxLength.HasValue && raw.Length > field.MaxLength.Value)
        {
            return $"{field.Label} must be no more than {field.MaxLength.Value} characters.";
        }

        // Pattern check
        if (!string.IsNullOrWhiteSpace(field.Pattern))
        {
            try
            {
                if (!Regex.IsMatch(raw, field.Pattern, RegexOptions.None, RegexTimeout))
                {
                    return $"{field.Label} is not in the expected format.";
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Pattern from BA is too complex or causes catastrophic backtracking
                return $"{field.Label} validation pattern is too complex to evaluate safely.";
            }
        }

        // Min/Max for number fields
        if (field.FieldType.Equals("number", StringComparison.OrdinalIgnoreCase) &&
            decimal.TryParse(raw, out var numericValue))
        {
            if (field.Min.HasValue && numericValue < field.Min.Value)
            {
                return $"{field.Label} must be at least {field.Min.Value}.";
            }

            if (field.Max.HasValue && numericValue > field.Max.Value)
            {
                return $"{field.Label} must be no more than {field.Max.Value}.";
            }
        }

        return null;
    }
}
