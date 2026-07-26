using System.Text.Json.Serialization;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GatewayKind
{
    Split,
    Join
}
