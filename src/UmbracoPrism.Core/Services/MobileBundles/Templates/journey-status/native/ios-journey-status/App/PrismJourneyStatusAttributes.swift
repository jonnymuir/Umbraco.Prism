import ActivityKit
import Foundation

/// A journey's status on the lock screen (Design/mobile-journey-status.md). The site sends the
/// fixed parts when it starts the Live Activity, and a new content state with each update.
/// Compiled into the app, which starts and ends activities, and into the PrismJourneyStatus
/// extension, which draws them.
struct PrismJourneyStatusAttributes: ActivityAttributes {
    struct ContentState: Codable, Hashable {
        var status: String
        var step: Int?
        var steps: Int?
    }

    var key: String
    var serviceName: String
    var path: String

    /// The page tapping the status opens, which the app shows in the tab for it.
    var url: URL? {
        URL(string: path, relativeTo: URL(string: "@@PRISM_START_URL@@"))?.absoluteURL
    }
}
