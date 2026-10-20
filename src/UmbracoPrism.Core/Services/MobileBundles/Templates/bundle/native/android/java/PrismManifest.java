package @@PRISM_PACKAGE@@;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.webkit.CookieManager;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Objects;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.function.Consumer;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

/** The tabs and session cookie the site describes, with the last copy kept so the app starts with the right tabs even offline. */
final class PrismManifest {
    static final class Tab {
        final String label;
        final String path;
        final String icon;

        Tab(String label, String path, String icon) {
            this.label = label;
            this.path = path;
            this.icon = icon;
        }

        @Override
        public boolean equals(Object other) {
            if (!(other instanceof Tab)) return false;
            Tab tab = (Tab) other;
            return label.equals(tab.label) && path.equals(tab.path) && Objects.equals(icon, tab.icon);
        }

        @Override
        public int hashCode() {
            return Objects.hash(label, path, icon);
        }
    }

    private static final String CACHE_KEY = "prism.manifest";
    private static final ExecutorService NETWORK = Executors.newSingleThreadExecutor();

    final List<Tab> tabs;
    final String sessionCookie;

    private PrismManifest(List<Tab> tabs, String sessionCookie) {
        this.tabs = tabs;
        this.sessionCookie = sessionCookie;
    }

    static PrismManifest fallback() {
        return new PrismManifest(Collections.singletonList(new Tab("Home", "/", "home")), null);
    }

    static PrismManifest parse(String json) throws JSONException {
        JSONObject root = new JSONObject(json);
        JSONArray items = root.getJSONArray("tabs");
        List<Tab> tabs = new ArrayList<>();
        for (int i = 0; i < items.length(); i++) {
            JSONObject item = items.getJSONObject(i);
            tabs.add(new Tab(item.getString("label"), item.getString("path"), item.isNull("icon") ? null : item.optString("icon", null)));
        }
        if (tabs.isEmpty()) throw new JSONException("The manifest has no tabs.");
        return new PrismManifest(tabs, root.isNull("sessionCookie") ? null : root.optString("sessionCookie", null));
    }

    static PrismManifest cached(Context context) {
        String json = preferences(context).getString(CACHE_KEY, null);
        if (json == null) return fallback();
        try {
            return parse(json);
        } catch (JSONException e) {
            return fallback();
        }
    }

    /** Fetches the manifest as the signed-in member (the webviews' cookies), since the tabs a member can use depend on who they are. */
    static void refresh(Context context, Consumer<PrismManifest> completion) {
        Context app = context.getApplicationContext();
        Handler main = new Handler(Looper.getMainLooper());
        NETWORK.execute(() -> {
            PrismManifest manifest = null;
            HttpURLConnection connection = null;
            try {
                connection = (HttpURLConnection) new URL(PrismShell.origin() + PrismShell.MANIFEST_PATH).openConnection();
                connection.setUseCaches(false);
                connection.setConnectTimeout(10000);
                connection.setReadTimeout(10000);
                String cookies = CookieManager.getInstance().getCookie(PrismShell.origin());
                if (cookies != null) connection.setRequestProperty("Cookie", cookies);
                int status = connection.getResponseCode();
                if (status == 200) {
                    try (InputStream in = connection.getInputStream()) {
                        String json = new String(PrismShell.readAll(in), StandardCharsets.UTF_8);
                        manifest = parse(json);
                        preferences(app).edit().putString(CACHE_KEY, json).apply();
                    }
                    Log.i(PrismShell.LOG_TAG, "Manifest refreshed: " + manifest.tabs.size() + " tabs");
                } else {
                    Log.w(PrismShell.LOG_TAG, "Manifest not refreshed (status " + status + "); keeping the cached tabs");
                }
            } catch (Exception e) {
                Log.w(PrismShell.LOG_TAG, "Manifest not refreshed; keeping the cached tabs", e);
            } finally {
                if (connection != null) connection.disconnect();
            }
            PrismManifest result = manifest;
            main.post(() -> completion.accept(result));
        });
    }

    String sessionCookieName() {
        return sessionCookie == null || sessionCookie.isEmpty() ? "PrismMemberCookie" : sessionCookie;
    }

    @Override
    public boolean equals(Object other) {
        if (!(other instanceof PrismManifest)) return false;
        PrismManifest manifest = (PrismManifest) other;
        return tabs.equals(manifest.tabs) && Objects.equals(sessionCookie, manifest.sessionCookie);
    }

    @Override
    public int hashCode() {
        return Objects.hash(tabs, sessionCookie);
    }

    private static SharedPreferences preferences(Context context) {
        return context.getSharedPreferences("prism", Context.MODE_PRIVATE);
    }
}
