import ActivityKit
import UIKit
import WebKit

/// Tells the site how to reach this iPhone's journey statuses: the token that starts a Live
/// Activity, and each running activity's own token. Posted natively with the member's session
/// cookie, since iOS can start an activity, and wake the app briefly to report its token, while
/// no page is open. Signing out ends every activity.
@MainActor
final class PrismJourneyStatusDevice {
    static let shared = PrismJourneyStatusDevice()

    private var startToken: String?
    private var registered = false
    private var watched = Set<String>()

    func start() {
        Task {
            for await token in Activity<PrismJourneyStatusAttributes>.pushToStartTokenUpdates {
                startToken = token.hexString
                registered = false
                await registerDevice()
            }
        }
        Task {
            for await activity in Activity<PrismJourneyStatusAttributes>.activityUpdates {
                watch(activity)
            }
        }
        Activity<PrismJourneyStatusAttributes>.activities.forEach(watch)

        let center = NotificationCenter.default
        center.addObserver(forName: UIApplication.willEnterForegroundNotification, object: nil, queue: .main) { _ in
            Task { @MainActor in await PrismJourneyStatusDevice.shared.registerDevice() }
        }
        center.addObserver(forName: PrismShell.sessionChanged, object: nil, queue: .main) { notification in
            let signedIn = notification.userInfo?[PrismShell.signedInKey] as? Bool ?? false
            Task { @MainActor in await PrismJourneyStatusDevice.shared.sessionChanged(signedIn: signedIn) }
        }
    }

    private func watch(_ activity: Activity<PrismJourneyStatusAttributes>) {
        guard watched.insert(activity.id).inserted else { return }
        Task {
            for await token in activity.pushTokenUpdates {
                await post("activity", ["key": activity.attributes.key, "token": token.hexString])
            }
        }
    }

    private func sessionChanged(signedIn: Bool) async {
        registered = false
        if signedIn {
            await registerDevice()
            return
        }
        PrismShell.log.info("Signed out; ending journey statuses")
        for activity in Activity<PrismJourneyStatusAttributes>.activities {
            await activity.end(nil, dismissalPolicy: .immediate)
        }
    }

    /// Until push notifications are registered, which the site's page does after sign-in, the site
    /// answers 409, so the app tries again when next brought to the front.
    private func registerDevice() async {
        guard !registered, let startToken else { return }
        registered = await post("device", ["platform": "ios", "startToken": startToken])
    }

    @discardableResult
    private func post(_ endpoint: String, _ body: [String: String]) async -> Bool {
        let cookies = await WKWebsiteDataStore.default().httpCookieStore.allCookies()
            .filter { PrismShell.isOwnHost($0.domain) }
        var request = URLRequest(url: PrismShell.origin.appendingPathComponent("umbraco/prism/journey-status/\(endpoint)"))
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        HTTPCookie.requestHeaderFields(with: cookies).forEach { request.setValue($1, forHTTPHeaderField: $0) }
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)

        do {
            let (_, response) = try await URLSession.shared.data(for: request, delegate: NoRedirects())
            let status = (response as? HTTPURLResponse)?.statusCode ?? 0
            PrismShell.log.info("Journey status \(endpoint, privacy: .public) sent: \(status)")
            return status == 204
        } catch {
            PrismShell.log.error("Journey status \(endpoint, privacy: .public) not sent: \(error.localizedDescription, privacy: .public)")
            return false
        }
    }
}

/// Signed out, the site redirects to its sign-in page; the answer that matters is the redirect.
private final class NoRedirects: NSObject, URLSessionTaskDelegate {
    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse, newRequest request: URLRequest) async -> URLRequest? {
        nil
    }
}

private extension Data {
    var hexString: String { map { String(format: "%02x", $0) }.joined() }
}
