
echo "Writing the native tab shell..."
if [ -d ios/App/App ]; then
  cat > ios/App/App/PrismShell.swift << 'PRISM_SWIFT_EOF'
import AuthenticationServices
import Capacitor
import UIKit
import WebKit

enum PrismShell {
    static let startURL = URL(string: "@@PRISM_START_URL@@")!
    static let manifestPath = "/umbraco/prism/mobile/manifest"

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

    static func isOwnHost(_ cookieDomain: String) -> Bool {
        let domain = cookieDomain.hasPrefix(".") ? String(cookieDomain.dropFirst()) : cookieDomain
        return ownHost == domain || ownHost.hasSuffix("." + domain)
    }
}

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
            URLSession.shared.dataTask(with: request) { data, response, _ in
                var manifest: PrismManifest?
                if let http = response as? HTTPURLResponse, http.statusCode == 200, let data,
                   let decoded = try? JSONDecoder().decode(PrismManifest.self, from: data), !decoded.tabs.isEmpty {
                    UserDefaults.standard.set(data, forKey: cacheKey)
                    manifest = decoded
                }
                DispatchQueue.main.async { completion(manifest) }
            }.resume()
        }
    }
}

/// The native tab bar. Each tab is its own Capacitor bridge and webview, created the first time
/// the tab is shown, so switching tabs never reloads a page and keeps each tab's place.
final class PrismTabBarController: UITabBarController, UITabBarControllerDelegate, WKHTTPCookieStoreObserver {
    private var manifest = PrismManifestStore.cached()
    private var hasSession: Bool?
    private let iconLoader = PrismTabIconLoader()

    override func viewDidLoad() {
        super.viewDidLoad()
        delegate = self
        tabBar.isTranslucent = false
        let appearance = UITabBarAppearance()
        appearance.configureWithOpaqueBackground()
        tabBar.standardAppearance = appearance
        tabBar.scrollEdgeAppearance = appearance
        view.backgroundColor = .systemBackground

        apply(manifest)
        WKWebsiteDataStore.default().httpCookieStore.add(self)
        refreshManifest()
    }

    private func apply(_ newManifest: PrismManifest) {
        manifest = newManifest
        let existing = (viewControllers ?? []).compactMap { $0 as? PrismWebTabViewController }
        let previousTabs = existing.map(\.prismTab)
        let selectedPath = (selectedViewController as? PrismWebTabViewController)?.prismTab.path

        let tabs = newManifest.tabs.map { tab -> PrismWebTabViewController in
            let controller = existing.first { $0.prismTab.path == tab.path } ?? PrismWebTabViewController(tab: tab)
            controller.prismTab = tab
            controller.tabBarItem = UITabBarItem(title: tab.label, image: iconLoader.placeholder(for: tab.icon), selectedImage: nil)
            iconLoader.load(tab.icon, relativeTo: PrismShell.origin) { [weak controller] image in
                controller?.tabBarItem.image = image
            }
            return controller
        }

        guard newManifest.tabs != previousTabs || viewControllers == nil else { return }
        setViewControllers(tabs, animated: false)
        if let selectedPath, let index = tabs.firstIndex(where: { $0.prismTab.path == selectedPath }) {
            selectedIndex = index
        }
        routeNotificationsToSelectedTab()
    }

    private func refreshManifest() {
        PrismManifestStore.refresh { [weak self] fresh in
            guard let self, let fresh, fresh != self.manifest else { return }
            self.apply(fresh)
        }
    }

    func tabBarController(_ tabBarController: UITabBarController, shouldSelect viewController: UIViewController) -> Bool {
        if viewController === selectedViewController, let tab = viewController as? PrismWebTabViewController {
            tab.returnToRoot()
        }
        return true
    }

    func tabBarController(_ tabBarController: UITabBarController, didSelect viewController: UIViewController) {
        (viewController as? PrismWebTabViewController)?.reloadIfStale()
        routeNotificationsToSelectedTab()
    }

    /// Every bridge makes itself the notification delegate when it is created, so taps would reach
    /// whichever tab was opened last. The selected tab is the one that should handle them.
    private func routeNotificationsToSelectedTab() {
        guard let tab = selectedViewController as? PrismWebTabViewController, let router = tab.notificationRouter else { return }
        UNUserNotificationCenter.current().delegate = router
    }

    /// A sign-in or sign-out in one tab leaves the others showing the old state, so they reload
    /// when next shown, and the tabs themselves are fetched again for the new member.
    func cookiesDidChange(in cookieStore: WKHTTPCookieStore) {
        let sessionCookie = manifest.sessionCookie ?? "PrismMemberCookie"
        cookieStore.getAllCookies { [weak self] cookies in
            let signedIn = cookies.contains { $0.name.hasPrefix(sessionCookie) && PrismShell.isOwnHost($0.domain) }
            DispatchQueue.main.async {
                guard let self else { return }
                defer { self.hasSession = signedIn }
                guard let previous = self.hasSession, previous != signedIn else { return }
                for case let tab as PrismWebTabViewController in self.viewControllers ?? [] where tab !== self.selectedViewController {
                    tab.markStale()
                }
                self.refreshManifest()
            }
        }
    }
}

/// One tab: a Capacitor bridge pinned below the status bar only while it shows another site's
/// page (a hosted sign-in page cannot be trusted to keep clear of the status bar the way this
/// app's own pages do).
final class PrismWebTabViewController: UIViewController {
    var prismTab: PrismManifest.Tab
    private var bridgeController: PrismBridgeViewController?
    private var topConstraint: NSLayoutConstraint?
    private var isStale = false

    init(tab: PrismManifest.Tab) {
        self.prismTab = tab
        super.init(nibName: nil, bundle: nil)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) is not supported")
    }

    var notificationRouter: UNUserNotificationCenterDelegate? {
        _ = view
        return bridgeController?.bridge?.notificationRouter
    }

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = .systemBackground
        let bridge = PrismBridgeViewController(startURL: PrismShell.url(forPath: prismTab.path))
        bridge.onPageChange = { [weak self] url in self?.reserveStatusBar(!PrismShell.isOwnSite(url)) }
        addChild(bridge)
        view.addSubview(bridge.view)
        bridge.view.translatesAutoresizingMaskIntoConstraints = false
        let top = bridge.view.topAnchor.constraint(equalTo: view.topAnchor)
        NSLayoutConstraint.activate([
            top,
            bridge.view.bottomAnchor.constraint(equalTo: view.safeAreaLayoutGuide.bottomAnchor),
            bridge.view.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            bridge.view.trailingAnchor.constraint(equalTo: view.trailingAnchor)
        ])
        bridge.didMove(toParent: self)
        bridgeController = bridge
        topConstraint = top
    }

    private func reserveStatusBar(_ reserve: Bool) {
        let target = reserve ? view.safeAreaInsets.top : 0
        guard let topConstraint, topConstraint.constant != target else { return }
        topConstraint.constant = target
        view.layoutIfNeeded()
    }

    func markStale() {
        isStale = true
    }

    func reloadIfStale() {
        guard isStale, isViewLoaded else { return }
        isStale = false
        bridgeController?.webView?.reload()
    }

    func returnToRoot() {
        bridgeController?.webView?.load(URLRequest(url: PrismShell.url(forPath: prismTab.path)))
    }
}

/// A Capacitor bridge that opens at a given page of the tenant's site.
final class PrismBridgeViewController: CAPBridgeViewController {
    private let startURL: URL
    private var navigationDecorator: PrismNavigationDecorator?
    var onPageChange: ((URL?) -> Void)?

    init(startURL: URL) {
        self.startURL = startURL
        super.init(nibName: nil, bundle: nil)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) is not supported")
    }

    override func instanceDescriptor() -> InstanceDescriptor {
        let descriptor = super.instanceDescriptor()
        descriptor.serverURL = startURL.absoluteString
        return descriptor
    }

    // Script and plugin registration goes here, not in webViewConfiguration(for:): Capacitor
    // replaces that configuration's content controller before it builds the webview.
    override func capacitorDidLoad() {
        super.capacitorDidLoad()
        guard let webView else { return }

        // WebKit zooms into a focused input whenever a page's viewport allows scaling, which
        // breaks hosted sign-in pages this app cannot edit. viewport-fit=cover lets this app's own
        // pages draw under the status bar.
        let viewportFix = "(function(){function pin(){var m=document.querySelector('meta[name=viewport]');if(!m){m=document.createElement('meta');m.name='viewport';document.head.appendChild(m);}m.content='width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover';}if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',pin);}else{pin();}})();"
        webView.configuration.userContentController.addUserScript(
            WKUserScript(source: viewportFix, injectionTime: .atDocumentStart, forMainFrameOnly: false))

        bridge?.registerPluginInstance(PrismIdentityCookiePlugin())
        bridge?.registerPluginInstance(PrismSystemSignInPlugin())
    }

    override func viewDidLoad() {
        super.viewDidLoad()
        guard let webView else { return }
        let decorator = PrismNavigationDecorator(
            navigationTarget: webView.navigationDelegate,
            uiTarget: webView.uiDelegate,
            isHostTrustedInApp: { [weak self] host in self?.bridge?.config.shouldAllowNavigation(to: host) ?? false },
            onPageChange: { [weak self] url in self?.onPageChange?(url) })
        navigationDecorator = decorator
        webView.navigationDelegate = decorator
        webView.uiDelegate = decorator
    }
}

/// Wraps Capacitor's own navigation and UI delegates, forwarding everything it does not handle,
/// so Capacitor's navigation policy keeps working unchanged.
private final class PrismNavigationDecorator: NSObject, WKNavigationDelegate, WKUIDelegate {
    private let navigationTarget: WKNavigationDelegate?
    private let uiTarget: WKUIDelegate?
    private let isHostTrustedInApp: (String) -> Bool
    private let onPageChange: (URL?) -> Void

    init(navigationTarget: WKNavigationDelegate?, uiTarget: WKUIDelegate?,
         isHostTrustedInApp: @escaping (String) -> Bool, onPageChange: @escaping (URL?) -> Void) {
        self.navigationTarget = navigationTarget
        self.uiTarget = uiTarget
        self.isHostTrustedInApp = isHostTrustedInApp
        self.onPageChange = onPageChange
    }

    override func responds(to aSelector: Selector!) -> Bool {
        super.responds(to: aSelector) || navigationTarget?.responds(to: aSelector) == true || uiTarget?.responds(to: aSelector) == true
    }

    override func forwardingTarget(for aSelector: Selector!) -> Any? {
        if super.responds(to: aSelector) { return nil }
        if navigationTarget?.responds(to: aSelector) == true { return navigationTarget }
        return uiTarget
    }

    func webView(_ webView: WKWebView, didStartProvisionalNavigation navigation: WKNavigation!) {
        onPageChange(webView.url)
        navigationTarget?.webView?(webView, didStartProvisionalNavigation: navigation)
    }

    func webView(_ webView: WKWebView, didReceiveServerRedirectForProvisionalNavigation navigation: WKNavigation!) {
        onPageChange(webView.url)
        navigationTarget?.webView?(webView, didReceiveServerRedirectForProvisionalNavigation: navigation)
    }

    func webView(_ webView: WKWebView, didCommit navigation: WKNavigation!) {
        onPageChange(webView.url)
        navigationTarget?.webView?(webView, didCommit: navigation)
    }

    // Capacitor sends every window.open() to Safari, even for this app's own pages (sign-out uses
    // one). Trusted hosts stay in the tab.
    func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration,
                 for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
        if let host = navigationAction.request.url?.host, isHostTrustedInApp(host) {
            webView.load(navigationAction.request)
            return nil
        }
        return uiTarget?.webView?(webView, createWebViewWith: configuration, for: navigationAction, windowFeatures: windowFeatures) ?? nil
    }
}

/// Tab icons: a built-in name maps to an SF Symbol; an image URL (a media item) is downloaded
/// once and cached. UIImage cannot decode SVG, so SVG icons are drawn by PrismSvgIcon.
final class PrismTabIconLoader {
    private static let symbols = [
        "home": "house", "dashboard": "square.grid.2x2", "account": "person.crop.circle",
        "settings": "gearshape", "transactions": "creditcard", "notifications": "bell", "more": "ellipsis"
    ]
    private static let size = CGSize(width: 25, height: 25)

    func placeholder(for icon: String?) -> UIImage? {
        UIImage(systemName: icon.flatMap { Self.symbols[$0] } ?? "circle")
    }

    func load(_ icon: String?, relativeTo origin: URL, completion: @escaping (UIImage) -> Void) {
        guard let icon, Self.symbols[icon] == nil, let url = URL(string: icon, relativeTo: origin)?.absoluteURL else { return }
        let cacheFile = Self.cacheFile(for: url)
        // A cached PNG loads at scale 1, so it is drawn back into the tab icon size like a download.
        if let cached = UIImage(contentsOfFile: cacheFile.path) {
            completion(Self.tabImage(cached))
            return
        }
        URLSession.shared.dataTask(with: url) { data, _, _ in
            guard let data else { return }
            let decoded = UIImage(data: data) ?? String(data: data, encoding: .utf8).flatMap { PrismSvgIcon.render($0, size: Self.size) }
            guard let decoded else { return }
            let image = Self.tabImage(decoded)
            try? image.pngData()?.write(to: cacheFile)
            DispatchQueue.main.async { completion(image) }
        }.resume()
    }

    private static func tabImage(_ image: UIImage) -> UIImage {
        UIGraphicsImageRenderer(size: size).image { _ in image.draw(in: CGRect(origin: .zero, size: size)) }
            .withRenderingMode(.alwaysTemplate)
    }

    private static func cacheFile(for url: URL) -> URL {
        let name = url.absoluteString.unicodeScalars.map { CharacterSet.alphanumerics.contains($0) ? String($0) : "_" }.joined()
        return FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask)[0].appendingPathComponent("prism-tab-\(name).png")
    }
}

/// Draws an icon-style SVG: the viewBox and every <path>'s d attribute, filled. That covers the
/// usual single-colour icon sets; anything else returns nil and the tab keeps its placeholder.
enum PrismSvgIcon {
    static func render(_ svg: String, size: CGSize) -> UIImage? {
        let paths = attributeValues(named: "d", inElementsNamed: "path", in: svg)
        guard !paths.isEmpty else { return nil }
        let viewBox = (attributeValues(named: "viewBox", inElementsNamed: "svg", in: svg).first ?? "0 0 24 24")
            .split(whereSeparator: { $0 == " " || $0 == "," }).compactMap { Double($0) }
        guard viewBox.count == 4, viewBox[2] > 0, viewBox[3] > 0 else { return nil }

        let combined = UIBezierPath()
        for d in paths {
            guard let path = parsePath(d) else { return nil }
            combined.append(path)
        }
        let scale = min(size.width / viewBox[2], size.height / viewBox[3])
        combined.apply(CGAffineTransform(translationX: -viewBox[0], y: -viewBox[1]))
        combined.apply(CGAffineTransform(scaleX: scale, y: scale))

        return UIGraphicsImageRenderer(size: size).image { _ in
            UIColor.black.setFill()
            combined.fill()
        }
    }

    private static func attributeValues(named attribute: String, inElementsNamed element: String, in svg: String) -> [String] {
        let pattern = "<\(element)\\b[^>]*?\\s\(attribute)\\s*=\\s*\"([^\"]*)\""
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else { return [] }
        let range = NSRange(svg.startIndex..., in: svg)
        return regex.matches(in: svg, range: range).compactMap { Range($0.range(at: 1), in: svg).map { String(svg[$0]) } }
    }

    // SVG path data: commands M L H V C S Q T A Z, absolute or relative.
    static func parsePath(_ d: String) -> UIBezierPath? {
        var scanner = PathScanner(Array(d.unicodeScalars))
        let path = UIBezierPath()
        var current = CGPoint.zero, start = CGPoint.zero, lastControl: CGPoint?
        var command: Character = " ", previousCommand: Character = " "

        while scanner.skipSeparators() {
            if let next = scanner.command() {
                command = next
            } else if command == "M" {
                command = "L"
            } else if command == "m" {
                command = "l"
            } else if command == " " {
                return nil
            }
            let relative = command.isLowercase
            let origin = relative ? current : .zero
            func point() -> CGPoint? {
                guard let x = scanner.number(), let y = scanner.number() else { return nil }
                return CGPoint(x: origin.x + x, y: origin.y + y)
            }

            switch command.uppercased().first! {
            case "M":
                guard let p = point() else { return nil }
                path.move(to: p)
                current = p; start = p; lastControl = nil
            case "L":
                guard let p = point() else { return nil }
                path.addLine(to: p)
                current = p; lastControl = nil
            case "H":
                guard let x = scanner.number() else { return nil }
                current = CGPoint(x: (relative ? current.x : 0) + x, y: current.y)
                path.addLine(to: current); lastControl = nil
            case "V":
                guard let y = scanner.number() else { return nil }
                current = CGPoint(x: current.x, y: (relative ? current.y : 0) + y)
                path.addLine(to: current); lastControl = nil
            case "C":
                guard let c1 = point(), let c2 = point(), let p = point() else { return nil }
                path.addCurve(to: p, controlPoint1: c1, controlPoint2: c2)
                current = p; lastControl = c2
            case "S":
                guard let c2 = point(), let p = point() else { return nil }
                let c1 = "CScs".contains(previousCommand) ? reflect(lastControl, about: current) : current
                path.addCurve(to: p, controlPoint1: c1, controlPoint2: c2)
                current = p; lastControl = c2
            case "Q":
                guard let c = point(), let p = point() else { return nil }
                path.addQuadCurve(to: p, controlPoint: c)
                current = p; lastControl = c
            case "T":
                guard let p = point() else { return nil }
                let c = "QTqt".contains(previousCommand) ? reflect(lastControl, about: current) : current
                path.addQuadCurve(to: p, controlPoint: c)
                current = p; lastControl = c
            case "A":
                guard let rx = scanner.number(), let ry = scanner.number(), let rotation = scanner.number(),
                      let largeArc = scanner.flag(), let sweep = scanner.flag(), let p = point() else { return nil }
                addArc(to: path, from: current, to: p, rx: rx, ry: ry, rotation: rotation, largeArc: largeArc, sweep: sweep)
                current = p; lastControl = nil
            case "Z":
                path.close()
                current = start; lastControl = nil
            default:
                return nil
            }
            previousCommand = command
        }
        return path
    }

    private static func reflect(_ control: CGPoint?, about point: CGPoint) -> CGPoint {
        guard let control else { return point }
        return CGPoint(x: 2 * point.x - control.x, y: 2 * point.y - control.y)
    }

    // SVG elliptical arc, converted from endpoint to centre form (SVG 1.1 appendix F.6.5) and
    // drawn as a transformed unit-circle arc.
    private static func addArc(to path: UIBezierPath, from p0: CGPoint, to p1: CGPoint,
                               rx rxIn: Double, ry ryIn: Double, rotation: Double, largeArc: Bool, sweep: Bool) {
        var rx = abs(rxIn), ry = abs(ryIn)
        guard rx > 0, ry > 0, p0 != p1 else {
            path.addLine(to: p1)
            return
        }
        let phi = rotation * .pi / 180
        let dx = (p0.x - p1.x) / 2, dy = (p0.y - p1.y) / 2
        let x1 = cos(phi) * dx + sin(phi) * dy
        let y1 = -sin(phi) * dx + cos(phi) * dy
        let lambda = (x1 * x1) / (rx * rx) + (y1 * y1) / (ry * ry)
        if lambda > 1 { rx *= sqrt(lambda); ry *= sqrt(lambda) }
        let numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1
        let denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1
        var coefficient = sqrt(max(0, numerator / denominator))
        if largeArc == sweep { coefficient = -coefficient }
        let cxPrime = coefficient * rx * y1 / ry
        let cyPrime = -coefficient * ry * x1 / rx
        let cx = cos(phi) * cxPrime - sin(phi) * cyPrime + (p0.x + p1.x) / 2
        let cy = sin(phi) * cxPrime + cos(phi) * cyPrime + (p0.y + p1.y) / 2

        func angle(_ ux: Double, _ uy: Double, _ vx: Double, _ vy: Double) -> Double {
            atan2(ux * vy - uy * vx, ux * vx + uy * vy)
        }
        let startAngle = angle(1, 0, (x1 - cxPrime) / rx, (y1 - cyPrime) / ry)
        var delta = angle((x1 - cxPrime) / rx, (y1 - cyPrime) / ry, (-x1 - cxPrime) / rx, (-y1 - cyPrime) / ry)
        if !sweep && delta > 0 { delta -= 2 * .pi }
        if sweep && delta < 0 { delta += 2 * .pi }

        let unitArc = UIBezierPath(arcCenter: .zero, radius: 1, startAngle: startAngle, endAngle: startAngle + delta, clockwise: delta > 0)
        unitArc.apply(CGAffineTransform(scaleX: rx, y: ry)
            .concatenating(CGAffineTransform(rotationAngle: phi))
            .concatenating(CGAffineTransform(translationX: cx, y: cy)))
        unitArc.cgPath.applyWithBlock { element in
            let points = element.pointee.points
            switch element.pointee.type {
            case .addLineToPoint: path.addLine(to: points[0])
            case .addQuadCurveToPoint: path.addQuadCurve(to: points[1], controlPoint: points[0])
            case .addCurveToPoint: path.addCurve(to: points[2], controlPoint1: points[0], controlPoint2: points[1])
            default: break
            }
        }
        path.addLine(to: p1)
    }

    private struct PathScanner {
        private let chars: [Unicode.Scalar]
        private var index = 0

        init(_ chars: [Unicode.Scalar]) {
            self.chars = chars
        }

        /// Skips whitespace and commas; false at the end of the data.
        mutating func skipSeparators() -> Bool {
            while index < chars.count, chars[index] == "," || chars[index].properties.isWhitespace { index += 1 }
            return index < chars.count
        }

        mutating func command() -> Character? {
            guard skipSeparators(), "MmLlHhVvCcSsQqTtAaZz".unicodeScalars.contains(chars[index]) else { return nil }
            defer { index += 1 }
            return Character(chars[index])
        }

        mutating func flag() -> Bool? {
            guard skipSeparators(), chars[index] == "0" || chars[index] == "1" else { return nil }
            defer { index += 1 }
            return chars[index] == "1"
        }

        // Numbers may run together: "1.5.5" is 1.5 then .5, "2-3" is 2 then -3.
        mutating func number() -> Double? {
            guard skipSeparators() else { return nil }
            let begin = index
            if chars[index] == "-" || chars[index] == "+" { index += 1 }
            var seenDot = false, seenDigit = false
            while index < chars.count {
                let c = chars[index]
                if c.properties.numericType != nil && ("0"..."9").contains(c) { seenDigit = true; index += 1 }
                else if c == "." && !seenDot { seenDot = true; index += 1 }
                else if (c == "e" || c == "E") && seenDigit {
                    index += 1
                    if index < chars.count, chars[index] == "-" || chars[index] == "+" { index += 1 }
                } else { break }
            }
            guard seenDigit else {
                index = begin
                return nil
            }
            return Double(String(String.UnicodeScalarView(chars[begin..<index])))
        }
    }
}

/// Removes the identity provider's own session cookies from the shared cookie store on sign-out
/// (see prism-biometric-signout.js), since the provider's pages cannot be asked to do it.
@objc(PrismIdentityCookiePlugin)
public class PrismIdentityCookiePlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "PrismIdentityCookiePlugin"
    public let jsName = "PrismIdentityCookiePlugin"
    public let pluginMethods: [CAPPluginMethod] = [
        .init(#selector(clearCookies))
    ]

    @objc func clearCookies(_ call: CAPPluginCall) {
        let hosts = call.getArray("hosts", String.self) ?? []
        guard !hosts.isEmpty else {
            call.resolve(["cleared": 0])
            return
        }

        let store = WKWebsiteDataStore.default()
        store.fetchDataRecords(ofTypes: [WKWebsiteDataTypeCookies]) { records in
            let matching = records.filter { record in
                hosts.contains { host in record.displayName == host || record.displayName.hasSuffix("." + host) }
            }
            guard !matching.isEmpty else {
                DispatchQueue.main.async { call.resolve(["cleared": 0]) }
                return
            }
            store.removeData(ofTypes: [WKWebsiteDataTypeCookies], for: matching) {
                DispatchQueue.main.async { call.resolve(["cleared": matching.count]) }
            }
        }
    }
}
/// Opens the system sign-in sheet for prism-system-sign-in.js and returns the callback URL. The
/// callback is an https URL on the tenant's own site, which iOS hands only to this app because
/// the site's apple-app-site-association names it. The session is ephemeral, so no identity
/// provider session outlives the sign-in.
@objc(PrismSystemSignInPlugin)
public class PrismSystemSignInPlugin: CAPPlugin, CAPBridgedPlugin, ASWebAuthenticationPresentationContextProviding {
    public let identifier = "PrismSystemSignInPlugin"
    public let jsName = "PrismSystemSignIn"
    public let pluginMethods: [CAPPluginMethod] = [
        .init(name: "authenticate", returnType: CAPPluginReturnPromise)
    ]
    private var session: ASWebAuthenticationSession?

    @objc func authenticate(_ call: CAPPluginCall) {
        guard let url = call.getString("url").flatMap(URL.init(string:)), PrismShell.isOwnSite(url),
              call.getString("callbackHost") == PrismShell.ownHost, let path = call.getString("callbackPath") else {
            call.reject("Sign-in must start and end on this app's own site.")
            return
        }

        DispatchQueue.main.async {
            let session = ASWebAuthenticationSession(url: url, callback: .https(host: PrismShell.ownHost, path: path)) { [weak self] callbackURL, error in
                self?.session = nil
                if let callbackURL {
                    call.resolve(["url": callbackURL.absoluteString])
                } else {
                    let cancelled = (error as? ASWebAuthenticationSessionError)?.code == .canceledLogin
                    call.reject(error?.localizedDescription ?? "Sign-in failed.", cancelled ? "CANCELLED" : "FAILED", error)
                }
            }
            session.prefersEphemeralWebBrowserSession = true
            session.presentationContextProvider = self
            self.session = session
            if !session.start() {
                self.session = nil
                call.reject("Sign-in could not start.", "FAILED")
            }
        }
    }

    public func presentationAnchor(for session: ASWebAuthenticationSession) -> ASPresentationAnchor {
        bridge?.viewController?.view.window ?? ASPresentationAnchor()
    }
}
PRISM_SWIFT_EOF
  rm -f ios/App/App/PrismBridgeViewController.swift
  echo "✓ PrismShell.swift written"

  cat > ios/App/App/AppDelegate.swift << 'PRISM_APPDELEGATE_EOF'
import UIKit
import Capacitor

@UIApplicationMain
class AppDelegate: UIResponder, UIApplicationDelegate {

    var window: UIWindow?

    // The storyboard's Capacitor bridge is replaced before it ever loads, so it never starts.
    func application(_ application: UIApplication, didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        window?.rootViewController = PrismTabBarController()
        return true
    }

    func application(_ app: UIApplication, open url: URL, options: [UIApplication.OpenURLOptionsKey: Any] = [:]) -> Bool {
        return ApplicationDelegateProxy.shared.application(app, open: url, options: options)
    }

    func application(_ application: UIApplication, continue userActivity: NSUserActivity, restorationHandler: @escaping ([UIUserActivityRestoring]?) -> Void) -> Bool {
        return ApplicationDelegateProxy.shared.application(application, continue: userActivity, restorationHandler: restorationHandler)
    }

}
PRISM_APPDELEGATE_EOF
  echo "✓ AppDelegate.swift written"

  # The site's apple-app-site-association names this app for its domain, which lets the system
  # sign-in sheet hand the https sign-in callback to this app alone.
  cat > ios/App/App/App.entitlements << 'PRISM_ENTITLEMENTS_EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>com.apple.developer.associated-domains</key>
	<array>
@@PRISM_ASSOCIATED_DOMAINS@@
	</array>
</dict>
</plist>
PRISM_ENTITLEMENTS_EOF
  echo "✓ App.entitlements written"

  cat > .prism-add-swift-file.mjs << 'PRISM_NODE_EOF'
import xcode from 'xcode';
import fs from 'node:fs';

const pbxprojPath = 'ios/App/App.xcodeproj/project.pbxproj';
const project = xcode.project(pbxprojPath);
project.parseSync();

const refs = project.hash.project.objects.PBXFileReference || {};
const hasRef = name => Object.values(refs).some(
  ref => ref && typeof ref === 'object' && typeof ref.path === 'string' && ref.path.includes(name)
);

// Xcode only compiles files registered in the project, and builds cleanly without them.
if (hasRef('PrismBridgeViewController.swift')) {
  project.removeSourceFile('App/PrismBridgeViewController.swift', { target: project.getFirstTarget().uuid }, 'App');
}
if (!hasRef('PrismShell.swift')) {
  project.addSourceFile('App/PrismShell.swift', { target: project.getFirstTarget().uuid }, 'App');
  console.log('✓ PrismShell.swift registered in project.pbxproj');
}

// iOS 17.4 is the first with https callbacks for the system sign-in sheet.
project.updateBuildProperty('IPHONEOS_DEPLOYMENT_TARGET', '17.4');
project.updateBuildProperty('CODE_SIGN_ENTITLEMENTS', 'App/App.entitlements');
fs.writeFileSync(pbxprojPath, project.writeSync());
PRISM_NODE_EOF
  node .prism-add-swift-file.mjs
  rm -f .prism-add-swift-file.mjs
else
  echo "⚠️ ios/App/App not found. Run 'npx cap add ios' first."
fi
