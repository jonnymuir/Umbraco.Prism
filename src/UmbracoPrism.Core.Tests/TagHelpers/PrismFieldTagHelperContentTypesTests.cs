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
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.TagHelpers;

namespace UmbracoPrism.Core.Tests.TagHelpers;

public class PrismFieldTagHelperContentTypesTests
{
    private static readonly WorkflowFieldValidator Validator = new();

    // ------------------------------------------------------------------ Helpers

    private static async Task<string> ProcessAsync(FieldRenderPayload field)
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

        var helper = new PrismFieldTagHelper(htmlHelperMock.Object, viewEngineMock.Object)
        {
            Field       = field,
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

