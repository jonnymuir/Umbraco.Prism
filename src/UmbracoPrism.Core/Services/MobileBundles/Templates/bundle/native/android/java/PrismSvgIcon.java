package @@PRISM_PACKAGE@@;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Draws an icon-style SVG: the viewBox and every path's d attribute, filled. That covers the
 * usual single-colour icon sets; anything else returns null and the tab keeps its placeholder.
 */
final class PrismSvgIcon {
    private PrismSvgIcon() {}

    static Bitmap render(String svg, int size) {
        List<String> paths = attributeValues("d", "path", svg);
        if (paths.isEmpty()) return null;
        List<String> viewBoxes = attributeValues("viewBox", "svg", svg);
        String[] parts = (viewBoxes.isEmpty() ? "0 0 24 24" : viewBoxes.get(0)).trim().split("[\\s,]+");
        if (parts.length != 4) return null;
        float[] viewBox = new float[4];
        try {
            for (int i = 0; i < 4; i++) viewBox[i] = Float.parseFloat(parts[i]);
        } catch (NumberFormatException e) {
            return null;
        }
        if (viewBox[2] <= 0 || viewBox[3] <= 0) return null;

        Path combined = new Path();
        for (String d : paths) {
            Path path = PrismSvgPath.parse(d);
            if (path == null) return null;
            combined.addPath(path);
        }
        return draw(combined, viewBox, size);
    }

    /** A single path in a 24 by 24 viewBox, as the built-in icons are. */
    static Bitmap renderPath(String d, int size) {
        Path path = PrismSvgPath.parse(d);
        return draw(path == null ? new Path() : path, new float[] {0, 0, 24, 24}, size);
    }

    private static Bitmap draw(Path path, float[] viewBox, int size) {
        float scale = Math.min(size / viewBox[2], size / viewBox[3]);
        Matrix matrix = new Matrix();
        matrix.setTranslate(-viewBox[0], -viewBox[1]);
        matrix.postScale(scale, scale);
        path.transform(matrix);

        Bitmap bitmap = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888);
        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        paint.setColor(Color.BLACK);
        paint.setStyle(Paint.Style.FILL);
        new Canvas(bitmap).drawPath(path, paint);
        return bitmap;
    }

    private static List<String> attributeValues(String attribute, String element, String svg) {
        Matcher matcher = Pattern.compile("<" + element + "\\b[^>]*?\\s" + attribute + "\\s*=\\s*\"([^\"]*)\"", Pattern.CASE_INSENSITIVE).matcher(svg);
        List<String> values = new ArrayList<>();
        while (matcher.find()) values.add(matcher.group(1));
        return values;
    }
}
