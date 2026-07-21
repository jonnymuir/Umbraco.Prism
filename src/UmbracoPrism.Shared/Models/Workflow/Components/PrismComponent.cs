using System.Text.Json.Serialization;

namespace UmbracoPrism.Shared.Models.Workflow.Components;

/// <summary>
/// Abstract base for all workflow component types in the v2.0 schema.
/// Enables polymorphic JSON serialization with type discriminator "type".
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FieldsetComponent), typeDiscriminator: "fieldset")]
[JsonDerivedType(typeof(AccordionComponent), typeDiscriminator: "accordion")]
[JsonDerivedType(typeof(PanelComponent), typeDiscriminator: "panel")]
[JsonDerivedType(typeof(TextInputComponent), typeDiscriminator: "text")]
[JsonDerivedType(typeof(NumberInputComponent), typeDiscriminator: "number")]
[JsonDerivedType(typeof(DecimalInputComponent), typeDiscriminator: "decimal")]
[JsonDerivedType(typeof(SelectComponent), typeDiscriminator: "select")]
[JsonDerivedType(typeof(RadiosComponent), typeDiscriminator: "radio")]
[JsonDerivedType(typeof(CheckboxesComponent), typeDiscriminator: "checkboxlist")]
[JsonDerivedType(typeof(DateInputComponent), typeDiscriminator: "date")]
[JsonDerivedType(typeof(EmailComponent), typeDiscriminator: "email")]
[JsonDerivedType(typeof(TextareaComponent), typeDiscriminator: "textarea")]
[JsonDerivedType(typeof(BooleanComponent), typeDiscriminator: "boolean")]
[JsonDerivedType(typeof(BodyComponent), typeDiscriminator: "body")]
[JsonDerivedType(typeof(HeadingComponent), typeDiscriminator: "heading")]
[JsonDerivedType(typeof(InsetTextComponent), typeDiscriminator: "inset-text")]
[JsonDerivedType(typeof(WarningTextComponent), typeDiscriminator: "warning-text")]
[JsonDerivedType(typeof(DetailsComponent), typeDiscriminator: "details")]
[JsonDerivedType(typeof(NotificationBannerComponent), typeDiscriminator: "notification-banner")]
[JsonDerivedType(typeof(WaitingComponent), typeDiscriminator: "waiting")]
[JsonDerivedType(typeof(SummaryListComponent), typeDiscriminator: "summary-list")]
[JsonDerivedType(typeof(TaskListComponent), typeDiscriminator: "task-list")]
[JsonDerivedType(typeof(FileUploadComponent), typeDiscriminator: "file-upload")]
[JsonDerivedType(typeof(GuidanceChecklistComponent), typeDiscriminator: "guidance-checklist")]
[JsonDerivedType(typeof(SliderComponent), typeDiscriminator: "slider")]
[JsonDerivedType(typeof(StatGroupComponent), typeDiscriminator: "stat-group")]
[JsonDerivedType(typeof(ChartComponent), typeDiscriminator: "chart")]
public abstract record PrismComponent
{
    /// <summary>
    /// Optional visibility expression evaluated against the workflow's calculation scope
    /// (inputs, service inputs and calculated fields). When it evaluates to false the
    /// component renders hidden; the live-form runtime re-evaluates it as inputs change.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShowWhen { get; init; }
}
