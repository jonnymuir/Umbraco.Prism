import UIKit

/// SVG path data (the d attribute): commands M L H V C S Q T A Z, absolute or relative. Returns nil
/// for anything it cannot read, so a tab keeps its placeholder icon rather than drawing nonsense.
enum PrismSvgPath {
    static func parse(_ d: String) -> UIBezierPath? {
        var scanner = PathScanner(Array(d.unicodeScalars))
        var builder = PathBuilder()
        var command: Character = " "

        while scanner.skipSeparators() {
            if let next = scanner.command() {
                command = next
            } else if let repeated = repeatedCommand(after: command) {
                command = repeated
            } else {
                return nil
            }
            guard builder.apply(command, &scanner) else { return nil }
        }
        return builder.path
    }

    /// Coordinates after a command repeat it, except after a move, which continues as a line.
    private static func repeatedCommand(after command: Character) -> Character? {
        switch command {
        case " ": return nil
        case "M": return "L"
        case "m": return "l"
        default: return command
        }
    }
}

/// Draws one command at a time, tracking the current point and the last control point that the
/// smooth curve commands (S and T) reflect.
private struct PathBuilder {
    let path = UIBezierPath()
    private var current = CGPoint.zero
    private var start = CGPoint.zero
    private var lastControl: CGPoint?
    private var previousCommand: Character = " "

    mutating func apply(_ command: Character, _ scanner: inout PathScanner) -> Bool {
        let origin = command.isLowercase ? current : .zero
        let drawn: Bool
        switch command.uppercased().first! {
        case "M": drawn = move(to: scanner.point(origin))
        case "L": drawn = line(to: scanner.point(origin))
        case "H": drawn = line(to: scanner.number().map { CGPoint(x: origin.x + $0, y: current.y) })
        case "V": drawn = line(to: scanner.number().map { CGPoint(x: current.x, y: origin.y + $0) })
        case "C": drawn = cubic(scanner.point(origin), scanner.point(origin), scanner.point(origin))
        case "S": drawn = cubic(reflectedControl(after: "CS"), scanner.point(origin), scanner.point(origin))
        case "Q": drawn = quadratic(scanner.point(origin), scanner.point(origin))
        case "T": drawn = quadratic(reflectedControl(after: "QT"), scanner.point(origin))
        case "A": drawn = arc(scanner.arc(origin))
        case "Z": drawn = close()
        default: drawn = false
        }
        previousCommand = command
        return drawn
    }

    private mutating func move(to point: CGPoint?) -> Bool {
        guard let point else { return false }
        path.move(to: point)
        current = point
        start = point
        lastControl = nil
        return true
    }

    private mutating func line(to point: CGPoint?) -> Bool {
        guard let point else { return false }
        path.addLine(to: point)
        current = point
        lastControl = nil
        return true
    }

    private mutating func cubic(_ control1: CGPoint?, _ control2: CGPoint?, _ point: CGPoint?) -> Bool {
        guard let control1, let control2, let point else { return false }
        path.addCurve(to: point, controlPoint1: control1, controlPoint2: control2)
        current = point
        lastControl = control2
        return true
    }

    private mutating func quadratic(_ control: CGPoint?, _ point: CGPoint?) -> Bool {
        guard let control, let point else { return false }
        path.addQuadCurve(to: point, controlPoint: control)
        current = point
        lastControl = control
        return true
    }

    private mutating func arc(_ arc: PathScanner.Arc?) -> Bool {
        guard let arc else { return false }
        PrismSvgArc.add(to: path, from: current, arc)
        current = arc.end
        lastControl = nil
        return true
    }

    private mutating func close() -> Bool {
        path.close()
        current = start
        lastControl = nil
        return true
    }

    /// S and T start from the previous curve's last control point, reflected through the current
    /// point, when they follow a curve of their own kind; otherwise from the current point.
    private func reflectedControl(after kinds: String) -> CGPoint {
        guard kinds.contains(previousCommand.uppercased()), let lastControl else { return current }
        return CGPoint(x: 2 * current.x - lastControl.x, y: 2 * current.y - lastControl.y)
    }
}

/// SVG elliptical arc, converted from endpoint to centre form (SVG 1.1 appendix F.6.5) and drawn as
/// a transformed unit-circle arc.
private enum PrismSvgArc {
    static func add(to path: UIBezierPath, from p0: CGPoint, _ arc: PathScanner.Arc) {
        let p1 = arc.end
        guard arc.rx != 0, arc.ry != 0, p0 != p1 else {
            path.addLine(to: p1)
            return
        }
        let phi = arc.rotation * .pi / 180
        let dx = (p0.x - p1.x) / 2, dy = (p0.y - p1.y) / 2
        let x1 = cos(phi) * dx + sin(phi) * dy
        let y1 = -sin(phi) * dx + cos(phi) * dy
        var rx = abs(arc.rx), ry = abs(arc.ry)
        let lambda = (x1 * x1) / (rx * rx) + (y1 * y1) / (ry * ry)
        if lambda > 1 { rx *= sqrt(lambda); ry *= sqrt(lambda) }

        let numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1
        let denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1
        let coefficient = sqrt(max(0, numerator / denominator)) * (arc.largeArc == arc.sweep ? -1 : 1)
        let cxPrime = coefficient * rx * y1 / ry
        let cyPrime = -coefficient * ry * x1 / rx
        let cx = cos(phi) * cxPrime - sin(phi) * cyPrime + (p0.x + p1.x) / 2
        let cy = sin(phi) * cxPrime + cos(phi) * cyPrime + (p0.y + p1.y) / 2

        let startAngle = angle(1, 0, (x1 - cxPrime) / rx, (y1 - cyPrime) / ry)
        var delta = angle((x1 - cxPrime) / rx, (y1 - cyPrime) / ry, (-x1 - cxPrime) / rx, (-y1 - cyPrime) / ry)
        if !arc.sweep && delta > 0 { delta -= 2 * .pi }
        if arc.sweep && delta < 0 { delta += 2 * .pi }

        let unitArc = UIBezierPath(arcCenter: .zero, radius: 1, startAngle: startAngle, endAngle: startAngle + delta, clockwise: delta > 0)
        unitArc.apply(CGAffineTransform(scaleX: rx, y: ry)
            .concatenating(CGAffineTransform(rotationAngle: phi))
            .concatenating(CGAffineTransform(translationX: cx, y: cy)))
        append(unitArc, to: path)
        path.addLine(to: p1)
    }

    private static func angle(_ ux: Double, _ uy: Double, _ vx: Double, _ vy: Double) -> Double {
        atan2(ux * vy - uy * vx, ux * vx + uy * vy)
    }

    /// Continues the path along another path's segments, without starting a new subpath.
    private static func append(_ segments: UIBezierPath, to path: UIBezierPath) {
        segments.cgPath.applyWithBlock { element in
            let points = element.pointee.points
            switch element.pointee.type {
            case .addLineToPoint: path.addLine(to: points[0])
            case .addQuadCurveToPoint: path.addQuadCurve(to: points[1], controlPoint: points[0])
            case .addCurveToPoint: path.addCurve(to: points[2], controlPoint1: points[0], controlPoint2: points[1])
            default: break
            }
        }
    }
}

/// Reads path data: commands, numbers and arc flags, skipping whitespace and commas.
private struct PathScanner {
    struct Arc {
        let rx: Double, ry: Double, rotation: Double
        let largeArc: Bool, sweep: Bool
        let end: CGPoint
    }

    private let chars: [Unicode.Scalar]
    private var index = 0

    init(_ chars: [Unicode.Scalar]) {
        self.chars = chars
    }

    /// Skips whitespace and commas; false at the end of the data.
    mutating func skipSeparators() -> Bool {
        while index < chars.count, chars[index] == "," || chars[index].properties.isWhitespace { index += 1 }
        return index < chars.count
    }

    mutating func command() -> Character? {
        guard skipSeparators(), "MmLlHhVvCcSsQqTtAaZz".unicodeScalars.contains(chars[index]) else { return nil }
        defer { index += 1 }
        return Character(chars[index])
    }

    mutating func point(_ origin: CGPoint) -> CGPoint? {
        guard let x = number(), let y = number() else { return nil }
        return CGPoint(x: origin.x + x, y: origin.y + y)
    }

    mutating func arc(_ origin: CGPoint) -> Arc? {
        guard let rx = number(), let ry = number(), let rotation = number(),
              let largeArc = flag(), let sweep = flag(), let end = point(origin) else { return nil }
        return Arc(rx: rx, ry: ry, rotation: rotation, largeArc: largeArc, sweep: sweep, end: end)
    }

    private mutating func flag() -> Bool? {
        guard skipSeparators(), chars[index] == "0" || chars[index] == "1" else { return nil }
        defer { index += 1 }
        return chars[index] == "1"
    }

    /// Numbers may run together: "1.5.5" is 1.5 then .5, "2-3" is 2 then -3.
    mutating func number() -> Double? {
        guard skipSeparators() else { return nil }
        let begin = index
        if chars[index] == "-" || chars[index] == "+" { index += 1 }
        let digits = consumeDigitsAndPoint()
        guard digits else {
            index = begin
            return nil
        }
        consumeExponent()
        return Double(String(String.UnicodeScalarView(chars[begin..<index])))
    }

    /// Digits with at most one decimal point; true when there was at least one digit.
    private mutating func consumeDigitsAndPoint() -> Bool {
        var seenDot = false, seenDigit = false
        while index < chars.count {
            if ("0"..."9").contains(chars[index]) {
                seenDigit = true
            } else if chars[index] == "." && !seenDot {
                seenDot = true
            } else {
                break
            }
            index += 1
        }
        return seenDigit
    }

    private mutating func consumeExponent() {
        guard index < chars.count, chars[index] == "e" || chars[index] == "E" else { return }
        index += 1
        if index < chars.count, chars[index] == "-" || chars[index] == "+" { index += 1 }
        while index < chars.count, ("0"..."9").contains(chars[index]) { index += 1 }
    }
}
