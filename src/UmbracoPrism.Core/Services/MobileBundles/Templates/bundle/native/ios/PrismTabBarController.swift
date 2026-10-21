import UIKit
import UserNotifications
import WebKit

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
        let cookieStore = WKWebsiteDataStore.default().httpCookieStore
        cookieStore.add(self)
        // Whether the member starts signed in, so the first sign-in or sign-out is a change.
        cookiesDidChange(in: cookieStore)
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
        (viewController as? PrismWebTabViewController)?.refreshWhenShown()
        routeNotificationsToSelectedTab()
    }

    /// Switches to the tab whose first page this is, if it is not the tab asking. True when it did.
    func selectTab(showing url: URL, from source: PrismWebTabViewController) -> Bool {
        let path = PrismShell.normalizedPath(url.path)
        guard let target = (viewControllers ?? []).compactMap({ $0 as? PrismWebTabViewController })
            .first(where: { PrismShell.normalizedPath($0.prismTab.path) == path }), target !== source else { return false }
        PrismShell.log.info("Opening \(path, privacy: .public) in its own tab")
        selectedViewController = target
        tabBarController(self, didSelect: target)
        return true
    }

    /// Opens a page of the site, in the tab whose first page it is or else in the tab showing.
    func open(_ url: URL) {
        let tabs = (viewControllers ?? []).compactMap { $0 as? PrismWebTabViewController }
        let path = PrismShell.normalizedPath(url.path)
        guard let target = tabs.first(where: { PrismShell.normalizedPath($0.prismTab.path) == path })
            ?? selectedViewController as? PrismWebTabViewController else { return }
        PrismShell.log.info("Opening \(path, privacy: .public)")
        if target !== selectedViewController {
            selectedViewController = target
            tabBarController(self, didSelect: target)
        }
        target.open(url)
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
                PrismShell.log.info("Member signed \(signedIn ? "in" : "out", privacy: .public); other tabs reload when next shown")
                NotificationCenter.default.post(name: PrismShell.sessionChanged, object: nil, userInfo: [PrismShell.signedInKey: signedIn])
                for case let tab as PrismWebTabViewController in self.viewControllers ?? [] where tab !== self.selectedViewController {
                    tab.markStale()
                }
                self.refreshManifest()
            }
        }
    }
}
