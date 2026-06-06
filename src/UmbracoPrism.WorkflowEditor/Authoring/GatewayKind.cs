using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GatewayKind
{
    Split,
    Join
}
