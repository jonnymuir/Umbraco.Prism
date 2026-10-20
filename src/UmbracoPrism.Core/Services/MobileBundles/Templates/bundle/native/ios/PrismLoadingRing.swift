import UIKit

/// The loading indicator: a thin arc turning around a faint track, in the site's brand colour, on
/// a small frosted disc so it stands out on any page. With Reduce Motion on it pulses instead of
/// turning.
final class PrismLoadingRing: UIView {
    private enum Size {
        static let disc: CGFloat = 72
        static let ring: CGFloat = 40
        static let line: CGFloat = 4
    }

    private let disc = UIVisualEffectView(effect: UIBlurEffect(style: .systemThinMaterial))
    private let track = CAShapeLayer()
    private let arc = CAShapeLayer()

    init() {
        super.init(frame: CGRect(x: 0, y: 0, width: Size.disc, height: Size.disc))
        isUserInteractionEnabled = false
        isAccessibilityElement = true
        accessibilityLabel = "Loading"
        accessibilityTraits = .updatesFrequently

        disc.frame = bounds
        disc.layer.cornerRadius = Size.disc / 2
        disc.clipsToBounds = true
        addSubview(disc)

        let inset = (Size.disc - Size.ring) / 2
        let circle = UIBezierPath(ovalIn: bounds.insetBy(dx: inset, dy: inset)).cgPath
        for (layer, end) in [(track, 1.0), (arc, 0.28)] {
            layer.path = circle
            layer.fillColor = nil
            layer.lineWidth = Size.line
            layer.lineCap = .round
            layer.strokeEnd = end
            layer.frame = bounds
            self.layer.addSublayer(layer)
        }
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) is not supported")
    }

    override var intrinsicContentSize: CGSize { CGSize(width: Size.disc, height: Size.disc) }

    /// Starts the ring in the given colour, frosted to suit the page behind it rather than the
    /// phone's own light or dark appearance.
    func start(colour: UIColor, over background: UIColor) {
        disc.effect = UIBlurEffect(style: Self.isLight(background) ? .systemThinMaterialLight : .systemThinMaterialDark)
        arc.strokeColor = colour.cgColor
        track.strokeColor = colour.withAlphaComponent(0.2).cgColor
        arc.removeAllAnimations()
        arc.add(UIAccessibility.isReduceMotionEnabled ? Self.pulse() : Self.spin(), forKey: "loading")
    }

    func stop() {
        arc.removeAllAnimations()
    }

    private static func isLight(_ colour: UIColor) -> Bool {
        var red: CGFloat = 1, green: CGFloat = 1, blue: CGFloat = 1
        colour.getRed(&red, green: &green, blue: &blue, alpha: nil)
        return 0.299 * red + 0.587 * green + 0.114 * blue > 0.5
    }

    private static func spin() -> CAAnimation {
        let spin = CABasicAnimation(keyPath: "transform.rotation.z")
        spin.fromValue = 0
        spin.toValue = 2 * Double.pi
        spin.duration = 0.9
        spin.repeatCount = .infinity
        return spin
    }

    private static func pulse() -> CAAnimation {
        let pulse = CABasicAnimation(keyPath: "opacity")
        pulse.fromValue = 1
        pulse.toValue = 0.35
        pulse.duration = 0.8
        pulse.autoreverses = true
        pulse.repeatCount = .infinity
        return pulse
    }
}
