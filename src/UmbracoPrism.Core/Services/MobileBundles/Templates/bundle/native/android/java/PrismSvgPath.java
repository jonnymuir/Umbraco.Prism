package @@PRISM_PACKAGE@@;

import android.graphics.Path;
import android.graphics.PointF;

/**
 * SVG path data (the d attribute): commands M L H V C S Q T A Z, absolute or relative. Returns null
 * for anything it cannot read, so a tab keeps its placeholder icon rather than drawing nonsense.
 */
final class PrismSvgPath {
    private PrismSvgPath() {}

    static Path parse(String d) {
        Scanner scanner = new Scanner(d);
        Builder builder = new Builder();
        char command = ' ';

        while (scanner.skipSeparators()) {
            Character next = scanner.command();
            if (next != null) {
                command = next;
            } else if (command == ' ') {
                return null;
            } else if (command == 'M' || command == 'm') {
                // Coordinates after a move continue as a line.
                command = command == 'M' ? 'L' : 'l';
            }
            if (!builder.apply(command, scanner)) return null;
        }
        return builder.path;
    }

    /**
     * Draws one command at a time, tracking the current point and the last control point that the
     * smooth curve commands (S and T) reflect.
     */
    private static final class Builder {
        final Path path = new Path();
        private PointF current = new PointF();
        private PointF start = new PointF();
        private PointF lastControl;
        private char previousCommand = ' ';

        boolean apply(char command, Scanner scanner) {
            PointF origin = Character.isLowerCase(command) ? current : new PointF();
            boolean drawn;
            switch (Character.toUpperCase(command)) {
                case 'M': drawn = move(scanner.point(origin)); break;
                case 'L': drawn = line(scanner.point(origin)); break;
                case 'H': drawn = horizontal(scanner.number(), origin); break;
                case 'V': drawn = vertical(scanner.number(), origin); break;
                case 'C': drawn = cubic(scanner.point(origin), scanner.point(origin), scanner.point(origin)); break;
                case 'S': drawn = cubic(reflectedControl("CS"), scanner.point(origin), scanner.point(origin)); break;
                case 'Q': drawn = quadratic(scanner.point(origin), scanner.point(origin)); break;
                case 'T': drawn = quadratic(reflectedControl("QT"), scanner.point(origin)); break;
                case 'A': drawn = arc(scanner.arc(origin)); break;
                case 'Z': drawn = close(); break;
                default: drawn = false;
            }
            previousCommand = command;
            return drawn;
        }

        private boolean move(PointF point) {
            if (point == null) return false;
            path.moveTo(point.x, point.y);
            current = point;
            start = point;
            lastControl = null;
            return true;
        }

        private boolean line(PointF point) {
            if (point == null) return false;
            path.lineTo(point.x, point.y);
            current = point;
            lastControl = null;
            return true;
        }

        private boolean horizontal(Float x, PointF origin) {
            return x != null && line(new PointF(origin.x + x, current.y));
        }

        private boolean vertical(Float y, PointF origin) {
            return y != null && line(new PointF(current.x, origin.y + y));
        }

        private boolean cubic(PointF control1, PointF control2, PointF point) {
            if (control1 == null || control2 == null || point == null) return false;
            path.cubicTo(control1.x, control1.y, control2.x, control2.y, point.x, point.y);
            current = point;
            lastControl = control2;
            return true;
        }

        private boolean quadratic(PointF control, PointF point) {
            if (control == null || point == null) return false;
            path.quadTo(control.x, control.y, point.x, point.y);
            current = point;
            lastControl = control;
            return true;
        }

        private boolean arc(Scanner.Arc arc) {
            if (arc == null) return false;
            PrismSvgArc.add(path, current, arc);
            current = arc.end;
            lastControl = null;
            return true;
        }

        private boolean close() {
            path.close();
            current = start;
            lastControl = null;
            return true;
        }

        /**
         * S and T start from the previous curve's last control point, reflected through the
         * current point, when they follow a curve of their own kind; otherwise from the current point.
         */
        private PointF reflectedControl(String kinds) {
            if (kinds.indexOf(Character.toUpperCase(previousCommand)) < 0 || lastControl == null) return current;
            return new PointF(2 * current.x - lastControl.x, 2 * current.y - lastControl.y);
        }
    }

    /** Reads path data: commands, numbers and arc flags, skipping whitespace and commas. */
    static final class Scanner {
        static final class Arc {
            final double rx, ry, rotation;
            final boolean largeArc, sweep;
            final PointF end;

            Arc(double rx, double ry, double rotation, boolean largeArc, boolean sweep, PointF end) {
                this.rx = rx;
                this.ry = ry;
                this.rotation = rotation;
                this.largeArc = largeArc;
                this.sweep = sweep;
                this.end = end;
            }
        }

        private final String chars;
        private int index;

        Scanner(String chars) {
            this.chars = chars;
        }

        /** Skips whitespace and commas; false at the end of the data. */
        boolean skipSeparators() {
            while (index < chars.length() && (chars.charAt(index) == ',' || Character.isWhitespace(chars.charAt(index)))) index++;
            return index < chars.length();
        }

        Character command() {
            if (!skipSeparators() || "MmLlHhVvCcSsQqTtAaZz".indexOf(chars.charAt(index)) < 0) return null;
            return chars.charAt(index++);
        }

        PointF point(PointF origin) {
            Float x = number();
            Float y = number();
            return x == null || y == null ? null : new PointF(origin.x + x, origin.y + y);
        }

        Arc arc(PointF origin) {
            Float rx = number(), ry = number(), rotation = number();
            Boolean largeArc = flag(), sweep = flag();
            PointF end = point(origin);
            if (rx == null || ry == null || rotation == null || largeArc == null || sweep == null || end == null) return null;
            return new Arc(rx, ry, rotation, largeArc, sweep, end);
        }

        private Boolean flag() {
            if (!skipSeparators() || (chars.charAt(index) != '0' && chars.charAt(index) != '1')) return null;
            return chars.charAt(index++) == '1';
        }

        /** Numbers may run together: "1.5.5" is 1.5 then .5, "2-3" is 2 then -3. */
        Float number() {
            if (!skipSeparators()) return null;
            int begin = index;
            if (chars.charAt(index) == '-' || chars.charAt(index) == '+') index++;
            if (!consumeDigitsAndPoint()) {
                index = begin;
                return null;
            }
            consumeExponent();
            return Float.parseFloat(chars.substring(begin, index));
        }

        /** Digits with at most one decimal point; true when there was at least one digit. */
        private boolean consumeDigitsAndPoint() {
            boolean seenDot = false, seenDigit = false;
            for (; index < chars.length(); index++) {
                char c = chars.charAt(index);
                if (c >= '0' && c <= '9') {
                    seenDigit = true;
                } else if (c == '.' && !seenDot) {
                    seenDot = true;
                } else {
                    break;
                }
            }
            return seenDigit;
        }

        private void consumeExponent() {
            if (index >= chars.length() || (chars.charAt(index) != 'e' && chars.charAt(index) != 'E')) return;
            index++;
            if (index < chars.length() && (chars.charAt(index) == '-' || chars.charAt(index) == '+')) index++;
            while (index < chars.length() && Character.isDigit(chars.charAt(index))) index++;
        }
    }
}
