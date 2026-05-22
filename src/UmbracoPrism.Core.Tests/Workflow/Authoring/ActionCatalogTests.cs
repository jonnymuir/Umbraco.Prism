using System.Text.Json.Nodes;
using FluentAssertions;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

public class ActionCatalogTests
{
    [Fact]
    public void DefaultParameterWidgetMapper_MapsCommonParameterKinds()
    {
        var mapper = new DefaultParameterWidgetMapper();

        mapper.GetWidget(new AuthoredParameterDefinition
        {
            Key = "textValue",
            ValueKind = ParameterValueKind.String
        }).Should().Be(ParameterWidgets.Text);

        mapper.GetWidget(new AuthoredParameterDefinition
        {
            Key = "numberValue",
            ValueKind = ParameterValueKind.Integer
        }).Should().Be(ParameterWidgets.Number);

        mapper.GetWidget(new AuthoredParameterDefinition
        {
            Key = "choice",
            ValueKind = ParameterValueKind.String,
            AllowedValues = ["a", "b"]
        }).Should().Be(ParameterWidgets.Select);

        mapper.GetWidget(new AuthoredParameterDefinition
        {
            Key = "enabled",
            ValueKind = ParameterValueKind.Boolean
        }).Should().Be(ParameterWidgets.Toggle);

        mapper.GetWidget(new AuthoredParameterDefinition
        {
            Key = "dueDate",
            ValueKind = ParameterValueKind.String,
            Format = "date"
        }).Should().Be(ParameterWidgets.Date);

        mapper.GetWidget(new AuthoredParameterDefinition
        {
            Key = "message",
            ValueKind = ParameterValueKind.String,
            Editor = "textarea"
        }).Should().Be(ParameterWidgets.Textarea);
    }

    [Fact]
    public void BuiltInCatalogProvider_ExposesExpectedBuiltInActions()
    {
        var provider = new BuiltInActionCatalogProvider(new DefaultParameterWidgetMapper());

        var entries = provider.GetEntries();

        entries.Should().HaveCountGreaterOrEqualTo(8);
        entries.Select(entry => entry.Type).Should().Contain([
            "forms.load",
            "forms.save",
            "forms.submit",
            "case.assign",
            "notifications.send-email"
        ]);

        provider.GetEntry("forms.load")!.ParameterWidgets["formDefinitionId"].Should().Be(ParameterWidgets.Text);
        provider.GetEntry("case.assign")!.ParameterWidgets["overwriteExisting"].Should().Be(ParameterWidgets.Toggle);
        provider.GetEntry("notifications.send-email")!.ParamsSchema.Required.Should().Contain(["templateId", "recipientEmail"]);
    }

    [Fact]
    public void Project_UsesBuiltInCatalogSchema_WhenWorkflowOmitsReusableParameterSchemas()
    {
        var projector = new WorkflowProjector(new BuiltInActionCatalogProvider(new DefaultParameterWidgetMapper()));

        var result = projector.Project(BuildWorkflow(new JsonObject
        {
            ["formDefinitionId"] = "details-form"
        }));

        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Project_WithBuiltInCatalogActionMissingRequiredParameter_ReturnsValidationError()
    {
        var projector = new WorkflowProjector(new BuiltInActionCatalogProvider(new DefaultParameterWidgetMapper()));

        var result = projector.Project(BuildWorkflow(new JsonObject()));

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Code == "PROJ120" && d.StageKey == "details");
    }

    private static AuthoredWorkflow BuildWorkflow(JsonObject parameters) => new()
    {
        DefinitionKey = "catalog-validation",
        DisplayName = "Catalog Validation",
        InitialStageKey = "details",
        Stages =
        [
            new AuthoredStage
            {
                StageKey = "details",
                DisplayName = "Details",
                Actions =
                [
                    new AuthoredAction
                    {
                        Type = "forms.load",
                        Timing = ActionTiming.OnEntry,
                        Parameters = parameters
                    }
                ]
            }
        ]
    };
}
