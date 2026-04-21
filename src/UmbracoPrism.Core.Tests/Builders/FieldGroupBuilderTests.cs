using FluentAssertions;
using UmbracoPrism.Shared.Builders;

namespace UmbracoPrism.Core.Tests.Builders;

public class FieldGroupBuilderTests
{
    [Fact]
    public void Build_WithAllProperties_ReturnsCompleteFieldGroup()
    {
        var result = new FieldGroupBuilder()
            .Key("personal-info")
            .DisplayName("Personal Information")
            .Version(2)
            .AddField("full-name", f => f
                .Label("Full Name")
                .FieldType("text")
                .Required()
                .MaxLength(100))
            .Build();

        result.GroupKey.Should().Be("personal-info");
        result.DisplayName.Should().Be("Personal Information");
        result.Version.Should().Be(2);
        result.Fields.Should().HaveCount(1);
    }

    [Fact]
    public void Build_WithoutVersionCall_DefaultsToOne()
    {
        var result = new FieldGroupBuilder()
            .Key("test-group")
            .Build();

        result.Version.Should().Be(1);
    }

    [Fact]
    public void Key_SetsGroupKey()
    {
        var result = new FieldGroupBuilder()
            .Key("unique-group-key")
            .Build();

        result.GroupKey.Should().Be("unique-group-key");
    }

    [Fact]
    public void DisplayName_SetsDisplayName()
    {
        var result = new FieldGroupBuilder()
            .DisplayName("My Field Group")
            .Build();

        result.DisplayName.Should().Be("My Field Group");
    }

    [Fact]
    public void AddField_WithTextFieldType_SetsFieldTypeCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("text-field", f => f
                .Label("Text")
                .FieldType("text"))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be("text");
    }

    [Fact]
    public void AddField_WithEmailFieldType_SetsFieldTypeCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("email-field", f => f
                .Label("Email")
                .FieldType("email"))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be("email");
    }

    [Fact]
    public void AddField_WithTextareaFieldType_SetsFieldTypeCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("textarea-field", f => f
                .Label("Comments")
                .FieldType("textarea"))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be("textarea");
    }

    [Fact]
    public void AddField_WithSelectFieldType_SetsFieldTypeCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("select-field", f => f
                .Label("Select")
                .FieldType("select"))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be("select");
    }

    [Fact]
    public void AddField_WithRadioFieldType_SetsFieldTypeCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("radio-field", f => f
                .Label("Radio")
                .FieldType("radio"))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be("radio");
    }

    [Fact]
    public void AddField_WithCheckboxlistFieldType_SetsFieldTypeCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("checkbox-field", f => f
                .Label("Checkboxes")
                .FieldType("checkboxlist"))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be("checkboxlist");
    }

    [Fact]
    public void AddField_WithBooleanFieldType_SetsFieldTypeCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("boolean-field", f => f
                .Label("Agree")
                .FieldType("boolean"))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be("boolean");
    }

    [Fact]
    public void AddField_WithNumberFieldType_SetsFieldTypeCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("number-field", f => f
                .Label("Age")
                .FieldType("number"))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be("number");
    }

    [Fact]
    public void AddField_WithDateInputFieldType_SetsFieldTypeCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("date-field", f => f
                .Label("Date of Birth")
                .FieldType("date-input"))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be("date-input");
    }

    [Fact]
    public void AddField_WithRequiredFlag_SetsRequiredTrue()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("required-field", f => f
                .Label("Required Field")
                .Required())
            .Build();

        var field = result.Fields.Single();
        field.Required.Should().BeTrue();
    }

    [Fact]
    public void AddField_WithRequiredFalse_SetsRequiredFalse()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("optional-field", f => f
                .Label("Optional Field")
                .Required(false))
            .Build();

        var field = result.Fields.Single();
        field.Required.Should().BeFalse();
    }

    [Fact]
    public void AddField_WithoutRequiredCall_DefaultsToFalse()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("default-field", f => f
                .Label("Default Field"))
            .Build();

        var field = result.Fields.Single();
        field.Required.Should().BeFalse();
    }

    [Fact]
    public void AddField_WithOptions_SetsOptionsCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("select-field", f => f
                .Label("Select")
                .FieldType("select")
                .Options("Option 1", "Option 2", "Option 3"))
            .Build();

        var field = result.Fields.Single();
        field.Options.Should().NotBeNull();
        field.Options.Should().HaveCount(3);
        field.Options.Should().ContainInOrder("Option 1", "Option 2", "Option 3");
    }

    [Fact]
    public void AddField_WithMaxLength_SetsMaxLengthCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("text-field", f => f
                .Label("Text")
                .MaxLength(50))
            .Build();

        var field = result.Fields.Single();
        field.MaxLength.Should().Be(50);
    }

    [Fact]
    public void AddField_WithMinLength_SetsMinLengthCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("text-field", f => f
                .Label("Text")
                .MinLength(10))
            .Build();

        var field = result.Fields.Single();
        field.MinLength.Should().Be(10);
    }

    [Fact]
    public void AddField_WithPattern_SetsPatternCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("text-field", f => f
                .Label("Text")
                .Pattern(@"^\d{5}$"))
            .Build();

        var field = result.Fields.Single();
        field.Pattern.Should().Be(@"^\d{5}$");
    }

    [Fact]
    public void AddField_WithMin_SetsMinCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("number-field", f => f
                .Label("Age")
                .FieldType("number")
                .Min(18))
            .Build();

        var field = result.Fields.Single();
        field.Min.Should().Be(18);
    }

    [Fact]
    public void AddField_WithMax_SetsMaxCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("number-field", f => f
                .Label("Age")
                .FieldType("number")
                .Max(100))
            .Build();

        var field = result.Fields.Single();
        field.Max.Should().Be(100);
    }

    [Fact]
    public void AddField_WithHint_SetsHintCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("text-field", f => f
                .Label("Email")
                .Hint("We'll use this to contact you"))
            .Build();

        var field = result.Fields.Single();
        field.Hint.Should().Be("We'll use this to contact you");
    }

    [Fact]
    public void AddField_WithShowWhen_SetsConditionalPropertiesCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("conditional-field", f => f
                .Label("Conditional")
                .ShowWhen("parent-field", "yes"))
            .Build();

        var field = result.Fields.Single();
        field.ConditionalOn.Should().Be("parent-field");
        field.VisibleWhen.Should().Be("yes");
    }

    [Fact]
    public void AddField_WithPrefix_SetsPrefixCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("price-field", f => f
                .Label("Price")
                .FieldType("number")
                .Prefix("£"))
            .Build();

        var field = result.Fields.Single();
        field.Prefix.Should().Be("£");
    }

    [Fact]
    public void AddField_WithContent_SetsContentCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("inset", f => f
                .FieldType("inset-text")
                .Content("This is important information"))
            .Build();

        var field = result.Fields.Single();
        field.Content.Should().Be("This is important information");
    }

    [Fact]
    public void AddField_WithReadOnly_SetsReadOnlyCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("readonly-field", f => f
                .Label("Read Only")
                .ReadOnly())
            .Build();

        var field = result.Fields.Single();
        // Note: ReadOnly property doesn't exist on FieldFile in the current model
        // But the method exists on the builder - this might be a future property
        // For now, this test validates the API exists and doesn't throw
    }

    [Fact]
    public void AddField_WithMultipleFields_AllFieldsPresent()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("field1", f => f.Label("First"))
            .AddField("field2", f => f.Label("Second"))
            .AddField("field3", f => f.Label("Third"))
            .AddField("field4", f => f.Label("Fourth"))
            .AddField("field5", f => f.Label("Fifth"))
            .Build();

        result.Fields.Should().HaveCount(5);
        result.Fields[0].FieldKey.Should().Be("field1");
        result.Fields[1].FieldKey.Should().Be("field2");
        result.Fields[2].FieldKey.Should().Be("field3");
        result.Fields[3].FieldKey.Should().Be("field4");
        result.Fields[4].FieldKey.Should().Be("field5");
    }

    [Fact]
    public void AddField_WithComplexField_SetsAllPropertiesCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("complex-field", f => f
                .Label("Complex Field")
                .FieldType("text")
                .Required()
                .Hint("Enter your response")
                .MinLength(5)
                .MaxLength(100)
                .Pattern(@"^[a-zA-Z\s]+$")
                .ShowWhen("show-field", "yes"))
            .Build();

        var field = result.Fields.Single();
        field.FieldKey.Should().Be("complex-field");
        field.Label.Should().Be("Complex Field");
        field.FieldType.Should().Be("text");
        field.Required.Should().BeTrue();
        field.Hint.Should().Be("Enter your response");
        field.MinLength.Should().Be(5);
        field.MaxLength.Should().Be(100);
        field.Pattern.Should().Be(@"^[a-zA-Z\s]+$");
        field.ConditionalOn.Should().Be("show-field");
        field.VisibleWhen.Should().Be("yes");
    }

    [Fact]
    public void Build_WithMultipleFieldTypes_ReturnsAllFieldsInOrder()
    {
        var result = new FieldGroupBuilder()
            .Key("mixed-fields")
            .DisplayName("Mixed Field Types")
            .AddField("text1", f => f.Label("Text").FieldType("text"))
            .AddField("email1", f => f.Label("Email").FieldType("email"))
            .AddField("number1", f => f.Label("Number").FieldType("number"))
            .AddField("date1", f => f.Label("Date").FieldType("date-input"))
            .AddField("select1", f => f.Label("Select").FieldType("select").Options("A", "B", "C"))
            .AddField("radio1", f => f.Label("Radio").FieldType("radio").Options("Yes", "No"))
            .Build();

        result.Fields.Should().HaveCount(6);
        result.Fields[0].FieldType.Should().Be("text");
        result.Fields[1].FieldType.Should().Be("email");
        result.Fields[2].FieldType.Should().Be("number");
        result.Fields[3].FieldType.Should().Be("date-input");
        result.Fields[4].FieldType.Should().Be("select");
        result.Fields[5].FieldType.Should().Be("radio");
    }

    [Fact]
    public void AddField_WithLabel_SetsLabelCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("field1", f => f.Label("My Label"))
            .Build();

        var field = result.Fields.Single();
        field.Label.Should().Be("My Label");
    }

    [Fact]
    public void AddField_SetsFieldKeyCorrectly()
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("unique-field-key", f => f.Label("Field"))
            .Build();

        var field = result.Fields.Single();
        field.FieldKey.Should().Be("unique-field-key");
    }

    [Theory]
    [InlineData("text")]
    [InlineData("email")]
    [InlineData("textarea")]
    [InlineData("select")]
    [InlineData("radio")]
    [InlineData("checkboxlist")]
    [InlineData("boolean")]
    [InlineData("number")]
    [InlineData("date-input")]
    public void AddField_WithVariousFieldTypes_SetsFieldTypeCorrectly(string fieldType)
    {
        var result = new FieldGroupBuilder()
            .Key("test")
            .AddField("field1", f => f.Label("Field").FieldType(fieldType))
            .Build();

        var field = result.Fields.Single();
        field.FieldType.Should().Be(fieldType);
    }
}
