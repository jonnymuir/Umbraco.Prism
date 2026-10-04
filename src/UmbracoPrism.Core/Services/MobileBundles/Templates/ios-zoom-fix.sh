
echo "Disabling WebKit's zoom-into-focused-input behaviour on hosted content..."
if [ -d ios/App/App ]; then
  cat > ios/App/App/PrismBridgeViewController.swift << 'PRISM_SWIFT_EOF'
import Capacitor
import WebKit
@@PRISM_DIAGNOSTICS_DECLARATIONS@@
// Force-pins every page's own viewport meta tag to maximum-scale=1 at the WebKit level, so it
// applies even to cross-origin hosted content (forMainFrameOnly: false) that this app has no
// CSS/markup control over — see bootstrap-ios.sh's own comment for the full rationale. Runs at
// document start and again on DOMContentLoaded, so it wins regardless of whether the page's own
// <meta name=viewport> tag exists yet.
class PrismBridgeViewController: CAPBridgeViewController, WKScriptMessageHandler {
    private static let viewportFixDiagnosticMessageName = "prismViewportFixDiag"

    // TEMPORARY — confirms live that moving script injection to capacitorDidLoad() (see its own
    // remarks) actually reaches the real webview, the same way an earlier version of this exact
    // counter proved the previous injection point (webViewConfiguration(for:)) never did. Remove
    // once confirmed.
    fileprivate static var viewportScriptPingCount = 0

    // TEMPORARY — reported live: plugin= (PrismContentWatcherPlugin.callCount) stays at 0 despite
    // interacting with pages that definitely mutate the DOM, even though every step of the
    // registration/JS-export/message-routing path was independently confirmed correct against
    // Capacitor's own vendored source. A completely separate, already-proven-reliable
    // WKScriptMessageHandler channel (the same mechanism vp= uses) reports each stage of
    // prism-mobile-content-watcher.js's own execution directly, without depending on the
    // still-unproven plugin bridge to report progress — the same isolation technique that found
    // the capacitorDidLoad() root cause in the first place. ws= (script executed at all, pinged
    // unconditionally at the top of the file) / wr= (Cap.isNativePlatform()/nativePromise guard
    // passed and the MutationObserver was actually attached) / wm= (the observer fired and a
    // native call was about to be attempted) / we= (that native call's promise rejected) — reading
    // which of these four climbs and which doesn't pinpoints exactly which link is broken, the
    // same way raw=/init=/cfg= did previously. Remove once root-caused.
    private static let contentWatcherDiagnosticMessageName = "prismContentWatcherDiag"
    fileprivate static var contentWatcherScriptStartedPingCount = 0
    fileprivate static var contentWatcherReadyPingCount = 0
    fileprivate static var contentWatcherMutationPingCount = 0
    fileprivate static var contentWatcherErrorPingCount = 0

    private var navigationHold: PrismNavigationHoldDelegate?

    // Root-caused from Capacitor's own vendored iOS source (CAPBridgeViewController.prepareWebView):
    // a webViewConfiguration(for:) override's returned WKWebViewConfiguration.userContentController
    // gets discarded and replaced wholesale with Capacitor's own internal one, one line later,
    // before the real webview is ever built — confirmed live, previously, by a counter proving
    // that override WAS being called while everything added to its content controller (a
    // paint-holding content-change script, and — a real product bug, not just a diagnostic gap —
    // this app's own viewport-zoom-fix for Entra's hosted login page) never actually ran.
    // capacitorDidLoad() is called from inside loadView() — documented: webView/bridge are already
    // set by this point, but no navigation has started yet — and webView.configuration
    // .userContentController here IS the real, live one Capacitor itself keeps (the same one its
    // own plugin bridge JS gets added to), so anything added here actually takes effect.
    override func capacitorDidLoad() {
        super.capacitorDidLoad()
        guard let webView = self.webView else { return }
        let contentController = webView.configuration.userContentController

        // Capacitor's own `zoomEnabled: false` only disables the user's own pinch-zoom gesture —
        // it does not stop WKWebView's own "zoom into a focused text input" behaviour, which fires
        // independently whenever a page's own viewport doesn't cap maximum-scale. Reported live on
        // the Entra/ciamlogin.com sign-in page (hosted content this app doesn't control and can't
        // add page-level CSS/viewport-meta to): the password field triggered a zoomed-in,
        // left-clipped layout. forMainFrameOnly: false specifically because this needs to reach
        // that cross-origin hosted content — a server-rendered <script> tag (the approach used for
        // this app's own pages' paint-holding detection, see PrismContentWatcherPlugin's own
        // remarks) fundamentally cannot reach a page a different server renders; only native
        // injection at the WebView level can.
        //
        // viewport-fit=cover, plus AppDelegate now pinning the webview's own top edge to the
        // screen's true top (not always the safe-area guide) — see AppDelegate's and
        // PrismNavigationHoldDelegate's own remarks — together let this app's own pages fill all
        // the way under the status bar/notch/Dynamic Island (this app's own header is tall enough
        // to clear that area on its own). Without viewport-fit=cover, env(safe-area-inset-top)
        // always resolves to 0 — and this script's own meta-tag overwrite was silently stripping
        // the viewport-fit=cover Master.cshtml already sets, on every page including this app's
        // own, which is why this must be set unconditionally here too.
        //
        // Deliberately NOT also trying to push hosted content's own top-of-page content clear of
        // that strip from here, the way an earlier version of this fix did (a `<style>` rule
        // conditional on window.location.hostname): reported live, that worked for some Entra
        // screens ("stay signed in") but not others (password entry, "pick an account",
        // create-account, username entry) — those are different client-side view states within
        // ONE hosted page, not separate navigations, so a CSS fix injected once at that page's
        // initial load has no way to stay applied across however Microsoft's own hosted UI
        // re-renders itself afterward (a full-viewport position:fixed container used for one view
        // state but not another would silently defeat a plain `html{padding-top}` rule too,
        // regardless of timing). PrismNavigationHoldDelegate's own safe-area-reservation toggle
        // (see its own remarks) has neither problem: it's a property of the whole webview for
        // that page's entire lifetime, completely independent of the page's own DOM/CSS, so it
        // stays correct through however many internal view-state changes a hosted flow goes
        // through — exactly why the original #250 fix (safe-area-pinned unconditionally) was
        // 100% reliable for Entra's entire flow in the first place.
        let viewportFixSource = "(function(){function pin(){var meta=document.querySelector('meta[name=viewport]');if(!meta){meta=document.createElement('meta');meta.name='viewport';document.head.appendChild(meta);}meta.content='width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover';if(\(PrismMobileDiagnosticsFlag.enabled)&&window.webkit&&window.webkit.messageHandlers&&window.webkit.messageHandlers.\(Self.viewportFixDiagnosticMessageName)){window.webkit.messageHandlers.\(Self.viewportFixDiagnosticMessageName).postMessage('viewport-ready');}}if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',pin);}else{pin();}})();"
        let viewportFixScript = WKUserScript(source: viewportFixSource, injectionTime: .atDocumentStart, forMainFrameOnly: false)
        contentController.addUserScript(viewportFixScript)
        if PrismMobileDiagnosticsFlag.enabled {
            // WKUserContentController retains whatever's added as a message handler for as long
            // as it exists — adding `self` directly here would be the textbook WKScriptMessageHandler
            // retain cycle (this view controller owns the webview, which owns this very content
            // controller, which would then own this view controller right back). The
            // weak-referencing proxy below is the standard fix.
            contentController.add(WeakScriptMessageHandler(target: self), name: Self.viewportFixDiagnosticMessageName)
            contentController.add(WeakScriptMessageHandler(target: self), name: Self.contentWatcherDiagnosticMessageName)
        }

        // Capacitor's own canonical JS-to-native bridge, replacing an earlier
        // WKScriptMessageHandler-based attempt for paint-holding's own content-change detection —
        // see PrismContentWatcherPlugin's own remarks. registerPluginInstance (not
        // registerPluginType, which silently no-ops when autoRegisterPlugins is true — confirmed
        // from Capacitor's own vendored source, and true by default here since this app never
        // overrides it) is what actually wires this into Capacitor's real, live content
        // controller via its own JSExport mechanism — the exact mechanism
        // @aparajita/capacitor-biometric-auth's own plugin (already proven working in this app)
        // uses too.
        bridge?.registerPluginInstance(PrismContentWatcherPlugin())

        // See PrismIdentityCookiePlugin's own remarks. Registered unconditionally, same
        // reasoning as PrismContentWatcherPlugin above — this app never overrides
        // autoRegisterPlugins, so registerPluginInstance (not registerPluginType) is what
        // actually wires it in.
        bridge?.registerPluginInstance(PrismIdentityCookiePlugin())
    }

    override func viewDidLoad() {
        super.viewDidLoad()
        guard let webView = self.webView else { return }
        // See PrismNavigationHoldDelegate's own remarks for why this exists and how it avoids
        // reimplementing Capacitor's own navigation handling. Also wraps webView.uiDelegate, not
        // just navigationDelegate — reported live: every decidePolicyFor decision during a sign-out
        // attempt came back "allow", including for the logout POST itself, so whatever's actually
        // bouncing the app to system Safari isn't happening through navigationDelegate at all. A
        // suspicious "allow GET about:blank" in that same log is the fingerprint of a window.open()
        // call, which WebKit routes through a completely different delegate method
        // (createWebViewWith, on WKUIDelegate) that this app was never observing.
        let hold = PrismNavigationHoldDelegate(
            forwardingTo: webView.navigationDelegate,
            forwardingUIDelegateTo: webView.uiDelegate,
            // Reported live: a window.open()-style popup request for this app's own
            // /auth/logout — the exact URL a plain top-level navigation was already handling
            // correctly — got sent straight to system Safari by Capacitor's own createWebViewWith,
            // which (confirmed from its vendored source) has no allowlist logic at all, unlike
            // decidePolicyFor. Reuses the SAME allowlist Capacitor's own regular navigation
            // handling already trusts (bridge.config.shouldAllowNavigation, the exact call
            // decidePolicyFor itself makes) rather than inventing a second one, so a popup
            // targeting this app's own trusted hosts stays in-app regardless of what triggered it.
            isHostTrustedInApp: { [weak self] host in self?.bridge?.config.shouldAllowNavigation(to: host) ?? false }
        )
        navigationHold = hold
        // Read by PrismContentWatcherPlugin — see its own remarks on why a plain direct reference,
        // set here, rather than reached via bridge.viewController.
        PrismContentWatcherPlugin.activeHold = hold
        webView.navigationDelegate = hold
        webView.uiDelegate = hold

        // TEMPORARY (see PrismNavigationHoldDelegate's own remarks) — a no-op unless this specific
        // build was produced with mobile diagnostics enabled. A two-finger long-press anywhere on
        // the page toggles the on-screen paint-holding diagnostic label on/off; otherwise
        // invisible. Reported live: an earlier single-finger version, restricted to near the top
        // of the screen to avoid colliding with normal page interaction, didn't work at all — the
        // real status bar area isn't part of this app's own view hierarchy (it's drawn by iOS
        // itself, and AppDelegate's own safe-area-pinned container deliberately sits below it, see
        // its own remarks), so no gesture recognizer attached to anything in this app can ever see
        // a touch that lands there. Attached directly to webView (not just its superview) so it
        // works anywhere the page actually renders, not a strip that turned out to be unreachable.
        if PrismMobileDiagnosticsFlag.enabled {
            let diagnosticsGesture = UILongPressGestureRecognizer(target: hold, action: #selector(PrismNavigationHoldDelegate.handleDiagnosticsGesture(_:)))
            diagnosticsGesture.numberOfTouchesRequired = 2
            diagnosticsGesture.minimumPressDuration = 1.5
            diagnosticsGesture.delegate = hold
            webView.addGestureRecognizer(diagnosticsGesture)
        }

        // Real product fix, unconditional on diagnostics — reported live: on a slow connection
        // the navigation spinner sits dead-center, but a user's eyes are already on wherever they
        // just tapped, not the screen's center, so a subtle spinner there goes unnoticed. Tracks
        // the most recent tap's location so showSpinner() can appear right where attention already
        // is instead. cancelsTouchesInView = false, plus shouldRecognizeSimultaneouslyWith
        // (already true for every recognizer on this delegate — see its own remarks), mean this
        // only observes taps, it never intercepts or delays them: ordinary page interaction (link
        // taps, button presses, WKWebView's own tap handling) is completely unaffected.
        let tapTracker = UITapGestureRecognizer(target: hold, action: #selector(PrismNavigationHoldDelegate.handleTapForSpinnerPositioning(_:)))
        tapTracker.cancelsTouchesInView = false
        tapTracker.delegate = hold
        webView.addGestureRecognizer(tapTracker)
    }

    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        switch message.name {
        case Self.viewportFixDiagnosticMessageName:
            if message.body as? String == "viewport-ready" {
                Self.viewportScriptPingCount += 1
            }
        case Self.contentWatcherDiagnosticMessageName:
            switch message.body as? String {
            case "script-started": Self.contentWatcherScriptStartedPingCount += 1
            case "ready": Self.contentWatcherReadyPingCount += 1
            case "mutation": Self.contentWatcherMutationPingCount += 1
            case "error": Self.contentWatcherErrorPingCount += 1
            default: break
            }
        default:
            break
        }
    }
}

private final class WeakScriptMessageHandler: NSObject, WKScriptMessageHandler {
    private weak var target: WKScriptMessageHandler?

    init(target: WKScriptMessageHandler) {
        self.target = target
    }

    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        target?.userContentController(userContentController, didReceive: message)
    }
}

// Bridges AppDelegate's own top constraint — this app's own top-of-screen/safe-area choice for
// whatever page is CURRENTLY loaded — to PrismNavigationHoldDelegate, the only thing that knows,
// per navigation, which host that is (see its own remarks on why this exists and how it decides).
// AppDelegate and PrismBridgeViewController.swift are separate files/objects with no other
// connection between them; mirrors PrismContentWatcherPlugin.activeHold's own static
// weak-reference bridging pattern below for exactly the same reason. weak on both: AppDelegate
// owns the constraint/view for the app's entire lifetime, this holds no strong reference to
// either.
enum PrismSafeAreaTopCoordinator {
    static weak var topConstraint: NSLayoutConstraint?
    static weak var containerView: UIView?
}

// Capacitor's own canonical JS-to-native bridge for content Prism itself serves (see
// prism-mobile-content-watcher.js's own remarks for the full history of why this replaced a
// native WKUserScript/WKScriptMessageHandler-based attempt entirely: that mechanism's
// WKWebViewConfiguration.userContentController was confirmed, from Capacitor's own vendored
// source, to be discarded before the real webview is ever built, so nothing added there was ever
// actually live). registerPluginInstance (in PrismBridgeViewController.capacitorDidLoad()) wires
// this into Capacitor's real, live content controller via its own JSExport mechanism — the exact
// mechanism @aparajita/capacitor-biometric-auth's own plugin already uses successfully in this
// app. jsName ("PrismContentWatcher") must exactly match the string prism-mobile-content-watcher.js
// passes to Cap.nativePromise — that's the only key Capacitor's own JS-side plugin-header lookup
// matches on.
@objc(PrismContentWatcherPlugin)
public class PrismContentWatcherPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "PrismContentWatcherPlugin"
    public let jsName = "PrismContentWatcher"
    public let pluginMethods: [CAPPluginMethod] = [
        .init(#selector(contentChanged))
    ]

    // Set by PrismBridgeViewController.viewDidLoad() once PrismNavigationHoldDelegate exists — not
    // reached via bridge.viewController, since whether that resolves to the SAME
    // PrismBridgeViewController instance that registered this plugin isn't something confirmed
    // from Capacitor's own source the way registerPluginInstance itself is; a plain direct
    // reference avoids depending on it. weak since PrismBridgeViewController (not this plugin) owns
    // the delegate's lifetime.
    fileprivate static weak var activeHold: PrismNavigationHoldDelegate?

    // Diagnostic: distinguishes "the JS bridge never reached this method at all" (stays at 0) from
    // "it did, but activeHold was nil" (this climbs, contentChangeSignalCount on the label doesn't)
    // from "everything downstream works" (both climb together) — the same reasoning the mechanism
    // this replaces used its own raw-message counter for.
    fileprivate static var callCount = 0

    @objc func contentChanged(_ call: CAPPluginCall) {
        Self.callCount += 1
        if let webView = self.bridge?.webView {
            Self.activeHold?.contentDidChange(in: webView)
        }
        call.resolve()
    }
}

// Deletes the IdP's own SSO session cookie(s) directly from this app's WebView cookie store —
// prism-biometric-signout.js's own remarks explain why this exists: AccountController.Logout()
// correctly skips the federated sign-out redirect for a biometric-tagged session (no Entra
// session in this WebView for it to end), but that leaves Entra's own cookie from this device's
// original interactive sign-in stale forever, since nothing else ever clears it again. Two
// silent alternatives were already tried and failed for the reason documented there (Entra's
// X-Frame-Options: DENY; WKWebView dropping Set-Cookie on cross-origin fetch under ITP) — both
// ask Entra's own server to do the clearing over a channel that blocks it. This asks Entra
// nothing: WKWebsiteDataStore is this app's own local cookie jar, so neither limitation applies.
// jsName ("PrismIdentityCookiePlugin") must exactly match the string
// prism-biometric-signout.js passes to Cap.nativePromise.
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
            // Suffix match, not just equality — WKWebsiteDataStore records a cookie's owning
            // host exactly as the page that set it (e.g. a specific "{tenant}.ciamlogin.com"),
            // and a generic OIDC authority's own subdomains deserve the same coverage a plain
            // equality check would miss.
            let matching = records.filter { record in
                hosts.contains { host in
                    record.displayName == host || record.displayName.hasSuffix("." + host)
                }
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

// WKWebView, embedded the way Capacitor uses it here, shows a real blank gap between full-page
// navigations (confirmed live; confirmed by reading Capacitor's own vendored iOS source: no
// snapshot/hold mechanism anywhere in it, this is a genuine gap in what it provides, not something
// misconfigured). Two different attempts to paper over that gap with a frozen frame of the
// outgoing page failed live, in production, in the same way — a synchronous UIView snapshot, and
// WKWebView's own async takeSnapshot API, each independently resolved to a blank/black image —
// and both share the same root cause: both were captured at the instant a navigation starts,
// which is exactly the timing WebKit's own documentation and outside reports call out as
// unreliable, because the outgoing page hasn't settled yet. Neither is captured at that moment any
// more. Instead, scheduleSnapshotCapture() takes a fresh snapshot a short delay *after* each
// navigation finishes — the one timing this API is documented to actually work at — and caches it;
// showSpinner() for the *next* navigation just hands over whatever's already cached, synchronously,
// with no async call happening at the moment it's needed and so no timing race left to get wrong
// there. If nothing has been cached yet (the very first navigation after launch), WKWebView's own
// default rendering is left alone rather than covering it with a placeholder that isn't actually a
// frame of anything. Either way, a small spinner is layered on top too, revealed after a short
// delay (100ms) so a slow navigation still gets a visible sign something is happening.
//
// The cached frame is also refreshed while the user stays on a page — contentDidChange(in:) is
// called from PrismBridgeViewController's own WKScriptMessageHandler whenever injected JS detects
// input/change/scroll activity (debounced to one message per 100ms of quiet — see that script's
// own remarks), so a page the user has actually typed into or scrolled since it loaded doesn't
// keep showing the empty/unscrolled frame it had right after settling. It can still be briefly
// behind the very latest keystroke or scroll position — that's an accepted tradeoff of capturing
// only once things go quiet, the same one iOS's own app-switcher snapshots make — but it's no
// longer pinned to "whatever the page looked like the moment it finished loading" for the whole
// time the user stays on it.
//
// Works for every navigation regardless of origin, including the federated redirect chain through
// a hosted IdP (Entra) and back — a page this app doesn't control obviously can't run any JS of
// ours, so a DOM-level fix (a click-triggered spinner, tried first) can only ever cover taps on
// this app's own pages, not that whole chain. This covers all of it, because it hooks the WebView
// itself, not any one page's content.
//
// WKWebView.navigationDelegate is a single slot Capacitor already fills with its own
// WebViewDelegationHandler (real navigation policy/redirect/auth-challenge handling the bridge
// depends on to function) — replacing it outright, or reimplementing everything it does by hand,
// is exactly the kind of blind reimplementation that already broke this app twice this session (on
// the safe-area fix, before landing on the AppDelegate approach actually shipped). So this
// implements only the two methods it needs (didStartProvisionalNavigation/didFinish/didFail) and
// forwards every other WKNavigationDelegate call straight through to Capacitor's own delegate
// unchanged, via the standard Cocoa message-forwarding decorator pattern
// (responds(to:)/forwardingTarget(for:)) — Capacitor's own handling of everything else is
// untouched, not reimplemented.
private final class PrismNavigationHoldDelegate: NSObject, WKNavigationDelegate, WKUIDelegate, UIGestureRecognizerDelegate {
    private let target: WKNavigationDelegate?
    // Not weak — matches `target` above, which is also a plain strong reference to the same kind
    // of Capacitor-owned delegate object. No retain cycle risk: neither this object nor `target`
    // holds any reference back to the webview/view controller that in turn retains `hold`.
    private let uiTarget: WKUIDelegate?
    private let isHostTrustedInApp: (String) -> Bool
    private var spinnerView: UIActivityIndicatorView?
    private var spinnerRevealWorkItem: DispatchWorkItem?
    private var snapshotOverlayView: UIImageView?
    private var lastGoodSnapshot: UIImage?
    // Read by showSpinner() to position the spinner near where the user is actually looking —
    // see viewDidLoad's own remarks on the tap-tracking gesture that sets this, and
    // handleTapForSpinnerPositioning's own remarks on the recency window.
    private var lastTapLocation: (point: CGPoint, at: Date)?
    private var pendingSnapshotCapture: DispatchWorkItem?
    private var isCaptureInFlight = false
    private var captureNeededAfterInFlight = false
    private var pendingCaptureSource = "none"

    // Permanent, but never present at all unless this exact bundle was produced with mobile
    // diagnostics deliberately enabled (PrismMobileDiagnosticsFlag.enabled, a build-time constant
    // — see BuildBootstrapIosScript's own remarks), and never visible even then unless the reveal
    // gesture (see handleDiagnosticsGesture) has been used. Tracks what's actually happening in
    // this pipeline so it can be read directly off a device, where there's no attached debugger/
    // console to check instead. A UILabel, not anything drawn into the web page itself — it can't
    // ever trigger this class's own JS-side input/change/scroll listeners, so no risk of it
    // feeding back into the very thing it's reporting on. Shows only build/version, cache hit/
    // miss counters, and timing — nothing from the page's own content, and nothing a user typed.
    private var diagnosticLabel: UILabel?
    private var diagnosticDismissWorkItem: DispatchWorkItem?
    private var isDiagnosticsRevealed = false
    private var lastCaptureSource = "none"
    private var lastCaptureAt: Date?
    private var contentChangeSignalCount = 0
    private var captureSuccessCount = 0
    private var captureFailureCount = 0

    init(forwardingTo target: WKNavigationDelegate?, forwardingUIDelegateTo uiTarget: WKUIDelegate?, isHostTrustedInApp: @escaping (String) -> Bool) {
        self.target = target
        self.uiTarget = uiTarget
        self.isHostTrustedInApp = isHostTrustedInApp
    }

    override func responds(to aSelector: Selector!) -> Bool {
        if super.responds(to: aSelector) { return true }
        if target?.responds(to: aSelector) ?? false { return true }
        return uiTarget?.responds(to: aSelector) ?? false
    }

    override func forwardingTarget(for aSelector: Selector!) -> Any? {
        if super.responds(to: aSelector) { return nil }
        if target?.responds(to: aSelector) ?? false { return target }
        // Anything neither this class nor `target` implements falls through to `uiTarget` —
        // needed because this is now also installed as webView.uiDelegate (see viewDidLoad's own
        // remarks), and Capacitor's own uiDelegate may implement WKUIDelegate methods beyond
        // createWebViewWith (JS alert/confirm panels, for instance) that this class doesn't
        // reimplement and must not silently drop just by having taken over the delegate slot.
        return uiTarget
    }

    func webView(_ webView: WKWebView, didStartProvisionalNavigation navigation: WKNavigation!) {
        // A capture scheduled for the page now being navigated away from — if it hasn't fired
        // yet, cancel it rather than let it run mid-navigation, which is the exact bad timing
        // this whole scheme exists to avoid. Also drops any catch-up already flagged for once an
        // in-flight capture finishes — that capture started on the *old* page (its result is
        // still valid and kept), but a follow-up triggered by it firing mid-navigation on the
        // *new* page would not be. lastGoodSnapshot just stays one navigation stale in that case;
        // see the class-level remarks on why that's an accepted tradeoff, not a bug.
        pendingSnapshotCapture?.cancel()
        pendingSnapshotCapture = nil
        captureNeededAfterInFlight = false
        // Before showSpinner, not after: showSpinner reads webView.frame for its snapshot
        // overlay, so the destination page's own top-reservation choice needs to already be in
        // effect by the time it does, not applied to a frame that's about to change again.
        updateSafeAreaTopReservation(for: webView.url)
        showSpinner(over: webView)
        target?.webView?(webView, didStartProvisionalNavigation: navigation)
    }

    // A server-side redirect (e.g. Entra's own auth flow hopping from ciamlogin.com to
    // login.microsoftonline.com and back) keeps the SAME provisional-navigation lifecycle —
    // didStartProvisionalNavigation only fires once, for the ORIGINAL request, so without this
    // the reservation below would stay stuck on whatever the very first hop's host needed even
    // after redirecting to a host that needs the opposite treatment. webView.url is already
    // updated to the redirect's target by the time this fires. Capacitor's own delegate
    // (WebViewDelegationHandler, confirmed from its vendored source) doesn't implement this
    // method at all, so there's nothing to preserve by forwarding — done anyway, matching this
    // class's own forward-everything-else discipline, in case that ever changes.
    func webView(_ webView: WKWebView, didReceiveServerRedirectForProvisionalNavigation navigation: WKNavigation!) {
        updateSafeAreaTopReservation(for: webView.url)
        target?.webView?(webView, didReceiveServerRedirectForProvisionalNavigation: navigation)
    }

    // Reported live: some Entra screens (password entry, "pick an account", create-account,
    // username entry) still overlapped the status bar/notch even once the viewport-fix script
    // could reliably fix this app's own pages, while others ("stay signed in") were fine —
    // because those are different client-side view states within ONE hosted page, not separate
    // navigations, so a CSS fix applied once at that page's initial load can't reliably stay
    // applied across however Microsoft's own hosted UI re-renders itself afterward. This
    // sidesteps that entirely: reserving/not-reserving the safe-area strip is a property of the
    // whole webview's own frame for that page's entire lifetime, completely independent of the
    // page's own DOM/CSS, so it stays correct no matter how many internal view-state changes a
    // hosted flow goes through — the same reason the original #250 fix (safe-area-pinned
    // unconditionally, for every page) was 100% reliable for Entra's entire flow in the first
    // place; this only narrows WHEN that reservation applies, from every page to just the ones
    // that actually need it. PrismOwnHost.value, not isHostTrustedInApp — that closure answers a
    // different question ("is this popup destination one we can just navigate to in-app", true
    // for Entra's own hosts too, since they must be navigable at all) from the one asked here
    // ("is this literally this app's own site, whose own layout we already know handles this").
    private func updateSafeAreaTopReservation(for url: URL?) {
        guard let topConstraint = PrismSafeAreaTopCoordinator.topConstraint,
              let containerView = PrismSafeAreaTopCoordinator.containerView else { return }
        let isOwnHost = url?.host == PrismOwnHost.value
        let target: CGFloat = isOwnHost ? 0 : containerView.safeAreaInsets.top
        guard topConstraint.constant != target else { return }
        topConstraint.constant = target
        containerView.layoutIfNeeded()
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        target?.webView?(webView, didFinish: navigation)
        // One extra runloop turn so the new page has actually painted before the spinner is
        // hidden — didFinish fires on load completion, not first paint.
        DispatchQueue.main.async { [weak self] in self?.hideSpinner() }
        scheduleSnapshotCapture(of: webView, delay: 0.3, source: "settle")
    }

    // Called by PrismBridgeViewController's own WKScriptMessageHandler when injected JS detects
    // the page has been typed into, changed, or scrolled — see the class-level remarks. A much
    // shorter delay than the post-didFinish capture: the page is already loaded and settled, this
    // is just refreshing a cached frame to match what's actually on screen now, not waiting out
    // trailing load-time rendering.
    fileprivate func contentDidChange(in webView: WKWebView) {
        contentChangeSignalCount += 1
        scheduleSnapshotCapture(of: webView, delay: 0.1, source: "change")
    }

    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        target?.webView?(webView, didFail: navigation, withError: error)
        hideSpinner()
    }

    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        target?.webView?(webView, didFailProvisionalNavigation: navigation, withError: error)
        hideSpinner()
    }

    // Observes, never alters, Capacitor's own real navigation-policy decision — reported live: a
    // sign-out tap bounces the whole app out to system Safari, landing on this app's own
    // /auth/logout URL, blank. Confirmed from Capacitor's vendored source that its decision here is
    // purely host-allowlist-based with no method/navigationType distinction, and confirmed every
    // Sign Out button submits a plain top-level POST form — so which exact URL, with which method,
    // actually gets cancelled (bounced) is the one thing that can't be settled by reading source,
    // only by watching a real decision happen. Manually forwards to target rather than relying on
    // forwardingTarget(for:) (used for everything else this class doesn't implement) specifically
    // because observing requires wrapping the completion handler, not just relaying the call
    // unchanged — target?.webView?(...) still safely no-ops exactly like forwardingTarget would if
    // target is nil or doesn't implement this, which the guard below detects and falls back to
    // .allow for (WKWebView's own default with no navigationDelegate at all) so a navigation can
    // never hang waiting for a decisionHandler that was never going to fire.
    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        let urlString = navigationAction.request.url?.absoluteString ?? "nil"
        let method = navigationAction.request.httpMethod ?? "GET"
        // Explicit Void? annotation: deliberate here, not an accident this needs silencing —
        // nil is exactly how "target is nil, or doesn't implement this optional method" is
        // distinguished from "it does, and just called our wrapped completion".
        let forwarded: Void? = target?.webView?(webView, decidePolicyFor: navigationAction, decisionHandler: { policy in
            Self.recordNavigationDecision(url: urlString, method: method, decision: policy == .cancel ? "CANCEL" : "allow")
            decisionHandler(policy)
        })
        if forwarded == nil {
            Self.recordNavigationDecision(url: urlString, method: method, decision: "allow")
            decisionHandler(.allow)
        }
    }

    // createWebViewWith (WKUIDelegate) is a completely separate delegate method from
    // decidePolicyFor (WKNavigationDelegate) above — reported live: decidePolicyFor showed nothing
    // but "allow" during a sign-out attempt that still ended up in system Safari, including for
    // the logout POST itself, immediately followed by "allow GET about:blank" and then this
    // method firing for that exact same /auth/logout URL. Something in that chain issues a
    // window.open()-style request for a URL a plain top-level navigation was already handling
    // correctly — Capacitor's own implementation of this method (confirmed from its vendored
    // source) has no allowlist check of any kind, unlike decidePolicyFor: it unconditionally opens
    // the popup's target URL in system Safari regardless of host. Rather than alter that
    // wholesale, this checks the target host against the SAME allowlist Capacitor's own
    // decidePolicyFor already trusts for ordinary navigation (bridge.config.shouldAllowNavigation,
    // via isHostTrustedInApp — see viewDidLoad's own remarks) — a popup targeting one of this
    // app's own trusted hosts loads in the same webview instead, regardless of what triggered it;
    // anything else still goes to Capacitor's own real uiDelegate exactly as before, unchanged.
    func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration, for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
        let urlString = navigationAction.request.url?.absoluteString ?? "nil"
        if let host = navigationAction.request.url?.host, isHostTrustedInApp(host) {
            Self.recordNavigationDecision(url: urlString, method: "WINDOW.OPEN", decision: "IN-APP")
            webView.load(navigationAction.request)
            return nil
        }
        Self.recordNavigationDecision(url: urlString, method: "WINDOW.OPEN", decision: "EXTERNAL")
        return uiTarget?.webView?(webView, createWebViewWith: configuration, for: navigationAction, windowFeatures: windowFeatures) ?? nil
    }

    // TEMPORARY diagnostic aid, same spirit as the paint-holding counters above but for a
    // different bug: sign-out leaves the app entirely, so there's no "next navigation's spinner"
    // moment left in THIS app session to show a label at — UserDefaults, not an in-memory property,
    // specifically so the log survives the round trip through Safari even if the user force-quits
    // while there (reported as part of the same investigation) rather than just backgrounding.
    // Recording is gated on PrismMobileDiagnosticsFlag.enabled (a build-time constant) but NOT on
    // isDiagnosticsRevealed (a display-time toggle) — capturing what happened shouldn't depend on
    // whether anyone happened to have the overlay open at that exact moment; only showing it does.
    private static let navigationDecisionLogKey = "prism.diag.navigationDecisionLog"

    private static func recordNavigationDecision(url: String, method: String, decision: String) {
        guard PrismMobileDiagnosticsFlag.enabled else { return }
        var log = UserDefaults.standard.stringArray(forKey: navigationDecisionLogKey) ?? []
        log.append("\(decision) \(method) \(url)")
        if log.count > 10 {
            log.removeFirst(log.count - 10)
        }
        UserDefaults.standard.set(log, forKey: navigationDecisionLogKey)
    }

    private func showSpinner(over webView: WKWebView) {
        // The webview's own superview (AppDelegate's safe-area-pinned container — see its own
        // remarks), not the webview itself: adding a plain UIView as a WKWebView's own direct
        // subview risks interfering with WKWebView's own internal view hierarchy, which it
        // manages itself. Read fresh here rather than captured once at init — the view hierarchy
        // may not be fully attached yet at viewDidLoad time.
        guard let hostView = webView.superview, spinnerView == nil else { return }

        // A real frame of the outgoing page, if one's on hand — captured proactively, after the
        // previous navigation settled (see scheduleSnapshotCapture()'s own remarks for why that
        // timing, and not this one, is the only one this API is documented to work reliably at).
        // Shown synchronously: no async call happens here, so there's no timing race left to get
        // wrong at the point it matters. With nothing cached yet (the very first navigation after
        // launch), only the spinner below appears — WKWebView's own default rendering is left
        // alone rather than covering it with a placeholder that isn't actually a frame of
        // anything.
        if let snapshot = lastGoodSnapshot {
            let imageView = UIImageView(image: snapshot)
            imageView.contentMode = .top
            imageView.clipsToBounds = true
            imageView.frame = webView.frame
            hostView.addSubview(imageView)
            snapshotOverlayView = imageView
        }

        let spinner = UIActivityIndicatorView(style: .medium)
        spinner.hidesWhenStopped = true
        hostView.addSubview(spinner)
        spinnerView = spinner

        // Reported live: a dead-center spinner is easy to miss on a slow connection, because the
        // user's eyes are already on wherever they just tapped, not the screen's center. Anchored
        // there instead when a recent-enough tap is on record — clamped inward so it's never
        // clipped by the view's own edges — so it lands exactly where attention already is. Falls
        // back to dead-center (the previous behaviour) otherwise: a navigation can also come from
        // a redirect, a JS-driven navigation, or simply an old tap from well before this one
        // started, none of which should place a spinner somewhere the user isn't looking any more.
        var center = CGPoint(x: webView.frame.midX, y: webView.frame.midY)
        if let tap = lastTapLocation, Date().timeIntervalSince(tap.at) < 2.0 {
            center = webView.convert(tap.point, to: hostView)
        }
        let inset = max(spinner.bounds.width, spinner.bounds.height)
        let clampBounds = hostView.bounds.insetBy(dx: inset, dy: inset)
        if clampBounds.width > 0 && clampBounds.height > 0 {
            center.x = min(max(center.x, clampBounds.minX), clampBounds.maxX)
            center.y = min(max(center.y, clampBounds.minY), clampBounds.maxY)
        }
        spinner.center = center

        let reveal = DispatchWorkItem { spinner.startAnimating() }
        spinnerRevealWorkItem = reveal
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.1, execute: reveal)

        showDiagnosticLabel(over: hostView, webView: webView)
    }

    private func hideSpinner() {
        spinnerRevealWorkItem?.cancel()
        spinnerRevealWorkItem = nil
        spinnerView?.removeFromSuperview()
        spinnerView = nil
        snapshotOverlayView?.removeFromSuperview()
        snapshotOverlayView = nil
        // The diagnostic label outlives the spinner/snapshot by a couple of seconds (see its own
        // remarks) rather than disappearing the instant the new page paints — it needs to still be
        // readable after the transition it's describing has already finished.
    }

    // Only ever wired up (see viewDidLoad's own remarks) when PrismMobileDiagnosticsFlag.enabled —
    // a build that doesn't have diagnostics on never creates the gesture recognizer that could
    // call this. Two fingers (numberOfTouchesRequired, set at the call site) plus a 1.5s hold
    // together rule out an accidental trigger from ordinary scrolling/tapping/WebKit's own
    // ~0.5s single-finger long-press-for-selection gesture — deliberately not interfering with
    // that (see viewDidLoad's own remarks on why a different touch signature, not a screen
    // region, is what actually keeps the two apart).
    @objc fileprivate func handleDiagnosticsGesture(_ recognizer: UILongPressGestureRecognizer) {
        guard recognizer.state == .began else { return }
        isDiagnosticsRevealed.toggle()
        UIImpactFeedbackGenerator(style: .light).impactOccurred()
    }

    // Wired up unconditionally in viewDidLoad (not gated on PrismMobileDiagnosticsFlag — this is
    // a real product fix, not a diagnostic aid). Records where showSpinner() should anchor its
    // spinner for the *next* navigation this tap triggers. The recency check happens in
    // showSpinner() itself, not here, since how stale a tap is allowed to be before falling back
    // to dead-center is a property of when it's read, not when it's recorded.
    @objc fileprivate func handleTapForSpinnerPositioning(_ recognizer: UITapGestureRecognizer) {
        guard recognizer.state == .ended, let view = recognizer.view else { return }
        lastTapLocation = (recognizer.location(in: view), Date())
    }

    // Lets this gesture recognize alongside WKWebView's own internal ones (its own single-finger
    // long-press for text selection among them) rather than one silently blocking the other —
    // they target different touch counts already, so this is defence in depth, not the primary
    // fix (that's requiring two fingers at all — see viewDidLoad's own remarks).
    func gestureRecognizer(_ gestureRecognizer: UIGestureRecognizer, shouldRecognizeSimultaneouslyWith otherGestureRecognizer: UIGestureRecognizer) -> Bool {
        true
    }

    // Gated on two independent things: PrismMobileDiagnosticsFlag.enabled (a build-time constant —
    // this bundle either was or wasn't produced with diagnostics on, and if not, nothing below
    // this guard is reachable at all) and isDiagnosticsRevealed (a per-session runtime toggle via
    // the long-press gesture — so a diagnostics-enabled build still shows nothing until someone
    // deliberately asks for it). Reflects exactly what showSpinner() is about to show (or not
    // show) for THIS navigation, not a live-updating log — simplest thing that answers "did this
    // navigation have a cached frame, how did it get there, and how stale is it."
    private func showDiagnosticLabel(over hostView: UIView, webView: WKWebView) {
        guard PrismMobileDiagnosticsFlag.enabled, isDiagnosticsRevealed else { return }

        diagnosticDismissWorkItem?.cancel()
        diagnosticLabel?.removeFromSuperview()

        let ageDescription: String
        if let lastCaptureAt {
            ageDescription = String(format: "%.1fs", Date().timeIntervalSince(lastCaptureAt))
        } else {
            ageDescription = "n/a"
        }
        // build/marketing-version included specifically so a stale-TestFlight-build question
        // never has to be a guess: CFBundleVersion is CURRENT_PROJECT_VERSION at archive time,
        // set to the CI run number in deploy-testflight.yml — a plain, always-unique, always-
        // increasing per-deploy counter to compare against the workflow run that actually shipped
        // whatever's being tested right now.
        let build = Bundle.main.infoDictionary?["CFBundleVersion"] as? String ?? "?"
        let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "?"
        let text = "paint-diag v\(version)(\(build)) snap=\(lastGoodSnapshot != nil ? "yes" : "no") src=\(lastCaptureSource) age=\(ageDescription) vp=\(PrismBridgeViewController.viewportScriptPingCount) plugin=\(PrismContentWatcherPlugin.callCount) ws=\(PrismBridgeViewController.contentWatcherScriptStartedPingCount) wr=\(PrismBridgeViewController.contentWatcherReadyPingCount) wm=\(PrismBridgeViewController.contentWatcherMutationPingCount) we=\(PrismBridgeViewController.contentWatcherErrorPingCount) chg=\(contentChangeSignalCount) ok=\(captureSuccessCount) fail=\(captureFailureCount)"

        // Piggybacks on the same label/reveal gesture rather than a separate view — see
        // recordNavigationDecision's own remarks on why this is captured via UserDefaults
        // regardless of whether anyone had this label open at the time. Shown here (a completely
        // unrelated bug's diagnostics) rather than only right after a sign-out attempt because
        // sign-out leaves the app entirely — this may be the first moment back in it with
        // anywhere left to show a label at all.
        let navLog = UserDefaults.standard.stringArray(forKey: Self.navigationDecisionLogKey) ?? []
        let navLogText = navLog.isEmpty ? "" : "\nnav-log (last \(navLog.count)):\n" + navLog.joined(separator: "\n")

        let label = UILabel()
        label.text = text + navLogText
        label.font = .monospacedSystemFont(ofSize: 10, weight: .regular)
        label.textColor = .white
        label.backgroundColor = UIColor.black.withAlphaComponent(0.6)
        label.numberOfLines = 0
        label.textAlignment = .center
        label.isUserInteractionEnabled = false
        label.translatesAutoresizingMaskIntoConstraints = false
        hostView.addSubview(label)
        diagnosticLabel = label
        NSLayoutConstraint.activate([
            label.leadingAnchor.constraint(equalTo: webView.leadingAnchor),
            label.trailingAnchor.constraint(equalTo: webView.trailingAnchor),
            label.bottomAnchor.constraint(equalTo: webView.bottomAnchor)
        ])

        let dismiss = DispatchWorkItem { [weak label] in label?.removeFromSuperview() }
        diagnosticDismissWorkItem = dismiss
        // Longer when there's a navigation log to read too — that's several lines of URLs, not
        // the usual one-liner, and needs real time to actually read rather than just glimpse.
        let dismissDelay: TimeInterval = navLog.isEmpty ? 2.5 : 8.0
        DispatchQueue.main.asyncAfter(deadline: .now() + dismissDelay, execute: dismiss)
    }

    // 0.3s (didFinish, waiting out trailing load-time rendering) or 0.1s (contentDidChange,
    // waiting out a burst of typing/scrolling) — see each call site's own remarks. Neither is a
    // device-measured constant; worth revisiting with real measurements once this can be tested
    // live. Cancels any capture still pending from a previous trigger first — didFinish and
    // contentDidChange can each fire multiple times in quick succession (a fast navigation, or a
    // user still actively typing), and only the most recent trigger's delay should count, not a
    // pile-up of independently-scheduled timers.
    private func scheduleSnapshotCapture(of webView: WKWebView, delay: TimeInterval, source: String) {
        pendingSnapshotCapture?.cancel()
        pendingCaptureSource = source
        let capture = DispatchWorkItem { [weak self, weak webView] in
            guard let self, let webView else { return }
            self.captureSnapshot(of: webView)
        }
        pendingSnapshotCapture = capture
        DispatchQueue.main.asyncAfter(deadline: .now() + delay, execute: capture)
    }

    // Cancelling the DispatchWorkItem above only stops a capture that hasn't started yet — once
    // takeSnapshot itself has actually been called, that async call is in flight and isn't
    // something a cancelled DispatchWorkItem can stop. isCaptureInFlight guards against a second
    // trigger starting an overlapping takeSnapshot call while one's already running: it's deferred
    // instead (captureNeededAfterInFlight), so a change that arrives mid-capture still eventually
    // gets its own fresh snapshot rather than being silently dropped — just delayed until right
    // after the current one finishes, never running two at once.
    private func captureSnapshot(of webView: WKWebView) {
        guard !isCaptureInFlight else {
            captureNeededAfterInFlight = true
            return
        }
        isCaptureInFlight = true
        // Confirmed reliable at this timing, unlike at navigation start (see the class-level
        // remarks) — but still validated before caching: a nil result (the API can still decline,
        // e.g. mid-memory-pressure) leaves the previous cached frame in place rather than being
        // treated as a valid "blank page" to show next time.
        let source = pendingCaptureSource
        webView.takeSnapshot(with: nil) { [weak self, weak webView] image, _ in
            guard let self else { return }
            if let image {
                self.lastGoodSnapshot = image
                self.lastCaptureSource = source
                self.lastCaptureAt = Date()
                self.captureSuccessCount += 1
            } else {
                self.captureFailureCount += 1
            }
            self.isCaptureInFlight = false
            if self.captureNeededAfterInFlight {
                self.captureNeededAfterInFlight = false
                if let webView {
                    self.captureSnapshot(of: webView)
                }
            }
        }
    }
}
PRISM_SWIFT_EOF
  echo "✓ PrismBridgeViewController.swift written"

  echo "Pinning the app's root view to the screen, safe-area-pinned only at the bottom..."
  cat > ios/App/App/AppDelegate.swift << 'PRISM_APPDELEGATE_EOF'
import UIKit
import Capacitor

@UIApplicationMain
class AppDelegate: UIResponder, UIApplicationDelegate {

    var window: UIWindow?

    func application(_ application: UIApplication, didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        // Pins the storyboard's own root view controller (PrismBridgeViewController — see its
        // own remarks) inside a plain wrapper whose view is safe-area-pinned, via standard view
        // controller containment. Not done inside PrismBridgeViewController itself: Capacitor's
        // own loadView() is `final` and unconditionally does `view = webView` — the view
        // controller's `view` and its `webView` are literally the same object, not a webview
        // nested inside some container Prism could re-constrain. Confirmed live (reading
        // Capacitor's own vendored source, and by two failed attempts guided by the wrong
        // assumption): trying to reconstrain "the webview" from inside that view controller is
        // trying to reconstrain a view relative to itself. Wrapping from the outside — one
        // level up, in the window's own root view controller — sidesteps that entirely and
        // needs nothing Capacitor doesn't already fully support (its bridge view controller
        // works the same as a child VC as it does as the window's direct root).
        //
        // topAnchor now pins to the container's own top, not unconditionally to its safe-area
        // guide (as #250 first shipped, when the viewport couldn't be fixed for hosted content at
        // all) — reclaiming the status bar/notch/Dynamic Island strip for pages whose own layout
        // can use it. Reported live: with the strip reserved for every page regardless, this
        // app's own header rendered detached from the top of the screen with a plain blank gap
        // above it. A mutable constant, not a fixed 0, and handed to
        // PrismSafeAreaTopCoordinator (see its own remarks) rather than left alone here: reported
        // live afterward that a *static* choice made once, for whichever page happens to load
        // first, isn't enough either — some of Entra's own hosted screens still overlapped the
        // status bar while others didn't, because those are different client-side view states
        // within the SAME hosted page, not separate navigations Capacitor's own webViewDidLoad-
        // style hooks could react to. PrismNavigationHoldDelegate is what actually toggles this
        // constant, once per real navigation (including mid-flight redirects) — see its own
        // remarks for why that's reliable where a CSS-only fix for hosted content wasn't. Starts
        // at 0 (full-bleed): the app's very first navigation is always this app's own StartUrl,
        // where that's already known to be correct. bottomAnchor stays safe-area-pinned — only
        // the status-bar-area overlap this was ever about is at the top; nothing here was ever
        // about the home-indicator area at the bottom.
        if let bridgeViewController = window?.rootViewController {
            let container = UIViewController()
            container.view.backgroundColor = .white
            container.addChild(bridgeViewController)
            container.view.addSubview(bridgeViewController.view)
            bridgeViewController.view.translatesAutoresizingMaskIntoConstraints = false
            let topConstraint = bridgeViewController.view.topAnchor.constraint(equalTo: container.view.topAnchor)
            NSLayoutConstraint.activate([
                topConstraint,
                bridgeViewController.view.bottomAnchor.constraint(equalTo: container.view.safeAreaLayoutGuide.bottomAnchor),
                bridgeViewController.view.leadingAnchor.constraint(equalTo: container.view.leadingAnchor),
                bridgeViewController.view.trailingAnchor.constraint(equalTo: container.view.trailingAnchor)
            ])
            PrismSafeAreaTopCoordinator.topConstraint = topConstraint
            PrismSafeAreaTopCoordinator.containerView = container.view
            bridgeViewController.didMove(toParent: container)
            window?.rootViewController = container
        }
        return true
    }

    func applicationWillResignActive(_ application: UIApplication) {
        // Sent when the application is about to move from active to inactive state. This can occur for certain types of temporary interruptions (such as an incoming phone call or SMS message) or when the user quits the application and it begins the transition to the background state.
        // Use this method to pause ongoing tasks, disable timers, and invalidate graphics rendering callbacks. Games should use this method to pause the game.
    }

    func applicationDidEnterBackground(_ application: UIApplication) {
        // Use this method to release shared resources, save user data, invalidate timers, and store enough application state information to restore your application to its current state in case it is terminated later.
        // If your application supports background execution, this method is called instead of applicationWillTerminate: when the user quits.
    }

    func applicationWillEnterForeground(_ application: UIApplication) {
        // Called as part of the transition from the background to the active state; here you can undo many of the changes made on entering the background.
    }

    func applicationDidBecomeActive(_ application: UIApplication) {
        // Restart any tasks that were paused (or not yet started) while the application was inactive. If the application was previously in the background, optionally refresh the user interface.
    }

    func applicationWillTerminate(_ application: UIApplication) {
        // Called when the application is about to terminate. Save data if appropriate. See also applicationDidEnterBackground:.
    }

    func application(_ app: UIApplication, open url: URL, options: [UIApplication.OpenURLOptionsKey: Any] = [:]) -> Bool {
        // Called when the app was launched with a url. Feel free to add additional processing here,
        // but if you want the App API to support tracking app url opens, make sure to keep this call
        return ApplicationDelegateProxy.shared.application(app, open: url, options: options)
    }

    func application(_ application: UIApplication, continue userActivity: NSUserActivity, restorationHandler: @escaping ([UIUserActivityRestoring]?) -> Void) -> Bool {
        // Called when the app was launched with an activity, including Universal Links.
        // Feel free to add additional processing here, but if you want the App API to support
        // tracking app url opens, make sure to keep this call
        return ApplicationDelegateProxy.shared.application(application, continue: userActivity, restorationHandler: restorationHandler)
    }

}
PRISM_APPDELEGATE_EOF
  echo "✓ AppDelegate.swift written"

  STORYBOARD="ios/App/App/Base.lproj/Main.storyboard"
  if [ -f "$STORYBOARD" ]; then
    if grep -q 'customClass="CAPBridgeViewController"' "$STORYBOARD"; then
      sed -i.bak 's/customClass="CAPBridgeViewController" customModule="Capacitor"/customClass="PrismBridgeViewController" customModule="App"/' "$STORYBOARD"
      rm -f "$STORYBOARD.bak"
      echo "✓ Main.storyboard wired to PrismBridgeViewController"
    else
      echo "✓ Main.storyboard already wired to PrismBridgeViewController"
    fi
  else
    echo "⚠️ Main.storyboard not found. Run 'npx cap add ios' first."
  fi

  cat > .prism-add-swift-file.mjs << 'PRISM_NODE_EOF'
import xcode from 'xcode';
import fs from 'node:fs';

const pbxprojPath = 'ios/App/App.xcodeproj/project.pbxproj';
const project = xcode.project(pbxprojPath);
project.parseSync();

const refs = project.hash.project.objects.PBXFileReference || {};
const alreadyPresent = Object.values(refs).some(
  ref => ref && typeof ref === 'object' && typeof ref.path === 'string' && ref.path.includes('PrismBridgeViewController.swift')
);

let pbxprojDirty = false;

if (!alreadyPresent) {
  const target = project.getFirstTarget().uuid;
  project.addSourceFile('App/PrismBridgeViewController.swift', { target }, 'App');
  pbxprojDirty = true;
  console.log('✓ PrismBridgeViewController.swift registered in project.pbxproj');
} else {
  console.log('✓ PrismBridgeViewController.swift already registered in project.pbxproj');
}

// Capacitor's own iOS template defaults IPHONEOS_DEPLOYMENT_TARGET to 14.0. Not urgent today —
// App Store Connect still accepts a 14.0 upload, just flagging it (warning 90068) — but Apple's
// own notice on that warning states 15.0 becomes a hard floor for uploads/submissions starting
// Spring 2027, so there's no reason to keep shipping a value already known to stop working.
// updateBuildProperty with no build/targetName filter applies across every configuration and
// every target, matching how a single Xcode "Deployment Target" field edit would behave.
project.updateBuildProperty('IPHONEOS_DEPLOYMENT_TARGET', '15.0');
pbxprojDirty = true;
console.log('✓ IPHONEOS_DEPLOYMENT_TARGET set to 15.0 in project.pbxproj');

if (pbxprojDirty) {
  fs.writeFileSync(pbxprojPath, project.writeSync());
}
PRISM_NODE_EOF
  node .prism-add-swift-file.mjs
  rm -f .prism-add-swift-file.mjs
else
  echo "⚠️ ios/App/App not found. Run 'npx cap add ios' first."
fi
