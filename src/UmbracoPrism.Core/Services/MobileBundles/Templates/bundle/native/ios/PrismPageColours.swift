import UIKit
import WebKit

/// The site's own colours, read from each page as it loads and remembered for the next launch: its
/// page background, shown behind the webview so the moment between pages is never the system
/// background (black in dark mode), and its brand colour (--prism-primary), used for the loading
/// ring.
enum PrismPageColours {
    private static let backgroundKey = "prism.pageBackground"
    private static let accentKey = "prism.pageAccent"
    private static let script = """
        (function () {
          var transparent = function (c) { return !c || c === 'transparent' || c === 'rgba(0, 0, 0, 0)'; };
          var root = getComputedStyle(document.documentElement);
          var background = getComputedStyle(document.body).backgroundColor;
          return JSON.stringify({
            background: transparent(background) ? root.backgroundColor : background,
            accent: root.getPropertyValue('--prism-primary').trim()
          });
        })()
        """

    /// The last page's background, or white before any page has loaded.
    static var background: UIColor { stored(backgroundKey) ?? .white }

    /// The site's brand colour, or the system accent before a page has given one.
    static var accent: UIColor { stored(accentKey) ?? .tintColor }

    /// Reads the loaded page's colours and remembers them; calls back only when the page has a background.
    static func read(from webView: WKWebView, completion: @escaping (UIColor) -> Void) {
        webView.evaluateJavaScript(script) { result, _ in
            guard let json = (result as? String)?.data(using: .utf8),
                  let colours = try? JSONSerialization.jsonObject(with: json) as? [String: String] else { return }
            if let accent = colours["accent"].flatMap(parse) {
                UserDefaults.standard.set(accent, forKey: accentKey)
            }
            guard let background = colours["background"].flatMap(parse) else { return }
            UserDefaults.standard.set(background, forKey: backgroundKey)
            completion(colour(background))
        }
    }

    private static func stored(_ key: String) -> UIColor? {
        (UserDefaults.standard.array(forKey: key) as? [Double]).flatMap { $0.count == 3 ? colour($0) : nil }
    }

    private static func colour(_ rgb: [Double]) -> UIColor {
        UIColor(red: rgb[0], green: rgb[1], blue: rgb[2], alpha: 1)
    }

    /// An opaque CSS colour, "rgb(...)", "rgba(..., 1)", "#rgb" or "#rrggbb", as red, green and blue from 0 to 1.
    private static func parse(_ css: String) -> [Double]? {
        let value = css.trimmingCharacters(in: .whitespaces)
        if value.hasPrefix("#") {
            return parseHex(String(value.dropFirst()))
        }
        let numbers = value.split(whereSeparator: { !"0123456789.".contains($0) }).compactMap { Double($0) }
        guard numbers.count == 3 || (numbers.count == 4 && numbers[3] == 1) else { return nil }
        return numbers.prefix(3).map { $0 / 255 }
    }

    private static func parseHex(_ hex: String) -> [Double]? {
        let digits = hex.count == 3 ? hex.map { "\($0)\($0)" }.joined() : hex
        guard digits.count == 6, let value = UInt32(digits, radix: 16) else { return nil }
        return [Double((value >> 16) & 0xFF) / 255, Double((value >> 8) & 0xFF) / 255, Double(value & 0xFF) / 255]
    }
}
