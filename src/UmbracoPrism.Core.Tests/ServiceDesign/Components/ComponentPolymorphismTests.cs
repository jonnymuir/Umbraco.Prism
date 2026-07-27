using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;
using Xunit;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Components;

/// <summary>
/// Tests for polymorphic JSON serialization/deserialization of v2.0 component hierarchy.
/// Verifies that each component type roundtrips correctly with its discriminator.
/// </summary>
public class ComponentPolymorphismTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Theory]
    [InlineData("fieldset")]
    [InlineData("accordion")]
    [InlineData("panel")]
    public void ContainerComponents_RoundtripCorrectly(string componentType)
    {
        // Arrange
        var component = componentType switch
        {
            "fieldset" => (PrismComponent)new FieldsetComponent
            {
                Legend = "Personal details",
                LegendSize = "l",
                Children = new List<PrismComponent>
                {
                    new TextInputComponent { FieldKey = "name", Label = "Full name", Required = true }
                }
            },
            "accordion" => new AccordionComponent
            {
                Sections = new List<AccordionSection>
                {
                    new()
                    {
                        Heading = "Section 1",
                        Summary = "Optional summary",
                        Children = new List<PrismComponent>
                        {
                            new BodyComponent { Content = "Content here" }
                        }
                    }
                }
            },
            "panel" => new PanelComponent { Heading = "Application complete" },
            _ => throw new ArgumentException($"Unknown type: {componentType}")
        };

        // Act
        var json = JsonSerializer.Serialize(component, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PrismComponent>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeOfType(component.GetType());
        json.Should().Contain($"\"type\": \"{componentType}\"");
        
        // Verify round-trip by re-serializing and comparing JSON
        var reserializedJson = JsonSerializer.Serialize(deserialized, JsonOptions);
        reserializedJson.Should().Be(json);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("number")]
    [InlineData("decimal")]
    [InlineData("select")]
    [InlineData("radio")]
    [InlineData("checkboxlist")]
    [InlineData("date")]
    [InlineData("email")]
    [InlineData("textarea")]
    [InlineData("boolean")]
    public void InputComponents_RoundtripCorrectly(string componentType)
    {
        // Arrange
        var component = componentType switch
        {
            "text" => (PrismComponent)new TextInputComponent
            {
                FieldKey = "full-name",
                Label = "Full name",
                Hint = "Enter your full legal name",
                Required = true,
                MinLength = 2,
                MaxLength = 100,
                Pattern = "^[A-Za-z ]+$",
                Prefix = "Mr/Mrs"
            },
            "number" => new NumberInputComponent
            {
                FieldKey = "age",
                Label = "Age",
                Required = true,
                Min = 18,
                Max = 120,
                Prefix = "Years:"
            },
            "decimal" => new DecimalInputComponent
            {
                FieldKey = "salary",
                Label = "Annual salary",
                Required = true,
                Min = 0,
                Max = 1000000,
                Prefix = "£"
            },
            "select" => new SelectComponent
            {
                FieldKey = "country",
                Label = "Country",
                Required = true,
                Options = new List<string> { "UK", "USA", "Canada" }
            },
            "radio" => new RadiosComponent
            {
                FieldKey = "contact-preference",
                Label = "How should we contact you?",
                Required = true,
                Options = new List<string> { "Email", "Phone", "Post" },
                ConditionalChildren = new Dictionary<string, IReadOnlyList<PrismComponent>>
                {
                    ["Email"] = new List<PrismComponent>
                    {
                        new EmailComponent { FieldKey = "email", Label = "Email address", Required = true }
                    }
                }
            },
            "checkboxlist" => new CheckboxesComponent
            {
                FieldKey = "interests",
                Label = "What are you interested in?",
                Required = false,
                Options = new List<string> { "Sports", "Music", "Reading" },
                ConditionalChildren = new Dictionary<string, IReadOnlyList<PrismComponent>>
                {
                    ["Sports"] = new List<PrismComponent>
                    {
                        new TextInputComponent { FieldKey = "favorite-sport", Label = "Favorite sport", Required = false }
                    }
                }
            },
            "date" => new DateInputComponent
            {
                FieldKey = "birth-date",
                Label = "Date of birth",
                Hint = "For example, 31 3 1980",
                Required = true
            },
            "email" => new EmailComponent
            {
                FieldKey = "email-address",
                Label = "Email address",
                Required = true,
                Pattern = "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$"
            },
            "textarea" => new TextareaComponent
            {
                FieldKey = "comments",
                Label = "Additional comments",
                Hint = "Tell us more about your situation",
                Required = false,
                MinLength = 10,
                MaxLength = 500
            },
            "boolean" => new BooleanComponent
            {
                FieldKey = "agree-terms",
                Label = "I agree to the terms and conditions",
                Required = true
            },
            _ => throw new ArgumentException($"Unknown type: {componentType}")
        };

        // Act
        var json = JsonSerializer.Serialize(component, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PrismComponent>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeOfType(component.GetType());
        json.Should().Contain($"\"type\": \"{componentType}\"");
        
        // Verify round-trip by re-serializing and comparing JSON
        var reserializedJson = JsonSerializer.Serialize(deserialized, JsonOptions);
        reserializedJson.Should().Be(json);
    }

    [Theory]
    [InlineData("body")]
    [InlineData("heading")]
    [InlineData("inset-text")]
    [InlineData("warning-text")]
    [InlineData("details")]
    [InlineData("notification-banner")]
    public void ContentComponents_RoundtripCorrectly(string componentType)
    {
        // Arrange
        var component = componentType switch
        {
            "body" => (PrismComponent)new BodyComponent { Content = "This is body text content." },
            "heading" => new HeadingComponent { Level = 2, Content = "Section heading" },
            "inset-text" => new InsetTextComponent { Content = "Important information in an inset box." },
            "warning-text" => new WarningTextComponent { Content = "You must complete this step." },
            "details" => new DetailsComponent
            {
                Heading = "Need help?",
                Content = "Contact us at support@example.com"
            },
            "notification-banner" => new NotificationBannerComponent
            {
                BannerType = "success",
                Heading = "Application submitted",
                Content = "Your application has been received."
            },
            _ => throw new ArgumentException($"Unknown type: {componentType}")
        };

        // Act
        var json = JsonSerializer.Serialize(component, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PrismComponent>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeOfType(component.GetType());
        json.Should().Contain($"\"type\": \"{componentType}\"");
        
        // Verify round-trip by re-serializing and comparing JSON
        var reserializedJson = JsonSerializer.Serialize(deserialized, JsonOptions);
        reserializedJson.Should().Be(json);
    }

    [Theory]
    [InlineData("waiting")]
    [InlineData("summary-list")]
    [InlineData("task-list")]
    [InlineData("file-upload")]
    [InlineData("guidance-checklist")]
    public void WorkflowComponents_RoundtripCorrectly(string componentType)
    {
        // Arrange
        var component = componentType switch
        {
            "waiting" => (PrismComponent)new WaitingComponent
            {
                Content = "We're processing your payment. This usually takes 30 seconds.",
                ExpectedWaitSeconds = 30,
                PollIntervalMs = 5000,
                AllowDefer = true,
                DeferMessage = "You can return to this page later."
            },
            "summary-list" => new SummaryListComponent
            {
                Children = new List<PrismComponent>
                {
                    new TextInputComponent { FieldKey = "full-name", Label = "Full name" },
                    new EmailComponent { FieldKey = "email-address", Label = "Email address" },
                    new TextInputComponent { FieldKey = "phone-number", Label = "Phone number" }
                },
                ChangeStateKey = "collect-details",
                Title = "Check your answers"
            },
            "task-list" => new TaskListComponent
            {
                Sections = new List<TaskSection>
                {
                    new()
                    {
                        Heading = "Before you start",
                        Tasks = new List<TaskItem>
                        {
                            new() { Label = "Check eligibility", TouchpointKey = "eligibility" },
                            new() { Label = "Read guidance", Href = "/guidance" }
                        }
                    }
                }
            },
            "file-upload" => new FileUploadComponent
            {
                FieldKey = "current-licence",
                Label = "Current licence",
                Required = true,
                AcceptedFileTypes = new List<string> { ".pdf", ".jpg" },
                MaxSizeBytes = 5 * 1024 * 1024
            },
            "guidance-checklist" => new GuidanceChecklistComponent
            {
                FieldKey = "guidance",
                Label = "Read the guidance",
                Required = true,
                Items = new List<GuidanceChecklistItem>
                {
                    new() { Key = "transfer-rules", Label = "Transfer Rules", Href = "/transfer-rules" },
                    new() { Key = "supporting-evidence", Label = "Supporting Evidence", Href = "/supporting-evidence" }
                }
            },
            _ => throw new ArgumentException($"Unknown type: {componentType}")
        };

        // Act
        var json = JsonSerializer.Serialize(component, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PrismComponent>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeOfType(component.GetType());
        json.Should().Contain($"\"type\": \"{componentType}\"");

        // Verify round-trip by re-serializing and comparing JSON
        var reserializedJson = JsonSerializer.Serialize(deserialized, JsonOptions);
        reserializedJson.Should().Be(json);
    }

    [Fact]
    public void WorkflowDefinitionFile_RoundtripsCorrectly()
    {
        // Arrange
        var definition = new ServiceBlueprint
        {
            DefinitionKey = "test-workflow",
            DisplayName = "Test Workflow",
            Version = 1,
            InitialTouchpoint = "start",
            RequestPolicy = "single",
            Touchpoints = new List<StepDefinition>
            {
                new()
                {
                    TouchpointKey = "start",
                    DisplayName = "Start",
                    Components = new List<PrismComponent>
                    {
                        new HeadingComponent { Level = 1, Content = "Welcome" },
                        new BodyComponent { Content = "Please provide your details." },
                        new FieldsetComponent
                        {
                            Legend = "Personal information",
                            Children = new List<PrismComponent>
                            {
                                new TextInputComponent { FieldKey = "name", Label = "Name", Required = true },
                                new EmailComponent { FieldKey = "email", Label = "Email", Required = true }
                            }
                        }
                    }
                },
                new()
                {
                    TouchpointKey = "check",
                    DisplayName = "Check your answers",
                    Components = new List<PrismComponent>
                    {
                        new SummaryListComponent
                        {
                            Children = new List<PrismComponent>
                            {
                                new TextInputComponent { FieldKey = "name", Label = "Name", Required = true },
                                new EmailComponent { FieldKey = "email", Label = "Email", Required = true }
                            },
                            ChangeStateKey = "start",
                            Title = "Your details"
                        }
                    }
                },
                new()
                {
                    TouchpointKey = "complete",
                    DisplayName = "Complete",
                    Components = new List<PrismComponent>
                    {
                        new PanelComponent { Heading = "Application complete" },
                        new BodyComponent { Content = "Thank you for your submission." }
                    }
                }
            },
            Transitions = new List<RouteFile>
            {
                new() { FromState = "start", ToState = "check", Action = "continue" },
                new() { FromState = "check", ToState = "complete", Action = "confirm" }
            },
            Layout = new ServiceBlueprintLayoutDefinition
            {
                Nodes = new Dictionary<string, NodePosition>
                {
                    ["stage:start"] = new() { X = 312, Y = 168 },
                    ["stage:check"] = new() { X = 312, Y = 472 },
                    ["gateway:route-from-start"] = new() { X = 356, Y = 320 }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(definition, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ServiceBlueprint>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(definition);
        deserialized!.Layout!.Nodes.Should().ContainKey("gateway:route-from-start")
            .WhoseValue.Should().BeEquivalentTo(new NodePosition { X = 356, Y = 320 });
        json.Should().Contain("\"type\": \"heading\"");
        json.Should().Contain("\"type\": \"body\"");
        json.Should().Contain("\"type\": \"fieldset\"");
        json.Should().Contain("\"type\": \"text\"");
        json.Should().Contain("\"type\": \"email\"");
        json.Should().Contain("\"type\": \"summary-list\"");
        json.Should().Contain("\"type\": \"panel\"");
    }

    [Fact]
    public void ConditionalChildren_OnRadios_RoundtripsCorrectly()
    {
        // Arrange
        var component = new RadiosComponent
        {
            FieldKey = "has-partner",
            Label = "Do you have a partner?",
            Required = true,
            Options = new List<string> { "Yes", "No" },
            ConditionalChildren = new Dictionary<string, IReadOnlyList<PrismComponent>>
            {
                ["Yes"] = new List<PrismComponent>
                {
                    new TextInputComponent
                    {
                        FieldKey = "partner-name",
                        Label = "Partner's name",
                        Required = true
                    },
                    new DateInputComponent
                    {
                        FieldKey = "partner-dob",
                        Label = "Partner's date of birth",
                        Required = true
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize<PrismComponent>(component, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PrismComponent>(json, JsonOptions) as RadiosComponent;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(component);
        deserialized!.ConditionalChildren.Should().ContainKey("Yes");
        deserialized.ConditionalChildren!["Yes"].Should().HaveCount(2);
        deserialized.ConditionalChildren["Yes"][0].Should().BeOfType<TextInputComponent>();
        deserialized.ConditionalChildren["Yes"][1].Should().BeOfType<DateInputComponent>();
    }

    [Fact]
    public void NestedContainers_RoundtripCorrectly()
    {
        // Arrange
        var component = new FieldsetComponent
        {
            Legend = "Outer fieldset",
            Children = new List<PrismComponent>
            {
                new HeadingComponent { Level = 3, Content = "Nested heading" },
                new AccordionComponent
                {
                    Sections = new List<AccordionSection>
                    {
                        new()
                        {
                            Heading = "Nested section",
                            Children = new List<PrismComponent>
                            {
                                new TextInputComponent
                                {
                                    FieldKey = "nested-field",
                                    Label = "Nested input",
                                    Required = false
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize<PrismComponent>(component, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PrismComponent>(json, JsonOptions) as FieldsetComponent;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(component);
        deserialized!.Children.Should().HaveCount(2);
        deserialized.Children[0].Should().BeOfType<HeadingComponent>();
        deserialized.Children[1].Should().BeOfType<AccordionComponent>();
    }

    [Fact]
    public void ComponentList_WithMixedTypes_RoundtripsCorrectly()
    {
        // Arrange
        var components = new List<PrismComponent>
        {
            new HeadingComponent { Level = 1, Content = "Application form" },
            new BodyComponent { Content = "Please complete all sections." },
            new InsetTextComponent { Content = "This form will take about 10 minutes." },
            new FieldsetComponent
            {
                Legend = "Personal details",
                Children = new List<PrismComponent>
                {
                    new TextInputComponent { FieldKey = "name", Label = "Name", Required = true },
                    new NumberInputComponent { FieldKey = "age", Label = "Age", Required = true }
                }
            },
            new WarningTextComponent { Content = "You must complete this section." },
            new DetailsComponent { Heading = "Help", Content = "Contact support if needed." }
        };

        // Act
        var json = JsonSerializer.Serialize(components, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<List<PrismComponent>>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().HaveCount(6);
        deserialized![0].Should().BeOfType<HeadingComponent>();
        deserialized[1].Should().BeOfType<BodyComponent>();
        deserialized[2].Should().BeOfType<InsetTextComponent>();
        deserialized[3].Should().BeOfType<FieldsetComponent>();
        deserialized[4].Should().BeOfType<WarningTextComponent>();
        deserialized[5].Should().BeOfType<DetailsComponent>();
    }
}
