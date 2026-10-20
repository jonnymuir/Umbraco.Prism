import UIKit

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
            guard let path = PrismSvgPath.parse(d) else { return nil }
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
}
