package @@PRISM_PACKAGE@@;

import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.webkit.WebView;
import java.util.function.IntConsumer;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import org.json.JSONException;
import org.json.JSONObject;
import org.json.JSONTokener;

/**
 * The site's own colours, read from each page as it loads and remembered for the next launch: its
 * page background, shown behind each webview between one page and the next, and its brand colour
 * (--prism-primary), used for the loading ring.
 */
final class PrismPageColours {
    private static final String BACKGROUND_KEY = "prism.pageBackground";
    private static final String ACCENT_KEY = "prism.pageAccent";
    private static final String SCRIPT =
        "(function () {"
        + "var transparent = function (c) { return !c || c === 'transparent' || c === 'rgba(0, 0, 0, 0)'; };"
        + "var root = getComputedStyle(document.documentElement);"
        + "var background = getComputedStyle(document.body).backgroundColor;"
        + "return JSON.stringify({ background: transparent(background) ? root.backgroundColor : background,"
        + " accent: root.getPropertyValue('--prism-primary').trim() });"
        + "})()";
    private static final Pattern RGB = Pattern.compile("rgba?\\((\\d+),\\s*(\\d+),\\s*(\\d+)(?:,\\s*([\\d.]+))?\\)");
    private static final Pattern HEX = Pattern.compile("#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})");

    private PrismPageColours() {}

    /** The last page's background, or white before any page has loaded. */
    static int background(Context context) {
        return preferences(context).getInt(BACKGROUND_KEY, Color.WHITE);
    }

    /** The site's brand colour, or the given default before a page has given one. */
    static int accent(Context context, int fallback) {
        return preferences(context).getInt(ACCENT_KEY, fallback);
    }

    /** Reads the loaded page's colours and remembers them; calls back only when the page has a background. */
    static void read(WebView webView, IntConsumer background) {
        Context context = webView.getContext().getApplicationContext();
        webView.evaluateJavascript(SCRIPT, result -> {
            JSONObject colours = decode(result);
            if (colours == null) return;
            Integer accent = parse(colours.optString("accent"));
            if (accent != null) preferences(context).edit().putInt(ACCENT_KEY, accent).apply();
            Integer page = parse(colours.optString("background"));
            if (page == null) return;
            preferences(context).edit().putInt(BACKGROUND_KEY, page).apply();
            background.accept(page);
        });
    }

    /** evaluateJavascript returns the script's string result JSON-encoded, so it is decoded twice. */
    private static JSONObject decode(String result) {
        try {
            Object value = new JSONTokener(result).nextValue();
            return value instanceof String ? new JSONObject((String) value) : null;
        } catch (JSONException e) {
            return null;
        }
    }

    /** An opaque CSS colour: "rgb(...)", "rgba(..., 1)", "#rgb" or "#rrggbb". */
    private static Integer parse(String css) {
        Matcher hex = HEX.matcher(css.trim());
        if (hex.matches()) {
            String digits = hex.group(1);
            if (digits.length() == 3) digits = digits.replaceAll("(.)", "$1$1");
            return Color.parseColor("#" + digits);
        }
        Matcher rgb = RGB.matcher(css);
        if (!rgb.find() || (rgb.group(4) != null && Float.parseFloat(rgb.group(4)) < 1)) return null;
        return Color.rgb(Integer.parseInt(rgb.group(1)), Integer.parseInt(rgb.group(2)), Integer.parseInt(rgb.group(3)));
    }

    private static SharedPreferences preferences(Context context) {
        return context.getSharedPreferences("prism", Context.MODE_PRIVATE);
    }
}
