import Foundation
import WebKit

struct PrismManifest: Codable, Equatable {
    struct Tab: Codable, Equatable {
        let label: String
        let path: String
        let icon: String?
    }

    let version: Int
    let tabs: [Tab]
    let sessionCookie: String?

    static let fallback = PrismManifest(version: 1, tabs: [Tab(label: "Home", path: "/", icon: "home")], sessionCookie: nil)
}

/// Keeps the last manifest the server sent, so the app starts with the right tabs even offline.
enum PrismManifestStore {
    private static let cacheKey = "prism.manifest"

    static func cached() -> PrismManifest {
        guard let data = UserDefaults.standard.data(forKey: cacheKey),
              let manifest = try? JSONDecoder().decode(PrismManifest.self, from: data),
              !manifest.tabs.isEmpty else { return .fallback }
        return manifest
    }

    /// Fetches the manifest as the signed-in member (the webviews' cookies), since the tabs a
    /// member can use depend on who they are.
    static func refresh(completion: @escaping (PrismManifest?) -> Void) {
        WKWebsiteDataStore.default().httpCookieStore.getAllCookies { cookies in
            var request = URLRequest(url: URL(string: PrismShell.manifestPath, relativeTo: PrismShell.origin)!.absoluteURL)
            request.cachePolicy = .reloadIgnoringLocalCacheData
            request.httpShouldHandleCookies = false
            let ownCookies = cookies.filter { PrismShell.isOwnHost($0.domain) }
            for (name, value) in HTTPCookie.requestHeaderFields(with: ownCookies) {
                request.setValue(value, forHTTPHeaderField: name)
            }
            URLSession.shared.dataTask(with: request) { data, response, error in
                let manifest = decode(data, response, error)
                if manifest != nil, let data {
                    UserDefaults.standard.set(data, forKey: cacheKey)
                }
                DispatchQueue.main.async { completion(manifest) }
            }.resume()
        }
    }

    /// The manifest in a response, or nil (logged) when the app keeps its cached tabs.
    private static func decode(_ data: Data?, _ response: URLResponse?, _ error: Error?) -> PrismManifest? {
        let status = (response as? HTTPURLResponse)?.statusCode
        guard status == 200, let data, let manifest = try? JSONDecoder().decode(PrismManifest.self, from: data), !manifest.tabs.isEmpty else {
            PrismShell.log.warning("Manifest not refreshed (status \(status.map(String.init) ?? "none", privacy: .public), error \(error?.localizedDescription ?? "none", privacy: .public)); keeping the cached tabs")
            return nil
        }
        PrismShell.log.info("Manifest refreshed: \(manifest.tabs.count) tabs")
        return manifest
    }
}
