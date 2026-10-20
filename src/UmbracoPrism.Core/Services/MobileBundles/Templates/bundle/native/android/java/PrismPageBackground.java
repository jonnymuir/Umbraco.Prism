package @@PRISM_PACKAGE@@;

import android.content.Context;
import android.graphics.Color;
import android.webkit.WebView;
import java.util.function.IntConsumer;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * The site's own page background, shown behind each webview between one page and the next. Each
 * loaded page's colour is remembered, so the next launch starts with it.
 */
final class PrismPageBackground {
    private static final String STORE_KEY = "prism.pageBackground";
    private static final String SCRIPT =
        "(function () {"
        + "var transparent = function (c) { return !c || c === 'transparent' || c === 'rgba(0, 0, 0, 0)'; };"
        + "var colour = getComputedStyle(document.body).backgroundColor;"
        + "return transparent(colour) ? getComputedStyle(document.documentElement).backgroundColor : colour;"
        + "})()";
    private static final Pattern RGB = Pattern.compile("rgba?\\((\\d+),\\s*(\\d+),\\s*(\\d+)(?:,\\s*([\\d.]+))?\\)");

    private PrismPageBackground() {}

    /** The last page's background, or white before any page has loaded. */
    static int current(Context context) {
        return context.getSharedPreferences("prism", Context.MODE_PRIVATE).getInt(STORE_KEY, Color.WHITE);
    }

    /** Reads the loaded page's background and remembers it; nothing when the page has none. */
    static void read(WebView webView, IntConsumer completion) {
        Context context = webView.getContext().getApplicationContext();
        webView.evaluateJavascript(SCRIPT, result -> {
            Integer colour = parse(result);
            if (colour == null) return;
            context.getSharedPreferences("prism", Context.MODE_PRIVATE).edit().putInt(STORE_KEY, colour).apply();
            completion.accept(colour);
        });
    }

    /** "rgb(243, 242, 241)" or an opaque "rgba(...)", as evaluateJavascript returns it (JSON quoted). */
    private static Integer parse(String result) {
        Matcher match = result == null ? null : RGB.matcher(result);
        if (match == null || !match.find()) return null;
        if (match.group(4) != null && Float.parseFloat(match.group(4)) < 1) return null;
        return Color.rgb(Integer.parseInt(match.group(1)), Integer.parseInt(match.group(2)), Integer.parseInt(match.group(3)));
    }
}
