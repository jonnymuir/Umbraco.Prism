import UIKit
import UserNotifications
import WebKit

/// One tab: a Capacitor bridge pinned below the status bar only while it shows another site's
/// page (a hosted sign-in page cannot be trusted to keep clear of the status bar the way this
/// app's own pages do).
final class PrismWebTabViewController: UIViewController {
    var prismTab: PrismManifest.Tab
    private var bridgeController: PrismBridgeViewController?
    private var hold: PrismNavigationHold?
    private var topConstraint: NSLayoutConstraint?
    private var isStale = false
    private var returnsToRootWhenShown = false
    private var firstPage: URL?

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
        view.backgroundColor = PrismPageColours.background
        let bridge = PrismBridgeViewController(startURL: firstPage ?? PrismShell.url(forPath: prismTab.path))
        bridge.events = PrismPageEvents(
            addressChanged: { [weak self] url in self?.reserveStatusBar(!PrismShell.isOwnSite(url)) },
            navigationStarted: { [weak self] in self?.hold?.navigationStarted() },
            loaded: { [weak self] webView in self?.pageLoaded(in: webView) },
            navigationFailed: { [weak self] in self?.hold?.navigationEnded(loaded: false) },
            contentChanged: { [weak self] in self?.hold?.contentChanged() },
            openInAnotherTab: { [weak self] url, followsLink in self?.openInAnotherTab(url, followsLink: followsLink) ?? false })
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
        bridge.webView.map(Self.paint)
        hold = bridge.webView.map(PrismNavigationHold.init)
    }

    private func pageLoaded(in webView: WKWebView) {
        hold?.navigationEnded(loaded: true)
        PrismPageColours.read(from: webView) { [weak self] colour in
            self?.view.backgroundColor = colour
            Self.paint(webView, colour)
        }
    }

    private static func paint(_ webView: WKWebView) {
        paint(webView, PrismPageColours.background)
    }

    private static func paint(_ webView: WKWebView, _ colour: UIColor) {
        webView.backgroundColor = colour
        webView.scrollView.backgroundColor = colour
    }

    /// Another tab's first page opens in that tab. When the member did not simply tap a link (a
    /// redirect after signing in or out, a form, a script), this tab's own page is out of date, so
    /// it goes back to its first page the next time it is shown.
    private func openInAnotherTab(_ url: URL, followsLink: Bool) -> Bool {
        guard let tabBar = tabBarController as? PrismTabBarController, tabBar.selectTab(showing: url, from: self) else { return false }
        if !followsLink {
            returnsToRootWhenShown = true
        }
        return true
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

    /// Brings the tab up to date as it is shown: back to its first page if it was left on the
    /// sign-in page, or reloaded if the member signed in or out elsewhere.
    func refreshWhenShown() {
        guard isViewLoaded else { return }
        if returnsToRootWhenShown {
            returnsToRootWhenShown = false
            isStale = false
            returnToRoot()
        } else if isStale {
            isStale = false
            bridgeController?.webView?.reload()
        }
    }

    /// Opens a page of the site in this tab, such as the one a tapped journey status points to.
    func open(_ url: URL) {
        let page = PrismShell.url(forPath: url.path + (url.query.map { "?" + $0 } ?? ""))
        returnsToRootWhenShown = false
        isStale = false
        if let webView = bridgeController?.webView {
            webView.load(URLRequest(url: page))
        } else {
            firstPage = page
        }
    }

    func returnToRoot() {
        bridgeController?.webView?.load(URLRequest(url: PrismShell.url(forPath: prismTab.path)))
    }
}
