package @@PRISM_PACKAGE@@;

import android.graphics.Path;
import android.graphics.PointF;

/**
 * SVG elliptical arc, converted from endpoint to centre form (SVG 1.1 appendix F.6.5) and drawn as
 * cubic curves of at most a quarter turn each.
 */
final class PrismSvgArc {
    private PrismSvgArc() {}

    static void add(Path path, PointF p0, PrismSvgPath.Scanner.Arc arc) {
        PointF p1 = arc.end;
        double rx = Math.abs(arc.rx), ry = Math.abs(arc.ry);
        if (rx == 0 || ry == 0 || (p0.x == p1.x && p0.y == p1.y)) {
            path.lineTo(p1.x, p1.y);
            return;
        }
        double phi = Math.toRadians(arc.rotation);
        double cosPhi = Math.cos(phi), sinPhi = Math.sin(phi);
        double dx = (p0.x - p1.x) / 2.0, dy = (p0.y - p1.y) / 2.0;
        double x1 = cosPhi * dx + sinPhi * dy;
        double y1 = -sinPhi * dx + cosPhi * dy;
        double lambda = (x1 * x1) / (rx * rx) + (y1 * y1) / (ry * ry);
        if (lambda > 1) {
            rx *= Math.sqrt(lambda);
            ry *= Math.sqrt(lambda);
        }
        double numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
        double denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
        double coefficient = Math.sqrt(Math.max(0, numerator / denominator)) * (arc.largeArc == arc.sweep ? -1 : 1);
        double cxPrime = coefficient * rx * y1 / ry;
        double cyPrime = -coefficient * ry * x1 / rx;
        double cx = cosPhi * cxPrime - sinPhi * cyPrime + (p0.x + p1.x) / 2.0;
        double cy = sinPhi * cxPrime + cosPhi * cyPrime + (p0.y + p1.y) / 2.0;

        double startAngle = angle(1, 0, (x1 - cxPrime) / rx, (y1 - cyPrime) / ry);
        double delta = angle((x1 - cxPrime) / rx, (y1 - cyPrime) / ry, (-x1 - cxPrime) / rx, (-y1 - cyPrime) / ry);
        if (!arc.sweep && delta > 0) delta -= 2 * Math.PI;
        if (arc.sweep && delta < 0) delta += 2 * Math.PI;

        Ellipse ellipse = new Ellipse(rx, ry, cosPhi, sinPhi, cx, cy);
        int segments = (int) Math.ceil(Math.abs(delta) / (Math.PI / 2));
        double step = delta / segments;
        for (int i = 0; i < segments; i++) {
            ellipse.addQuarter(path, startAngle + i * step, step);
        }
        path.lineTo(p1.x, p1.y);
    }

    private static double angle(double ux, double uy, double vx, double vy) {
        return Math.atan2(ux * vy - uy * vx, ux * vx + uy * vy);
    }

    /** The arc's ellipse: a unit circle scaled, rotated and moved into place. */
    private static final class Ellipse {
        private final double rx, ry, cosPhi, sinPhi, cx, cy;

        Ellipse(double rx, double ry, double cosPhi, double sinPhi, double cx, double cy) {
            this.rx = rx;
            this.ry = ry;
            this.cosPhi = cosPhi;
            this.sinPhi = sinPhi;
            this.cx = cx;
            this.cy = cy;
        }

        /** One cubic curve along the ellipse from theta, through at most a quarter turn. */
        void addQuarter(Path path, double theta, double step) {
            double k = 4.0 / 3.0 * Math.tan(step / 4);
            double cos1 = Math.cos(theta), sin1 = Math.sin(theta);
            double cos2 = Math.cos(theta + step), sin2 = Math.sin(theta + step);
            float[] c1 = map(cos1 - k * sin1, sin1 + k * cos1);
            float[] c2 = map(cos2 + k * sin2, sin2 - k * cos2);
            float[] end = map(cos2, sin2);
            path.cubicTo(c1[0], c1[1], c2[0], c2[1], end[0], end[1]);
        }

        private float[] map(double ux, double uy) {
            double x = ux * rx, y = uy * ry;
            return new float[] {(float) (cosPhi * x - sinPhi * y + cx), (float) (sinPhi * x + cosPhi * y + cy)};
        }
    }
}
