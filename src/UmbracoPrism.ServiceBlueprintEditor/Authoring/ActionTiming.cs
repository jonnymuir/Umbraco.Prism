using System.Text.Json.Serialization;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Execution phase for an authored action.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActionTiming
{
    /// <summary>Runs when a touchpoint becomes active.</summary>
    OnEntry,

    /// <summary>Runs when a touchpoint is being exited.</summary>
    OnExit,

    /// <summary>Runs while a transition is being taken.</summary>
    OnTransition
}
