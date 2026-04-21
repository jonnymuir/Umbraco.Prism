using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.TagHelpers;

namespace UmbracoPrism.Core.Tests.TagHelpers;

public class PrismFieldTagHelperContentTypesTests
{
    private static readonly WorkflowFieldValidator Validator = new();

    // ------------------------------------------------------------------ Helpers

    private static string Process(FieldRenderPayload field)
    {
        var helper = new PrismFieldTagHelper { Field = field };
        var context = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            "test-id");
        var output = new TagHelperOutput(
            "prism-field",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.Process(context, output);
        return output.Content.GetContent();
    }

    // ------------------------------------------------------------------ inset-text

    [Fact]
    public void GivenInsetTextField_WhenProcessed_ThenRendersGovukInsetTextDiv()
    {
        var field = new FieldRenderPayload
        {
            FieldKey = "privacy-note",
            Label = "",
            FieldType = "inset-text",
            Required = false,
            Content = "We'll only use your contact details to respond to your enquiry."
        };

        var html = Process(field);

        html.Should().Contain(@"class=""govuk-inset-text""");
        html.Should().Contain("We&#39;ll only use your contact details to respond to your enquiry.");
        html.Should().NotContain("govuk-form-group");
    }

    [Fact]
    public void GivenInsetTextFieldWithNullContent_WhenProcessed_ThenRendersEmpty()
    {
        var field = new FieldRenderPayload
        {
            FieldKey = "empty-note",
            Label = "",
            FieldType = "inset-text",
            Required = false,
            Content = null
        };

        var html = Process(field);

        html.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ warning-text

    [Fact]
    public void GivenWarningTextField_WhenProcessed_ThenRendersGovukWarningTextWithIcon()
    {
        var field = new FieldRenderPayload
        {
            FieldKey = "data-warning",
            Label = "",
            FieldType = "warning-text",
            Required = false,
            Content = "Do not include passwords or API keys."
        };

        var html = Process(field);

        html.Should().Contain(@"class=""govuk-warning-text""");
        html.Should().Contain(@"class=""govuk-warning-text__icon""");
        html.Should().Contain("!");
        html.Should().Contain(@"class=""govuk-visually-hidden"">Warning");
        html.Should().Contain("Do not include passwords or API keys.");
        html.Should().NotContain("govuk-form-group");
    }

    // ------------------------------------------------------------------ details

    [Fact]
    public void GivenDetailsField_WhenProcessed_ThenRendersGovukDetailsWithLabelAsSummary()
    {
        var field = new FieldRenderPayload
        {
            FieldKey = "why-details",
            Label = "Why do we need your contact details?",
            FieldType = "details",
            Required = false,
            Content = "We need your name and email so we can respond to your enquiry."
        };

        var html = Process(field);

        html.Should().Contain(@"class=""govuk-details""");
        html.Should().Contain(@"class=""govuk-details__summary""");
        html.Should().Contain("Why do we need your contact details?");
        html.Should().Contain(@"class=""govuk-details__text""");
        html.Should().Contain("We need your name and email so we can respond to your enquiry.");
        html.Should().NotContain("govuk-form-group");
    }

    [Fact]
    public void GivenDetailsFieldWithNoLabel_WhenProcessed_ThenUsesFallbackSummaryText()
    {
        var field = new FieldRenderPayload
        {
            FieldKey = "more-info",
            Label = "",
            FieldType = "details",
            Required = false,
            Content = "Some detail content here."
        };

        var html = Process(field);

        html.Should().Contain("More information");
    }

    // ------------------------------------------------------------------ notification-banner

    [Fact]
    public void GivenNotificationBannerField_WhenProcessed_ThenRendersCorrectStructure()
    {
        var field = new FieldRenderPayload
        {
            FieldKey = "success-banner",
            Label = "Application submitted",
            FieldType = "notification-banner",
            Required = false,
            Content = "Your application has been received."
        };

        var html = Process(field);

        html.Should().Contain(@"class=""govuk-notification-banner govuk-notification-banner--success""");
        html.Should().Contain(@"aria-labelledby=""success-banner-banner-title""");
        html.Should().Contain(@"data-module=""govuk-notification-banner""");
        html.Should().Contain("Application submitted");
        html.Should().Contain(@"class=""govuk-body""");
        html.Should().Contain("Your application has been received.");
        html.Should().NotContain("govuk-form-group");
    }

    [Fact]
    public void GivenNotificationBannerWithNoLabel_WhenProcessed_ThenUsesImportantAsTitle()
    {
        var field = new FieldRenderPayload
        {
            FieldKey = "alert-banner",
            Label = "",
            FieldType = "notification-banner",
            Required = false,
            Content = "Something important happened."
        };

        var html = Process(field);

        html.Should().Contain("Important");
    }

    // ------------------------------------------------------------------ Validator exclusion

    [Fact]
    public void GivenContentFieldTypes_WhenValidating_ThenNotTreatedAsRequiredFields()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "privacy-note", Label = "", FieldType = "inset-text", Required = false, Content = "Some note." },
            new() { FieldKey = "data-warning", Label = "", FieldType = "warning-text", Required = false, Content = "Warning text." },
            new() { FieldKey = "more-info", Label = "More info", FieldType = "details", Required = false, Content = "Detail body." },
            new() { FieldKey = "success-msg", Label = "Done", FieldType = "notification-banner", Required = false, Content = "All done." },
            new() { FieldKey = "name", Label = "Name", FieldType = "text", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["name"] = "Jane Doe"
            // Content fields deliberately absent — validator must not flag them
        };

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void GivenContentFieldKeyInSubmission_WhenValidating_ThenFlaggedAsUnknown()
    {
        // Content fields are server-side-only; if a client submits their key it should be rejected
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "privacy-note", Label = "", FieldType = "inset-text", Required = false, Content = "Some note." },
            new() { FieldKey = "name", Label = "Name", FieldType = "text", Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["name"] = "Jane Doe",
            ["privacy-note"] = "injected value"
        };

        var result = Validator.Validate(authoritative, submitted);

        // privacy-note is in authoritative so it's whitelisted — submitted value ignored harmlessly
        result.IsValid.Should().BeTrue();
    }
}
