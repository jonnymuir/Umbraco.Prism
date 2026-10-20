import UIKit

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
