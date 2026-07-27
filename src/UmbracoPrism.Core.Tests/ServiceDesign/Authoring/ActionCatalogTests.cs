using FluentAssertions;
using UmbracoPrism.MockBusinessApp.Services.Actions.ActionCatalog;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

public class ActionCatalogTests
{
    [Fact]
    public void DefaultParameterWidgetMapper_MapsCommonParameterKinds()
    {
        var mapper = new DefaultParameterWidgetMapper();

        mapper.GetWidget(new ActionParameterDefinition
        {
            Key = "textValue",
            ValueKind = ParameterValueKind.String
        }).Should().Be(ParameterWidgets.Text);

        mapper.GetWidget(new ActionParameterDefinition
        {
            Key = "numberValue",
            ValueKind = ParameterValueKind.Integer
        }).Should().Be(ParameterWidgets.Number);

        mapper.GetWidget(new ActionParameterDefinition
        {
            Key = "choice",
            ValueKind = ParameterValueKind.String,
            AllowedValues = ["a", "b"]
        }).Should().Be(ParameterWidgets.Select);

        mapper.GetWidget(new ActionParameterDefinition
        {
            Key = "enabled",
            ValueKind = ParameterValueKind.Boolean
        }).Should().Be(ParameterWidgets.Toggle);

        mapper.GetWidget(new ActionParameterDefinition
        {
            Key = "dueDate",
            ValueKind = ParameterValueKind.String,
            Format = "date"
        }).Should().Be(ParameterWidgets.Date);

        mapper.GetWidget(new ActionParameterDefinition
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
}
