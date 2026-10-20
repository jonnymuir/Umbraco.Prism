import UIKit
import WebKit

/// The site's own page background, shown behind each webview. Without it, the moment between one
/// page and the next shows the system background, which is black in dark mode. Each loaded page's
/// colour is remembered, so the next launch starts with it.
enum PrismPageBackground {
    private static let storeKey = "prism.pageBackground"
    private static let script = """
        (function () {
          var transparent = function (c) { return !c || c === 'transparent' || c === 'rgba(0, 0, 0, 0)'; };
          var colour = getComputedStyle(document.body).backgroundColor;
          return transparent(colour) ? getComputedStyle(document.documentElement).backgroundColor : colour;
        })()
        """

    /// The last page's background, or white before any page has loaded.
    static var current: UIColor {
        guard let rgb = UserDefaults.standard.array(forKey: storeKey) as? [Double], rgb.count == 3 else { return .white }
        return UIColor(red: rgb[0], green: rgb[1], blue: rgb[2], alpha: 1)
    }

    /// Reads the loaded page's background and remembers it; nothing when the page has none.
    static func read(from webView: WKWebView, completion: @escaping (UIColor) -> Void) {
        webView.evaluateJavaScript(script) { result, _ in
            guard let rgb = (result as? String).flatMap(parse) else { return }
            UserDefaults.standard.set(rgb, forKey: storeKey)
            completion(UIColor(red: rgb[0], green: rgb[1], blue: rgb[2], alpha: 1))
        }
    }

    /// "rgb(243, 242, 241)" or an opaque "rgba(...)" as red, green and blue from 0 to 1.
    private static func parse(_ css: String) -> [Double]? {
        let numbers = css.split(whereSeparator: { !"0123456789.".contains($0) }).compactMap { Double($0) }
        guard numbers.count >= 3, numbers.count == 3 || numbers[3] == 1 else { return nil }
        return numbers.prefix(3).map { $0 / 255 }
    }
}
