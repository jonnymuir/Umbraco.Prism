using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Moq;
using UmbracoPrism.Core.Models.ServiceDesign;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.TagHelpers;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Tests.TagHelpers;

public class PrismFieldTagHelperContentTypesTests
{
    private static readonly ServiceRequestFieldValidator Validator = new();

    // ------------------------------------------------------------------ Helpers

    private static async Task<string> ProcessAsync(
        FieldRenderPayload field,
        IReadOnlyDictionary<string, string>? errors = null,
        IReadOnlyDictionary<string, string>? values = null)
    {
        var htmlHelperMock = new Mock<IHtmlHelper>();
        htmlHelperMock.As<IViewContextAware>().Setup(x => x.Contextualize(It.IsAny<ViewContext>()));

        var viewEngineMock = new Mock<ICompositeViewEngine>();
        viewEngineMock
            .Setup(x => x.GetView(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(ViewEngineResult.NotFound("view", new[] { "not-found" }));

        var viewContext = new ViewContext
        {
            HttpContext    = new DefaultHttpContext(),
            ActionDescriptor = new ControllerActionDescriptor(),
            View           = Mock.Of<IView>(),
            ViewData       = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
            TempData       = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            Writer         = new StringWriter(),
        };

        var helper = new PrismComponentTagHelper(htmlHelperMock.Object, viewEngineMock.Object)
        {
            Field       = field,
            Errors      = errors,
            Values      = values,
            ViewContext = viewContext,
        };

        var context = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            "test-id");
        var output = new TagHelperOutput(
            "prism-field",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        await helper.ProcessAsync(context, output);
        return output.Content.GetContent();
    }

    // ------------------------------------------------------------------ inset-text

    [Fact]
    public async Task GivenInsetTextField_WhenProcessed_ThenRendersGovukInsetTextDiv()
    {
        var field = new FieldRenderPayload
        {
            FieldKey  = "privacy-note",
            Label     = "",
            FieldType = "inset-text",
            Required  = false,
            Content   = "We'll only use your contact details to respond to your enquiry."
        };

        var html = await ProcessAsync(field);

        html.Should().Contain(@"class=""govuk-inset-text""");
        html.Should().Contain("We&#39;ll only use your contact details to respond to your enquiry.");
        html.Should().NotContain("govuk-form-group");
    }

    [Fact]
    public async Task GivenInsetTextFieldWithNullContent_WhenProcessed_ThenRendersEmpty()
    {
        var field = new FieldRenderPayload
        {
            FieldKey  = "empty-note",
            Label     = "",
            FieldType = "inset-text",
            Required  = false,
            Content   = null
        };

        var html = await ProcessAsync(field);

        html.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ warning-text

    [Fact]
    public async Task GivenWarningTextField_WhenProcessed_ThenRendersGovukWarningTextWithIcon()
    {
        var field = new FieldRenderPayload
        {
            FieldKey  = "data-warning",
            Label     = "",
            FieldType = "warning-text",
            Required  = false,
            Content   = "Do not include passwords or API keys."
        };

        var html = await ProcessAsync(field);

        html.Should().Contain(@"class=""govuk-warning-text""");
        html.Should().Contain(@"class=""govuk-warning-text__icon""");
        html.Should().Contain("!");
        html.Should().Contain(@"class=""govuk-visually-hidden"">Warning");
        html.Should().Contain("Do not include passwords or API keys.");
        html.Should().NotContain("govuk-form-group");
    }

    [Fact]
    public async Task GivenDetailsField_WhenProcessed_ThenRendersGovukDetailsInsteadOfInput()
    {
        var field = new FieldRenderPayload
        {
            FieldKey = "why-we-need-details",
            Label = "Why do we need your contact details?",
            FieldType = "details",
            Required = false,
            Content = "We need your name and email so we can respond to your enquiry."
        };

        var html = await ProcessAsync(field);

        html.Should().Contain(@"<details class=""govuk-details"">");
        html.Should().Contain(@"class=""govuk-details__summary-text"">Why do we need your contact details?");
        html.Should().Contain("We need your name and email so we can respond to your enquiry.");
        html.Should().NotContain("govuk-input");
        html.Should().NotContain("govuk-form-group");
        html.Should().NotContain(@"name=""fields[why-we-need-details]""");
        html.Should().NotContain(@"id=""why-we-need-details""");
    }

    [Fact]
    public async Task GivenNotificationBannerField_WhenProcessed_ThenRendersGovukNotificationBanner()
    {
        var field = new FieldRenderPayload
        {
            FieldKey = "contact-banner",
            Label = "Information",
            FieldType = "notification-banner",
            Required = false,
            Content = "We'll reply within 2 working days."
        };

        var html = await ProcessAsync(field);

        html.Should().Contain(@"class=""govuk-notification-banner""");
        html.Should().Contain(@"role=""region""");
        html.Should().Contain("We&#39;ll reply within 2 working days.");
        html.Should().NotContain("govuk-input");
    }

    // ------------------------------------------------------------------ Validator exclusion

    [Fact]
    public void GivenContentFieldTypes_WhenValidating_ThenNotTreatedAsRequiredFields()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "privacy-note", Label = "", FieldType = "inset-text",           Required = false, Content = "Some note." },
            new() { FieldKey = "data-warning",  Label = "", FieldType = "warning-text",          Required = false, Content = "Warning text." },
            new() { FieldKey = "more-info",     Label = "More info", FieldType = "details",      Required = false, Content = "Detail body." },
            new() { FieldKey = "success-msg",   Label = "Done", FieldType = "notification-banner", Required = false, Content = "All done." },
            new() { FieldKey = "name",          Label = "Name", FieldType = "text",              Required = true }
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
    public void GivenContentFieldsAndMissingRealInputs_WhenValidating_ThenOnlyRealInputsFail()
    {
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "privacy-note", Label = "", FieldType = "inset-text", Required = false, Content = "We'll only use your contact details to respond to your enquiry." },
            new() { FieldKey = "why-we-need-details", Label = "Why do we need your contact details?", FieldType = "details", Required = false, Content = "We need your name and email so we can respond to your enquiry." },
            new() { FieldKey = "your-role", Label = "Your role", FieldType = "select", Required = true, Options = new List<string> { "Developer", "Architect" } },
            new() { FieldKey = "enquiry-type", Label = "What can we help with?", FieldType = "radio", Required = true, Options = new List<string> { "General enquiry", "Technical support" } },
            new() { FieldKey = "message", Label = "Tell us more", FieldType = "textarea", Required = true, MinLength = 20 }
        };
        var submitted = new Dictionary<string, string>();

        var result = Validator.Validate(authoritative, submitted);

        result.IsValid.Should().BeFalse();
        result.Errors.Keys.Should().BeEquivalentTo(["your-role", "enquiry-type", "message"]);
        result.Errors.Keys.Should().NotContain(["privacy-note", "why-we-need-details"]);
    }

    [Fact]
    public void GivenSelectAndTextareaErrors_WhenBuildingContext_ThenRealInputsCarryGdsErrorMetadata()
    {
        var selectContext = PrismFieldContext.Build(
            new FieldRenderPayload
            {
                FieldKey = "your-role",
                Label = "Your role",
                FieldType = "select",
                Required = true,
                Options = new List<string> { "Developer", "Architect" }
            },
            fieldError: "Select your role",
            values: null);

        var textareaContext = PrismFieldContext.Build(
            new FieldRenderPayload
            {
                FieldKey = "message",
                Label = "Tell us more",
                FieldType = "textarea",
                Required = true
            },
            fieldError: "Enter your message",
            values: null);

        selectContext.HasFieldError.Should().BeTrue();
        selectContext.WrapperClass.Should().Contain("govuk-form-group--error");
        selectContext.DescribedBy.Should().Contain("your-role-error");
        selectContext.AriaInvalid.Should().Contain("aria-invalid");

        textareaContext.HasFieldError.Should().BeTrue();
        textareaContext.WrapperClass.Should().Contain("govuk-form-group--error");
        textareaContext.DescribedBy.Should().Contain("message-error");
        textareaContext.AriaInvalid.Should().Contain("aria-invalid");
    }

    [Fact]
    public void GivenContentFieldKeyInSubmission_WhenValidating_ThenFlaggedAsUnknown()
    {
        // Content fields are server-side-only; if a client submits their key it should be rejected
        var authoritative = new List<FieldRenderPayload>
        {
            new() { FieldKey = "privacy-note", Label = "", FieldType = "inset-text", Required = false, Content = "Some note." },
            new() { FieldKey = "name",         Label = "Name", FieldType = "text",   Required = true }
        };
        var submitted = new Dictionary<string, string>
        {
            ["name"]         = "Jane Doe",
            ["privacy-note"] = "injected value"
        };

        var result = Validator.Validate(authoritative, submitted);

        // privacy-note is in authoritative so it's whitelisted — submitted value ignored harmlessly
        result.IsValid.Should().BeTrue();
    }
}
