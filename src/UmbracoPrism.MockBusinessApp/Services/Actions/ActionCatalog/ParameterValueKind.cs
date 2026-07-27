using System.Text.Json.Serialization;

namespace UmbracoPrism.MockBusinessApp.Services.Actions.ActionCatalog;

/// <summary>
/// Supported JSON value kinds for authored action parameters.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ParameterValueKind
{
    String,
    Number,
    Integer,
    Boolean,
    Object,
    Array,
    Null
}
