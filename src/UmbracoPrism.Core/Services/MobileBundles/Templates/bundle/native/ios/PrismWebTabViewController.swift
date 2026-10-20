import UIKit
import UserNotifications

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
