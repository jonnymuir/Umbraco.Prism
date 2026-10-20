import UIKit
import WebKit

/// WKWebView shows nothing between one page and the next: the outgoing page disappears before the
/// incoming one paints. The hold covers that gap with the outgoing page as it last looked, and
/// the loading ring once the wait is long enough to notice, until the new page has loaded.
///
/// The frame is captured a moment after each page settles, and again after the member scrolls or
/// types, never when a navigation starts: WebKit's snapshots taken at that moment come back blank.
/// So the very first page after launch has no frame to hold, only the page colour behind it
/// (see PrismPageColours). It hooks the webview, not the page, so it covers every navigation,
/// including the identity provider's pages during in-app sign-in.
final class PrismNavigationHold {
    private enum Timing {
        /// After a page finishes loading, before it is captured.
        static let settle: TimeInterval = 0.3
        /// After the member scrolls or types, before the page is captured again.
        static let contentChange: TimeInterval = 0.1
        /// How long a navigation runs before the ring appears; quicker ones show only the held frame.
        static let ringDelay: TimeInterval = 0.15
        static let fadeOut: TimeInterval = 0.2
    }

    private weak var webView: WKWebView?
    private let heldFrame = UIImageView()
    private let ring = PrismLoadingRing()
    private var capturedFrame: UIImage?
    private var pendingCapture: DispatchWorkItem?
    private var pendingRing: DispatchWorkItem?
    private var holding = false

    init(webView: WKWebView) {
        self.webView = webView
        heldFrame.contentMode = .topLeft
        heldFrame.clipsToBounds = true
        // Taps during the wait would land on a page that is already leaving.
        heldFrame.isUserInteractionEnabled = true
    }

    func navigationStarted() {
        guard !holding, let webView, let container = webView.superview else { return }
        holding = true
        pendingCapture?.cancel()

        heldFrame.layer.removeAllAnimations()
        heldFrame.alpha = 1
        heldFrame.frame = webView.frame
        heldFrame.image = capturedFrame
        if capturedFrame != nil {
            container.addSubview(heldFrame)
        }

        let reveal = DispatchWorkItem { [weak self] in self?.showRing(over: webView, in: container) }
        pendingRing = reveal
        DispatchQueue.main.asyncAfter(deadline: .now() + Timing.ringDelay, execute: reveal)
    }

    /// The navigation finished, failed or was cancelled. A loaded page is captured once it settles.
    func navigationEnded(loaded: Bool) {
        pendingRing?.cancel()
        if holding {
            holding = false
            UIView.animate(withDuration: Timing.fadeOut, animations: {
                self.heldFrame.alpha = 0
                self.ring.alpha = 0
            }, completion: { _ in
                guard !self.holding else { return }
                self.heldFrame.removeFromSuperview()
                self.ring.stop()
                self.ring.removeFromSuperview()
            })
        }
        if loaded {
            capture(after: Timing.settle)
        }
    }

    func contentChanged() {
        capture(after: Timing.contentChange)
    }

    private func showRing(over webView: WKWebView, in container: UIView) {
        guard holding else { return }
        ring.layer.removeAllAnimations()
        ring.alpha = 0
        ring.center = CGPoint(x: webView.frame.midX, y: webView.frame.midY)
        container.addSubview(ring)
        ring.start(colour: PrismPageColours.accent, over: PrismPageColours.background)
        UIView.animate(withDuration: 0.15) { self.ring.alpha = 1 }
    }

    private func capture(after delay: TimeInterval) {
        pendingCapture?.cancel()
        let capture = DispatchWorkItem { [weak self] in
            guard let self, !self.holding, let webView = self.webView else { return }
            webView.takeSnapshot(with: nil) { image, _ in
                guard let image, !self.holding else { return }
                self.capturedFrame = image
            }
        }
        pendingCapture = capture
        DispatchQueue.main.asyncAfter(deadline: .now() + delay, execute: capture)
    }
}
