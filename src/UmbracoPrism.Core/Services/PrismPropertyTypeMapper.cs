namespace UmbracoPrism.Core.Services;

/// <summary>
/// Maps Umbraco property editor aliases to workflow render field types.
/// </summary>
public static class PrismPropertyTypeMapper
{
    /// <summary>
    /// Maps a Umbraco property editor alias to a workflow field type hint.
    /// Returns "text" as a safe fallback for unmapped editors.
    /// </summary>
    public static string ToFieldType(string propertyEditorAlias) => propertyEditorAlias switch
    {
        "Umbraco.TextBox"             => "text",
        "Umbraco.TextString"          => "text",
        "Umbraco.TextArea"            => "textarea",
        "Umbraco.EmailAddress"        => "email",
        "Umbraco.Integer"             => "number",
        "Umbraco.Decimal"             => "decimal",
        "Umbraco.TrueFalse"           => "boolean",
        "Umbraco.DropDown.Flexible"   => "select",
        "Umbraco.CheckBoxList"        => "checkboxlist",
        "Umbraco.RadioButtonList"     => "radio",
        "Umbraco.DateTime"            => "datetime",
        "Umbraco.Date"                => "date",
        "Umbraco.Slider"              => "slider",
        "Umbraco.MultipleTextstring"  => "multitextstring",
        _                             => "text"
    };
}
