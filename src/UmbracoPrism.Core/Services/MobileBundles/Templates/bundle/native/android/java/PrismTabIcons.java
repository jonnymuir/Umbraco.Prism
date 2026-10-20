package @@PRISM_PACKAGE@@;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.Drawable;
import android.os.Handler;
import android.os.Looper;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.function.Consumer;

/**
 * Tab icons: a built-in name maps to a Material icon; an image URL (a media item) is downloaded
 * once and cached. Android cannot decode SVG at runtime, so SVG icons are drawn by PrismSvgIcon.
 */
final class PrismTabIcons {
    private static final Map<String, String> BUILT_IN = new HashMap<>();

    static {
        BUILT_IN.put("home", "M10,20v-6h4v6h5v-8h3L12,3 2,12h3v8z");
        BUILT_IN.put("dashboard", "M3,13h8L11,3L3,3v10zM3,21h8v-6L3,15v6zM13,21h8L21,11h-8v10zM13,3v6h8L21,3h-8z");
        BUILT_IN.put("account", "M12,2C6.48,2 2,6.48 2,12s4.48,10 10,10 10,-4.48 10,-10S17.52,2 12,2zM12,5c1.66,0 3,1.34 3,3s-1.34,3 -3,3 -3,-1.34 -3,-3 1.34,-3 3,-3zM12,19.2c-2.5,0 -4.71,-1.28 -6,-3.22 0.03,-1.99 4,-3.08 6,-3.08 1.99,0 5.97,1.09 6,3.08 -1.29,1.94 -3.5,3.22 -6,3.22z");
        BUILT_IN.put("settings", "M19.14,12.94c0.04,-0.3 0.06,-0.61 0.06,-0.94c0,-0.32 -0.02,-0.64 -0.07,-0.94l2.03,-1.58c0.18,-0.14 0.23,-0.41 0.12,-0.61l-1.92,-3.32c-0.12,-0.22 -0.37,-0.29 -0.59,-0.22l-2.39,0.96c-0.5,-0.38 -1.03,-0.7 -1.62,-0.94L14.4,2.81c-0.04,-0.24 -0.24,-0.41 -0.48,-0.41h-3.84c-0.24,0 -0.43,0.17 -0.47,0.41L9.25,5.35C8.66,5.59 8.12,5.92 7.63,6.29L5.24,5.33c-0.22,-0.08 -0.47,0 -0.59,0.22L2.74,8.87C2.62,9.08 2.66,9.34 2.86,9.48l2.03,1.58C4.84,11.36 4.8,11.69 4.8,12s0.02,0.64 0.07,0.94l-2.03,1.58c-0.18,0.14 -0.23,0.41 -0.12,0.61l1.92,3.32c0.12,0.22 0.37,0.29 0.59,0.22l2.39,-0.96c0.5,0.38 1.03,0.7 1.62,0.94l0.36,2.54c0.05,0.24 0.24,0.41 0.48,0.41h3.84c0.24,0 0.44,-0.17 0.47,-0.41l0.36,-2.54c0.59,-0.24 1.13,-0.56 1.62,-0.94l2.39,0.96c0.22,0.08 0.47,0 0.59,-0.22l1.92,-3.32c0.12,-0.22 0.07,-0.47 -0.12,-0.61L19.14,12.94zM12,15.6c-1.98,0 -3.6,-1.62 -3.6,-3.6s1.62,-3.6 3.6,-3.6s3.6,1.62 3.6,3.6S13.98,15.6 12,15.6z");
        BUILT_IN.put("transactions", "M20,4H4c-1.11,0 -1.99,0.89 -1.99,2L2,18c0,1.11 0.89,2 2,2h16c1.11,0 2,-0.89 2,-2V6c0,-1.11 -0.89,-2 -2,-2zM20,18H4v-6h16v6zM20,8H4V6h16v2z");
        BUILT_IN.put("notifications", "M12,22c1.1,0 2,-0.9 2,-2h-4c0,1.1 0.89,2 2,2zM18,16v-5c0,-3.07 -1.64,-5.64 -4.5,-6.32V4c0,-0.83 -0.67,-1.5 -1.5,-1.5s-1.5,0.67 -1.5,1.5v0.68C7.63,5.36 6,7.92 6,11v5l-2,2v1h16v-1l-2,-2z");
        BUILT_IN.put("more", "M6,10c-1.1,0 -2,0.9 -2,2s0.9,2 2,2 2,-0.9 2,-2 -0.9,-2 -2,-2zM18,10c-1.1,0 -2,0.9 -2,2s0.9,2 2,2 2,-0.9 2,-2 -0.9,-2 -2,-2zM12,10c-1.1,0 -2,0.9 -2,2s0.9,2 2,2 2,-0.9 2,-2 -0.9,-2 -2,-2z");
    }

    private static final String PLACEHOLDER = "M12,2C6.48,2 2,6.48 2,12s4.48,10 10,10 10,-4.48 10,-10S17.52,2 12,2zM12,20c-4.42,0 -8,-3.58 -8,-8s3.58,-8 8,-8 8,3.58 8,8 -3.58,8 -8,8z";
    private static final ExecutorService NETWORK = Executors.newFixedThreadPool(2);

    private final Context context;
    private final int size;
    private final Handler main = new Handler(Looper.getMainLooper());

    PrismTabIcons(Context context) {
        this.context = context.getApplicationContext();
        this.size = Math.round(24 * context.getResources().getDisplayMetrics().density);
    }

    Drawable placeholder(String icon) {
        String path = icon == null ? null : BUILT_IN.get(icon);
        return drawable(PrismSvgIcon.renderPath(path != null ? path : PLACEHOLDER, size));
    }

    void load(String icon, Consumer<Drawable> completion) {
        if (icon == null || BUILT_IN.containsKey(icon)) return;
        NETWORK.execute(() -> {
            Bitmap image = loadBitmap(icon);
            if (image != null) main.post(() -> completion.accept(drawable(image)));
        });
    }

    private Bitmap loadBitmap(String icon) {
        String url = icon.startsWith("http") ? icon : PrismShell.origin() + (icon.startsWith("/") ? "" : "/") + icon;
        File cacheFile = new File(context.getCacheDir(), "prism-tab-" + url.replaceAll("[^A-Za-z0-9]", "_") + ".png");
        Bitmap cached = BitmapFactory.decodeFile(cacheFile.getAbsolutePath());
        if (cached != null) return cached;

        HttpURLConnection connection = null;
        try {
            connection = (HttpURLConnection) new URL(url).openConnection();
            if (connection.getResponseCode() != 200) return null;
            byte[] data;
            try (InputStream in = connection.getInputStream()) {
                data = PrismShell.readAll(in);
            }
            Bitmap decoded = BitmapFactory.decodeByteArray(data, 0, data.length);
            Bitmap image = decoded != null
                ? Bitmap.createScaledBitmap(decoded, size, size, true)
                : PrismSvgIcon.render(new String(data, StandardCharsets.UTF_8), size);
            if (image == null) return null;
            try (FileOutputStream out = new FileOutputStream(cacheFile)) {
                image.compress(Bitmap.CompressFormat.PNG, 100, out);
            }
            return image;
        } catch (Exception e) {
            return null;
        } finally {
            if (connection != null) connection.disconnect();
        }
    }

    private Drawable drawable(Bitmap bitmap) {
        return new BitmapDrawable(context.getResources(), bitmap);
    }
}
