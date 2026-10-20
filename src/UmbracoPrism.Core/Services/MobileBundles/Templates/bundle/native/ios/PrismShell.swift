import Foundation
import os

/// The tenant's site this app shows, and the shell's shared logger. Every shell message is logged
/// under the app's bundle identifier with the category "PrismShell": filter on that in Xcode's
/// console or Console.app.
enum PrismShell {
    static let startURL = URL(string: "@@PRISM_START_URL@@")!
    static let manifestPath = "/umbraco/prism/mobile/manifest"
    static let log = Logger(subsystem: Bundle.main.bundleIdentifier ?? "PrismShell", category: "PrismShell")

    static var ownHost: String { startURL.host ?? "" }

    /// Same scheme, host and port as the tenant's site. Locally the identity provider can share
    /// the host name on another port.
    static func isOwnSite(_ url: URL?) -> Bool {
        guard let url else { return false }
        return url.scheme == startURL.scheme && url.host == startURL.host && url.port == startURL.port
    }

    static var origin: URL {
        var components = URLComponents(url: startURL, resolvingAgainstBaseURL: false)!
        components.path = ""
        components.query = nil
        components.fragment = nil
        return components.url!
    }

    /// The tenant's page at a site-relative path, flagged as an app request.
    static func url(forPath path: String) -> URL {
        var components = URLComponents(url: origin, resolvingAgainstBaseURL: false)!
        let parts = path.split(separator: "?", maxSplits: 1).map(String.init)
        components.path = parts.first.flatMap { $0.isEmpty ? nil : $0 } ?? "/"
        var items = parts.count > 1 ? (URLComponents(string: "?" + parts[1])?.queryItems ?? []) : []
        items.removeAll { $0.name == "prismMobile" }
        items.append(URLQueryItem(name: "prismMobile", value: "1"))
        components.queryItems = items
        return components.url!
    }

    /// A site path without its query or trailing slash, so a tab's path and a link can be compared.
    static func normalizedPath(_ path: String) -> String {
        let withoutQuery = path.split(separator: "?", maxSplits: 1, omittingEmptySubsequences: false).first.map(String.init) ?? "/"
        let trimmed = withoutQuery.count > 1 && withoutQuery.hasSuffix("/") ? String(withoutQuery.dropLast()) : withoutQuery
        return trimmed.isEmpty ? "/" : trimmed
    }

    static func isOwnHost(_ cookieDomain: String) -> Bool {
        let domain = cookieDomain.hasPrefix(".") ? String(cookieDomain.dropFirst()) : cookieDomain
        return ownHost == domain || ownHost.hasSuffix("." + domain)
    }
}
