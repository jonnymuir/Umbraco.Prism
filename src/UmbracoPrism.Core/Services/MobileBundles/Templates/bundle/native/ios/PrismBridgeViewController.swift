import Capacitor
import UIKit
import WebKit

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
