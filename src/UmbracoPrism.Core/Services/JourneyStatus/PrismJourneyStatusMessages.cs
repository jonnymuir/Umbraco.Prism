using System.Globalization;
using FirebaseAdmin.Messaging;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services.JourneyStatus;

/// <summary>
/// The Firebase messages that show a status. On iOS they start, update and end a Live Activity
/// drawn by the app's PrismJourneyStatus extension; on Android a data message the app turns into
/// a Live Update.
/// </summary>
internal static class PrismJourneyStatusMessages
{
    /// <summary>The Swift <c>ActivityAttributes</c> type the app's extension draws.</summary>
    public const string AttributesType = "PrismJourneyStatusAttributes";

    /// <summary>Marks a data message as a journey status, so the Android app handles it rather than its push plugin.</summary>
    public const string AndroidMarker = "prismJourneyStatus";

    /// <summary>How long an ended Live Activity keeps showing its last status.</summary>
    public static readonly TimeSpan DoneShowsFor = TimeSpan.FromMinutes(15);

    public static Message IosStart(string pushToken, string startToken, PrismJourneyStatusSchema status, DateTimeOffset now) =>
        Ios(pushToken, startToken, new Dictionary<string, object>
        {
            ["timestamp"] = now.ToUnixTimeSeconds(),
            ["event"] = "start",
            ["content-state"] = ContentState(status),
            ["attributes-type"] = AttributesType,
            ["attributes"] = new Dictionary<string, object>
            {
                ["key"] = status.JourneyKey,
                ["serviceName"] = status.ServiceName,
                ["path"] = status.Path
            },
            ["alert"] = new Dictionary<string, object> { ["title"] = status.ServiceName, ["body"] = status.Status }
        });

    public static Message IosUpdate(string pushToken, string activityToken, PrismJourneyStatusSchema status, DateTimeOffset now) =>
        Ios(pushToken, activityToken, new Dictionary<string, object>
        {
            ["timestamp"] = now.ToUnixTimeSeconds(),
            ["event"] = "update",
            ["content-state"] = ContentState(status)
        });

    public static Message IosEnd(string pushToken, string activityToken, PrismJourneyStatusSchema status, DateTimeOffset now) =>
        Ios(pushToken, activityToken, new Dictionary<string, object>
        {
            ["timestamp"] = now.ToUnixTimeSeconds(),
            ["event"] = "end",
            ["content-state"] = ContentState(status),
            ["dismissal-date"] = now.Add(DoneShowsFor).ToUnixTimeSeconds()
        });

    public static Message Android(string pushToken, PrismJourneyStatus status)
    {
        var data = new Dictionary<string, string>
        {
            [AndroidMarker] = "1",
            ["key"] = status.Key,
            ["kind"] = status.Kind.ToString(),
            ["serviceName"] = status.ServiceName,
            ["status"] = status.Status,
            ["path"] = status.Path
        };
        if (status.Step is { } step && status.Steps is { } steps)
        {
            data["step"] = step.ToString(CultureInfo.InvariantCulture);
            data["steps"] = steps.ToString(CultureInfo.InvariantCulture);
        }

        return new Message { Token = pushToken, Data = data, Android = new AndroidConfig { Priority = Priority.High } };
    }

    private static Message Ios(string pushToken, string liveActivityToken, Dictionary<string, object> aps) => new()
    {
        Token = pushToken,
        Apns = new ApnsConfig
        {
            LiveActivityToken = liveActivityToken,
            Headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
            Aps = new Aps { CustomData = aps }
        }
    };

    private static Dictionary<string, object> ContentState(PrismJourneyStatusSchema status)
    {
        var state = new Dictionary<string, object> { ["status"] = status.Status };
        if (status.Step is { } step && status.Steps is { } steps)
        {
            state["step"] = step;
            state["steps"] = steps;
        }

        return state;
    }
}
