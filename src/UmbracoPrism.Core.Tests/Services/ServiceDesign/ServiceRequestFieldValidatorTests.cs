using FluentAssertions;
using Wayfinder.Umbraco.Models;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.Core.Tests.Services.Workflow;

public class ServiceRequestFieldValidatorTests
{
    private static readonly ServiceRequestFieldValidator Validator = new();

    // ------------------------------------------------------------------ Happy Path

    [Fact]
    public void GivenAllRequiredFieldsProvided_WhenValid_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "name", Label = "Name", FieldType = "text", Required = true },
            new() { FieldKey = "email", Label = "Email", FieldType = "email", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["name"] = "Jane Doe",
            ["email"] = "jane@example.com"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GivenOptionalFieldsOmitted_WhenValid_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "name", Label = "Name", FieldType = "text", Required = true },
            new() { FieldKey = "bio", Label = "Bio", FieldType = "textarea", Required = false }
        };
        var submitted = new Dictionary<string, string>
        {
            ["name"] = "Jane"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("text", "Hello world")]
    [InlineData("email", "user@example.com")]
    [InlineData("number", "42")]
    [InlineData("select", "option1")]
    [InlineData("radio", "choice2")]
    [InlineData("checkboxlist", "item1,item2")]
    [InlineData("boolean", "true")]
    [InlineData("textarea", "Long text here")]
    [InlineData("date", "2024-04-10")]
    [InlineData("radios", "option1")]
    [InlineData("checkboxes", "item1")]
    [InlineData("currency", "1234.56")]
    [InlineData("currency", "999")]
    [InlineData("file", "somefile.pdf")]
    public void GivenFieldType_WhenValidValue_ThenIsValidTrue(string fieldType, string value)
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "field",
                Label = "Field",
                FieldType = fieldType,
                Required = true,
                Options = fieldType is "select" or "radio" or "checkboxlist" or "radios" or "checkboxes"
                    ? new List<string> { "option1", "choice2", "item1", "item2" }
                    : null
            }
        };
        var submitted = new Dictionary<string, string> { ["field"] = value };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenMinLengthConstraint_WhenExactlyAtMin_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "password", Label = "Password", FieldType = "text", Required = true, MinLength = 8 }
        };
        var submitted = new Dictionary<string, string> { ["password"] = "12345678" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenMaxLengthConstraint_WhenExactlyAtMax_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "code", Label = "Code", FieldType = "text", Required = true, MaxLength = 5 }
        };
        var submitted = new Dictionary<string, string> { ["code"] = "12345" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    // ------------------------------------------------------------------ Required Validation

    [Fact]
    public void GivenRequiredTextField_WhenEmpty_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "name", Label = "Name", FieldType = "text", Required = true }
        };
        var submitted = new Dictionary<string, string> { ["name"] = "" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("name");
        result.Errors["name"].Should().Be("Name is required.");
    }

    [Fact]
    public void GivenRequiredEmailField_WhenEmpty_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "email", Label = "Email", FieldType = "email", Required = true }
        };
        var submitted = new Dictionary<string, string> { ["email"] = "" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("email");
        result.Errors["email"].Should().Be("Email is required.");
    }

    [Fact]
    public void GivenRequiredSelectField_WhenEmpty_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "country",
                Label = "Country",
                FieldType = "select",
                Required = true,
                Options = new List<string> { "US", "UK", "CA" }
            }
        };
        var submitted = new Dictionary<string, string> { ["country"] = "" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("country");
        result.Errors["country"].Should().Be("Country is required.");
    }

    [Fact]
    public void GivenRequiredRadioField_WhenNotSelected_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "plan",
                Label = "Plan",
                FieldType = "radio",
                Required = true,
                Options = new List<string> { "basic", "pro", "enterprise" }
            }
        };
        var submitted = new Dictionary<string, string> { ["plan"] = "" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("plan");
        result.Errors["plan"].Should().Be("Plan is required.");
    }

    // ------------------------------------------------------------------ Type Validation

    [Theory]
    [InlineData("noatsign.com")]
    [InlineData("nodot@")]
    [InlineData("invalid")]
    public void GivenEmailField_WhenInvalidFormat_ThenValidationFails(string invalidEmail)
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "email", Label = "Email", FieldType = "email", Required = true }
        };
        var submitted = new Dictionary<string, string> { ["email"] = invalidEmail };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("email");
        result.Errors["email"].Should().Be("Email must be a valid email address.");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12.34.56")]
    [InlineData("not-a-number")]
    public void GivenNumberField_WhenNonNumeric_ThenValidationFails(string invalidNumber)
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "age", Label = "Age", FieldType = "number", Required = true }
        };
        var submitted = new Dictionary<string, string> { ["age"] = invalidNumber };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("age");
        result.Errors["age"].Should().Be("Age must be a number.");
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2024-13-01")]
    [InlineData("32/12/2024")]
    public void GivenDateField_WhenInvalidDate_ThenValidationFails(string invalidDate)
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "dob", Label = "Date of Birth", FieldType = "date", Required = true }
        };
        var submitted = new Dictionary<string, string> { ["dob"] = invalidDate };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("dob");
        result.Errors["dob"].Should().Be("Date of Birth must be a valid date.");
    }

    // ------------------------------------------------------------------ Options Whitelist

    [Fact]
    public void GivenSelectField_WhenValueNotInOptions_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "color",
                Label = "Color",
                FieldType = "select",
                Required = true,
                Options = new List<string> { "red", "green", "blue" }
            }
        };
        var submitted = new Dictionary<string, string> { ["color"] = "yellow" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("color");
        result.Errors["color"].Should().Be("Color contains an invalid selection.");
    }

    [Fact]
    public void GivenRadioField_WhenInjectedValue_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "membership",
                Label = "Membership",
                FieldType = "radio",
                Required = true,
                Options = new List<string> { "free", "basic", "premium" }
            }
        };
        var submitted = new Dictionary<string, string> { ["membership"] = "hacked-admin" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("membership");
        result.Errors["membership"].Should().Be("Membership contains an invalid selection.");
    }

    [Fact]
    public void GivenCheckboxListField_WhenOneValidOneInvalid_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "features",
                Label = "Features",
                FieldType = "checkboxlist",
                Required = false,
                Options = new List<string> { "sso", "api", "backup" }
            }
        };
        var submitted = new Dictionary<string, string> { ["features"] = "sso,injected-feature" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("features");
        result.Errors["features"].Should().Be("Features contains an invalid selection.");
    }

    // ------------------------------------------------------------------ Constraint Validation

    [Fact]
    public void GivenMinLengthConstraint_WhenTooShort_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "username",
                Label = "Username",
                FieldType = "text",
                Required = true,
                MinLength = 5
            }
        };
        var submitted = new Dictionary<string, string> { ["username"] = "abc" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("username");
        result.Errors["username"].Should().Be("Username must be at least 5 characters.");
    }

    [Fact]
    public void GivenMaxLengthConstraint_WhenTooLong_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "code",
                Label = "Code",
                FieldType = "text",
                Required = true,
                MaxLength = 10
            }
        };
        var submitted = new Dictionary<string, string> { ["code"] = "12345678901" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("code");
        result.Errors["code"].Should().Be("Code must be no more than 10 characters.");
    }

    [Fact]
    public void GivenPatternConstraint_WhenDoesNotMatch_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "postcode",
                Label = "Postcode",
                FieldType = "text",
                Required = true,
                Pattern = @"^[A-Z]{2}\d{2}$"
            }
        };
        var submitted = new Dictionary<string, string> { ["postcode"] = "12AB" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("postcode");
        result.Errors["postcode"].Should().Be("Postcode is not in the expected format.");
    }

    [Fact]
    public void GivenMinConstraint_WhenBelowMinimum_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "quantity",
                Label = "Quantity",
                FieldType = "number",
                Required = true,
                Min = 10
            }
        };
        var submitted = new Dictionary<string, string> { ["quantity"] = "5" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("quantity");
        result.Errors["quantity"].Should().Be("Quantity must be at least 10.");
    }

    [Fact]
    public void GivenMaxConstraint_WhenAboveMaximum_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "rating",
                Label = "Rating",
                FieldType = "number",
                Required = true,
                Max = 5
            }
        };
        var submitted = new Dictionary<string, string> { ["rating"] = "10" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("rating");
        result.Errors["rating"].Should().Be("Rating must be no more than 5.");
    }

    // ------------------------------------------------------------------ Security

    [Fact]
    public void GivenUnknownFieldKeyInSubmission_WhenNotInAuthoritative_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "name", Label = "Name", FieldType = "text", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["name"] = "Jane",
            ["injected_field"] = "malicious"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("injected_field");
        result.Errors["injected_field"].Should().Be("injected_field: Unknown field");
    }

    [Fact]
    public void GivenEmptySubmission_WhenNoRequiredFields_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "comment", Label = "Comment", FieldType = "textarea", Required = false }
        };
        var submitted = new Dictionary<string, string>();

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenXssAttemptInTextField_WhenValidatingStructure_ThenPassesThrough()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "comment", Label = "Comment", FieldType = "text", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["comment"] = "<script>alert('xss')</script>"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    // ------------------------------------------------------------------ Edge Cases

    [Fact]
    public void GivenBooleanField_WhenAbsentFromSubmission_ThenTreatedAsFalse()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "agree", Label = "I agree", FieldType = "boolean", Required = false }
        };
        var submitted = new Dictionary<string, string>();

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenCheckboxListField_WhenCommaSeparatedValues_ThenValidatesEachValue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "interests",
                Label = "Interests",
                FieldType = "checkboxlist",
                Required = false,
                Options = new List<string> { "sports", "music", "reading" }
            }
        };
        var submitted = new Dictionary<string, string> { ["interests"] = "sports,reading" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenCheckboxListField_WhenSubmittedWithSuffix_ThenFindsValue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "tags",
                Label = "Tags",
                FieldType = "checkboxlist",
                Required = true,
                Options = new List<string> { "tech", "design", "business" }
            }
        };
        var submitted = new Dictionary<string, string> { ["tags[]"] = "tech,design" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenSelectFieldWithEmptyOptions_WhenValidValue_ThenNoOptionsError()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "choice",
                Label = "Choice",
                FieldType = "select",
                Required = false,
                Options = new List<string>()
            }
        };
        var submitted = new Dictionary<string, string> { ["choice"] = "anything" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenPatternConstraint_WhenMatches_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "postcode",
                Label = "Postcode",
                FieldType = "text",
                Required = true,
                Pattern = @"^[A-Z]{2}\d{2}$"
            }
        };
        var submitted = new Dictionary<string, string> { ["postcode"] = "AB12" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenMinAndMaxNumberConstraints_WhenWithinRange_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "score",
                Label = "Score",
                FieldType = "number",
                Required = true,
                Min = 0,
                Max = 100
            }
        };
        var submitted = new Dictionary<string, string> { ["score"] = "75" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenMultipleFieldsWithErrors_WhenValidating_ThenReturnsAllErrors()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "name", Label = "Name", FieldType = "text", Required = true },
            new() { FieldKey = "email", Label = "Email", FieldType = "email", Required = true },
            new() { FieldKey = "age", Label = "Age", FieldType = "number", Required = true, Min = 18 }
        };
        var submitted = new Dictionary<string, string>
        {
            ["name"] = "",
            ["email"] = "invalid-email",
            ["age"] = "15"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().ContainKey("name");
        result.Errors.Should().ContainKey("email");
        result.Errors.Should().ContainKey("age");
    }

    [Fact]
    public void GivenDecimalFieldWithMinConstraint_WhenValueBelowMin_ThenReturnsError()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "amount", Label = "Amount (£)", FieldType = "decimal", Required = true, Min = 0.01m }
        };
        var submitted = new Dictionary<string, string>
        {
            ["amount"] = "0"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("amount");
        result.Errors["amount"].Should().Be("Amount (£) must be at least 0.01.");
    }

    [Fact]
    public void GivenOptionsWithCaseInsensitiveMatch_WhenValidating_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "color",
                Label = "Color",
                FieldType = "select",
                Required = true,
                Options = new List<string> { "Red", "Green", "Blue" }
            }
        };
        var submitted = new Dictionary<string, string> { ["color"] = "red" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    // ------------------------------------------------------------------ Conditional Fields

    [Fact]
    public void GivenConditionalRequiredField_WhenTriggerDoesNotMatch_ThenFieldIsSkipped()
    {
        // enquiry-type-other is only required when enquiry-type = "Other"
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "enquiry-type", Label = "Enquiry Type", FieldType = "radio", Required = true,
                Options = new List<string> { "General enquiry", "Other" } },
            new() { FieldKey = "enquiry-type-other", Label = "Please specify", FieldType = "text", Required = true,
                ConditionalOn = "enquiry-type", VisibleWhen = "Other" }
        };
        var submitted = new Dictionary<string, string>
        {
            ["enquiry-type"] = "General enquiry"
            // enquiry-type-other intentionally absent — trigger doesn't match
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue("the conditional field should be skipped when its trigger is not satisfied");
    }

    [Fact]
    public void GivenConditionalRequiredField_WhenTriggerMatches_ThenFieldIsValidated()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "enquiry-type", Label = "Enquiry Type", FieldType = "radio", Required = true,
                Options = new List<string> { "General enquiry", "Other" } },
            new() { FieldKey = "enquiry-type-other", Label = "Please specify", FieldType = "text", Required = true,
                ConditionalOn = "enquiry-type", VisibleWhen = "Other" }
        };
        var submitted = new Dictionary<string, string>
        {
            ["enquiry-type"] = "Other"
            // enquiry-type-other is absent but required when trigger matches
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("enquiry-type-other");
    }

    [Fact]
    public void GivenConditionalRequiredField_WhenTriggerMatchesAndValueProvided_ThenIsValid()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "enquiry-type", Label = "Enquiry Type", FieldType = "radio", Required = true,
                Options = new List<string> { "General enquiry", "Other" } },
            new() { FieldKey = "enquiry-type-other", Label = "Please specify", FieldType = "text", Required = true,
                ConditionalOn = "enquiry-type", VisibleWhen = "Other" }
        };
        var submitted = new Dictionary<string, string>
        {
            ["enquiry-type"] = "Other",
            ["enquiry-type-other"] = "Something else entirely"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenConditionalRequiredField_WhenTriggerMatchesCaseInsensitively_ThenFieldIsValidated()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "type", Label = "Type", FieldType = "radio", Required = true,
                Options = new List<string> { "Other" } },
            new() { FieldKey = "type-other", Label = "Specify", FieldType = "text", Required = true,
                ConditionalOn = "type", VisibleWhen = "Other" }
        };
        var submitted = new Dictionary<string, string> { ["type"] = "other" }; // lowercase

        // The trigger value "other" matches VisibleWhen "Other" case-insensitively
        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse("type-other is required when type=Other (case-insensitive)");
        result.Errors.Should().ContainKey("type-other");
    }

    // ------------------------------------------------------------------ ReadOnly (pre-populated) Fields

    [Fact]
    public void GivenReadOnlyField_WhenMissingFromSubmission_ThenSkippedValidation()
    {
        // Pre-populated fields (email, name from CMS claims) are ReadOnly and not re-validated
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "email", Label = "Email", FieldType = "email", Required = true, ReadOnly = true },
            new() { FieldKey = "message", Label = "Message", FieldType = "textarea", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            // email intentionally absent — ReadOnly fields may not be re-submitted
            ["message"] = "Hello from a test"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue("ReadOnly fields are skipped in server-side validation");
    }

    [Fact]
    public void GivenReadOnlyField_WhenInvalidValueProvided_ThenStillSkipped()
    {
        // Even if a client submits a bad value for a ReadOnly field, we don't validate it
        // (the controller uses the server-side value, not the submitted one)
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "email", Label = "Email", FieldType = "email", Required = true, ReadOnly = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["email"] = "not-an-email"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue("ReadOnly field type/format validation is skipped server-side");
    }

    // ------------------------------------------------------------------ Community Enquiry Demo Scenario

    [Fact]
    public void GivenCommunityEnquiryForm_WhenGeneralEnquirySubmitted_ThenIsValid()
    {
        // Mirrors the actual community-enquiry workflow form fields
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "full-name", Label = "Full Name", FieldType = "text", Required = true, ReadOnly = true },
            new() { FieldKey = "email-address", Label = "Email Address", FieldType = "email", Required = true, ReadOnly = true },
            new() { FieldKey = "organisation", Label = "Organisation", FieldType = "text", Required = false },
            new() { FieldKey = "your-role", Label = "Your Role", FieldType = "select", Required = true,
                Options = new List<string> { "Developer", "Architect", "Manager", "Designer", "Other" } },
            new() { FieldKey = "enquiry-type", Label = "What can we help with?", FieldType = "radio", Required = true,
                Options = new List<string> { "General enquiry", "Technical support", "Partnership opportunity", "Speaking / events", "Other" } },
            new() { FieldKey = "enquiry-type-other", Label = "Please specify", FieldType = "text", Required = false,
                ConditionalOn = "enquiry-type", VisibleWhen = "Other", MaxLength = 100 },
            new() { FieldKey = "message", Label = "Tell us more", FieldType = "textarea", Required = true, MinLength = 20, MaxLength = 500 },
            new() { FieldKey = "newsletter", Label = "Keep me updated", FieldType = "boolean", Required = false }
        };
        var submitted = new Dictionary<string, string>
        {
            ["your-role"] = "Developer",
            ["enquiry-type"] = "General enquiry",
            ["message"] = "I have a question about the Prism package and how it integrates with Umbraco workflows."
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenCommunityEnquiryForm_WhenOtherSelectedButSpecifyMissing_ThenConditionalFieldIsNotRequiredWhenOptional()
    {
        // enquiry-type-other is required=false in the real workflow
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "enquiry-type", Label = "What can we help with?", FieldType = "radio", Required = true,
                Options = new List<string> { "General enquiry", "Other" } },
            new() { FieldKey = "enquiry-type-other", Label = "Please specify", FieldType = "text", Required = false,
                ConditionalOn = "enquiry-type", VisibleWhen = "Other", MaxLength = 100 },
            new() { FieldKey = "message", Label = "Tell us more", FieldType = "textarea", Required = true, MinLength = 20 }
        };
        var submitted = new Dictionary<string, string>
        {
            ["enquiry-type"] = "Other",
            // enquiry-type-other is absent but not required
            ["message"] = "Some message that is long enough to pass min length validation easily"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue("optional conditional field absence should not fail validation");
    }

    [Fact]
    public void GivenCommunityEnquiryForm_WhenOtherSelectedAndSpecifyTooLong_ThenConstraintFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "enquiry-type", Label = "What can we help with?", FieldType = "radio", Required = true,
                Options = new List<string> { "General enquiry", "Other" } },
            new() { FieldKey = "enquiry-type-other", Label = "Please specify", FieldType = "text", Required = false,
                ConditionalOn = "enquiry-type", VisibleWhen = "Other", MaxLength = 10 },
            new() { FieldKey = "message", Label = "Tell us more", FieldType = "textarea", Required = true, MinLength = 5 }
        };
        var submitted = new Dictionary<string, string>
        {
            ["enquiry-type"] = "Other",
            ["enquiry-type-other"] = "This value is definitely longer than ten characters",
            ["message"] = "Some message"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("enquiry-type-other");
    }

    // ------------------------------------------------------------------ GDS Field Types

    [Fact]
    public void GivenDateInputField_WhenAllPartsProvided_ThenIsValidTrue()
    {
        // date (GDS) submits as {key}-day, {key}-month, {key}-year
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "dob", Label = "Date of birth", FieldType = "date", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["dob-day"] = "15",
            ["dob-month"] = "3",
            ["dob-year"] = "1990"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenDateInputField_WhenDayPartMissing_WhenRequired_ThenIsValidFalse()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "dob", Label = "Date of birth", FieldType = "date", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["dob-month"] = "3",
            ["dob-year"] = "1990"
            // day missing
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("dob");
    }

    [Fact]
    public void GivenDateInputField_WhenYearInvalid_ThenIsValidFalse()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "dob", Label = "Date of birth", FieldType = "date", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["dob-day"] = "15",
            ["dob-month"] = "3",
            ["dob-year"] = "99"  // 2-digit year
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GivenDateInputField_WhenNotRequired_WhenAllEmpty_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "optionalDate", Label = "Optional date", FieldType = "date", Required = false }
        };
        var submitted = new Dictionary<string, string>(); // nothing submitted

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("1234.56", true)]
    [InlineData("999", true)]
    [InlineData("0", true)]
    [InlineData("1,234.56", false)]  // commas not valid decimal
    [InlineData("£100", false)]      // prefix not valid
    [InlineData("abc", false)]       // not a number
    public void GivenCurrencyField_WhenValueSubmitted_ThenValidatesCorrectly(string value, bool expectedValid)
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "cost", Label = "Estimated cost", FieldType = "currency", Required = true }
        };
        var submitted = new Dictionary<string, string> { ["cost"] = value };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().Be(expectedValid);
    }

    // ------------------------------------------------------------------ Date-Input Year Validation

    [Fact]
    public void DateInput_YearBelow1900_ReturnsValidationError()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "event-date", Label = "Event date", FieldType = "date", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["event-date-day"] = "1",
            ["event-date-month"] = "1",
            ["event-date-year"] = "1899"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("event-date");
        result.Errors["event-date"].Should().Be("Event date year must be between 1900 and 2100.");
    }

    [Fact]
    public void DateInput_YearAbove2100_ReturnsValidationError()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "event-date", Label = "Event date", FieldType = "date", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["event-date-day"] = "31",
            ["event-date-month"] = "12",
            ["event-date-year"] = "2101"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("event-date");
        result.Errors["event-date"].Should().Be("Event date year must be between 1900 and 2100.");
    }

    [Fact]
    public void DateInput_YearAtBoundary1900_IsValid()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "event-date", Label = "Event date", FieldType = "date", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["event-date-day"] = "1",
            ["event-date-month"] = "1",
            ["event-date-year"] = "1900"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DateInput_YearAtBoundary2100_IsValid()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "event-date", Label = "Event date", FieldType = "date", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["event-date-day"] = "31",
            ["event-date-month"] = "12",
            ["event-date-year"] = "2100"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    // ------------------------------------------------------------------ Guidance Checklist

    [Fact]
    public void GivenGuidanceChecklist_WhenAllItemsAcknowledged_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "guidance",
                Label = "Guidance",
                FieldType = "guidance-checklist",
                Required = true,
                Options = new List<string> { "transfer-rules", "international-transfers", "supporting-evidence" }
            }
        };
        var submitted = new Dictionary<string, string>
        {
            ["guidance"] = "transfer-rules,international-transfers,supporting-evidence"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenGuidanceChecklist_WhenOnlySomeItemsAcknowledged_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "guidance",
                Label = "Guidance",
                FieldType = "guidance-checklist",
                Required = true,
                Options = new List<string> { "transfer-rules", "international-transfers", "supporting-evidence" }
            }
        };
        var submitted = new Dictionary<string, string>
        {
            ["guidance"] = "transfer-rules"
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse("a plain checkboxlist would accept a non-empty subset, but a required guidance-checklist must not");
        result.Errors.Should().ContainKey("guidance");
    }

    [Fact]
    public void GivenGuidanceChecklist_WhenNoneAcknowledged_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "guidance",
                Label = "Guidance",
                FieldType = "guidance-checklist",
                Required = true,
                Options = new List<string> { "transfer-rules", "international-transfers" }
            }
        };
        var submitted = new Dictionary<string, string> { ["guidance"] = "" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("guidance");
    }

    [Fact]
    public void GivenGuidanceChecklist_WhenNotRequiredAndPartiallyAcknowledged_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new()
            {
                FieldKey = "guidance",
                Label = "Guidance",
                FieldType = "guidance-checklist",
                Required = false,
                Options = new List<string> { "transfer-rules", "international-transfers" }
            }
        };
        var submitted = new Dictionary<string, string> { ["guidance"] = "transfer-rules" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue("the require-all rule only applies when the field is required");
    }

    // ------------------------------------------------------------------ File Upload

    [Fact]
    public void GivenFileUploadField_WhenRequiredAndSentinelPresent_ThenIsValidTrue()
    {
        // The controller injects a non-empty sentinel when a file was actually posted —
        // the validator itself never sees file bytes, only this string-based presence check.
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "current-licence", Label = "Current licence", FieldType = "file-upload", Required = true }
        };
        var submitted = new Dictionary<string, string> { ["current-licence"] = "uploaded" };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GivenFileUploadField_WhenRequiredAndNoFilePosted_ThenValidationFails()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "current-licence", Label = "Current licence", FieldType = "file-upload", Required = true }
        };
        var submitted = new Dictionary<string, string>();

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("current-licence");
        result.Errors["current-licence"].Should().Be("Current licence is required.");
    }

    [Fact]
    public void GivenFileUploadField_WhenOptionalAndNoFilePosted_ThenIsValidTrue()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "video-evidence", Label = "Video evidence", FieldType = "file-upload", Required = false }
        };
        var submitted = new Dictionary<string, string>();

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
    }
}
