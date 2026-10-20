
echo "Writing the native tab shell..."
JAVA_DIR="android/app/src/main/java/@@PRISM_JAVA_DIR@@"
if [ -d "$JAVA_DIR" ]; then
  cat > "$JAVA_DIR/PrismShell.java" << 'PRISM_JAVA_EOF'
package @@PRISM_PACKAGE@@;

import android.content.Context;
import android.net.Uri;
import com.getcapacitor.CapConfig;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.Objects;
import org.json.JSONException;
import org.json.JSONObject;

/** The tenant's site, and the Capacitor configuration each tab's bridge opens it with. */
final class PrismShell {
    static final Uri START_URL = Uri.parse("@@PRISM_START_URL@@");
    static final String MANIFEST_PATH = "/umbraco/prism/mobile/manifest";

    private PrismShell() {}

    static String origin() {
        return START_URL.getScheme() + "://" + START_URL.getEncodedAuthority();
    }

    /** Same scheme, host and port as the tenant's site. Locally the identity provider can share the host name on another port. */
    static boolean isOwnSite(Uri url) {
        return url != null
            && Objects.equals(url.getScheme(), START_URL.getScheme())
            && Objects.equals(url.getHost(), START_URL.getHost())
            && url.getPort() == START_URL.getPort();
    }

    /** The tenant's page at a site-relative path, flagged as an app request. */
    static String urlForPath(String path) {
        Uri relative = Uri.parse(path == null || path.isEmpty() ? "/" : path);
        String encodedPath = relative.getEncodedPath();
        Uri.Builder builder = Uri.parse(origin()).buildUpon()
            .encodedPath(encodedPath == null || encodedPath.isEmpty() ? "/" : encodedPath);
        for (String name : relative.getQueryParameterNames()) {
            if (name.equals("prismMobile")) continue;
            for (String value : relative.getQueryParameters(name)) {
                builder.appendQueryParameter(name, value);
            }
        }
        return builder.appendQueryParameter("prismMobile", "1").build().toString();
    }

    /** The app's own Capacitor configuration, opening at another page of the site. */
    static CapConfig configFor(Context context, String url) {
        try {
            JSONObject json = new JSONObject(readAsset(context, "capacitor.config.json"));
            JSONObject server = json.optJSONObject("server");
            if (server == null) {
                server = new JSONObject();
                json.put("server", server);
            }
            server.put("url", url);

            File dir = new File(context.getFilesDir(), "prism-tabs/" + Integer.toHexString(url.hashCode()));
            if (!dir.isDirectory() && !dir.mkdirs()) {
                return CapConfig.loadDefault(context);
            }
            try (FileOutputStream out = new FileOutputStream(new File(dir, "capacitor.config.json"))) {
                out.write(json.toString().getBytes(StandardCharsets.UTF_8));
            }
            return CapConfig.loadFromFile(context, dir.getAbsolutePath() + "/");
        } catch (IOException | JSONException e) {
            return CapConfig.loadDefault(context);
        }
    }

    static byte[] readAll(InputStream in) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = in.read(buffer)) != -1) {
            out.write(buffer, 0, read);
        }
        return out.toByteArray();
    }

    private static String readAsset(Context context, String name) throws IOException {
        try (InputStream in = context.getAssets().open(name)) {
            return new String(readAll(in), StandardCharsets.UTF_8);
        }
    }
}
PRISM_JAVA_EOF

  cat > "$JAVA_DIR/PrismManifest.java" << 'PRISM_JAVA_EOF'
package @@PRISM_PACKAGE@@;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Handler;
import android.os.Looper;
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
                if (connection.getResponseCode() == 200) {
                    try (InputStream in = connection.getInputStream()) {
                        String json = new String(PrismShell.readAll(in), StandardCharsets.UTF_8);
                        manifest = parse(json);
                        preferences(app).edit().putString(CACHE_KEY, json).apply();
                    }
                }
            } catch (Exception ignored) {
                // Offline or unreadable: the cached tabs stay.
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
PRISM_JAVA_EOF

  cat > "$JAVA_DIR/MainActivity.java" << 'PRISM_JAVA_EOF'
package @@PRISM_PACKAGE@@;

import android.content.Context;
import android.content.Intent;
import android.content.res.Configuration;
import android.graphics.Color;
import android.os.Bundle;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.CookieManager;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import androidx.activity.OnBackPressedCallback;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.ContextThemeWrapper;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.fragment.app.FragmentTransaction;
import com.google.android.material.bottomnavigation.BottomNavigationView;
import com.google.android.material.color.MaterialColors;
import com.google.android.material.navigation.NavigationBarView;
import java.util.ArrayList;
import java.util.List;

/**
 * The native tab bar. Each tab is its own Capacitor bridge and webview, so switching tabs never
 * reloads a page and keeps each tab's place.
 *
 * Every bridge is created at launch, one per possible tab, and a tab's page loads the first time
 * it is shown. Capacitor clears the session cookies whenever a bridge starts or stops, which at
 * launch is harmless but later would sign the member out; so a change of tabs re-points an
 * existing bridge rather than creating or destroying one.
 */
public class MainActivity extends AppCompatActivity {
    private static final int MAX_TABS = 5;

    private final List<PrismTabFragment> slots = new ArrayList<>();
    private final List<PrismManifest.Tab> shown = new ArrayList<>();
    private final List<PrismTabFragment> slotForTab = new ArrayList<>();
    private PrismManifest manifest;
    private BottomNavigationView nav;
    private PrismTabFragment selected;
    private Boolean hasSession;
    private PrismTabIcons icons;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        setTheme(R.style.AppTheme_NoActionBar);
        // The tabs come from the manifest, so they are rebuilt rather than restored.
        super.onCreate(null);
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        icons = new PrismTabIcons(this);

        Context themed = new ContextThemeWrapper(this, com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(MaterialColors.getColor(themed, com.google.android.material.R.attr.colorSurface, Color.WHITE));

        FrameLayout container = new FrameLayout(this);
        int containerId = View.generateViewId();
        container.setId(containerId);
        root.addView(container, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));

        nav = new BottomNavigationView(themed);
        nav.setLabelVisibilityMode(NavigationBarView.LABEL_VISIBILITY_LABELED);
        root.addView(nav, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        setContentView(root);

        // The system bars show the app's own surface behind them, as on Android 15 and later.
        getWindow().setStatusBarColor(Color.TRANSPARENT);
        getWindow().setNavigationBarColor(Color.TRANSPARENT);
        boolean night = (getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK) == Configuration.UI_MODE_NIGHT_YES;
        WindowCompat.getInsetsController(getWindow(), root).setAppearanceLightStatusBars(!night);

        // Pages sit below the status bar; while the keyboard is up it takes the tab bar's place.
        ViewCompat.setOnApplyWindowInsetsListener(root, (view, insets) -> {
            Insets bars = insets.getInsets(WindowInsetsCompat.Type.systemBars() | WindowInsetsCompat.Type.displayCutout());
            boolean keyboard = insets.isVisible(WindowInsetsCompat.Type.ime());
            int bottom = keyboard ? insets.getInsets(WindowInsetsCompat.Type.ime()).bottom : 0;
            view.setPadding(bars.left, bars.top, bars.right, bottom);
            nav.setVisibility(keyboard ? View.GONE : View.VISIBLE);
            return insets;
        });

        manifest = PrismManifest.cached(this);
        List<PrismManifest.Tab> tabs = firstTabs(manifest);
        FragmentTransaction transaction = getSupportFragmentManager().beginTransaction();
        for (int i = 0; i < MAX_TABS; i++) {
            PrismTabFragment slot = PrismTabFragment.create(i < tabs.size() ? tabs.get(i).path : "/", i == 0);
            slots.add(slot);
            transaction.add(containerId, slot);
            if (i != 0) transaction.hide(slot);
        }
        transaction.commitNow();
        selected = slots.get(0);
        // A notification tap that launched the app goes to the first tab.
        selected.onNewIntent(getIntent());

        nav.setOnItemSelectedListener(item -> {
            select(slotForTab.get(item.getItemId() - 1));
            return true;
        });
        nav.setOnItemReselectedListener(item -> selected.returnToRoot());

        getOnBackPressedDispatcher().addCallback(this, new OnBackPressedCallback(true) {
            @Override
            public void handleOnBackPressed() {
                if (selected.goBack()) return;
                if (!slotForTab.isEmpty() && selected != slotForTab.get(0)) {
                    nav.setSelectedItemId(1);
                    return;
                }
                moveTaskToBack(true);
            }
        });

        showTabs(tabs);
        refreshManifest();
    }

    private static List<PrismManifest.Tab> firstTabs(PrismManifest manifest) {
        return new ArrayList<>(manifest.tabs.subList(0, Math.min(MAX_TABS, manifest.tabs.size())));
    }

    /** Points a bridge at each tab, keeping the bridge that already shows a tab's page. */
    private void showTabs(List<PrismManifest.Tab> tabs) {
        List<PrismTabFragment> free = new ArrayList<>(slots);
        PrismTabFragment[] assigned = new PrismTabFragment[tabs.size()];
        for (int i = 0; i < tabs.size(); i++) {
            for (PrismTabFragment slot : free) {
                if (slot.path().equals(tabs.get(i).path)) {
                    assigned[i] = slot;
                    free.remove(slot);
                    break;
                }
            }
        }
        for (int i = 0; i < tabs.size(); i++) {
            if (assigned[i] == null) {
                assigned[i] = free.remove(0);
                assigned[i].assign(tabs.get(i).path);
            }
        }

        shown.clear();
        shown.addAll(tabs);
        slotForTab.clear();
        for (PrismTabFragment slot : assigned) slotForTab.add(slot);

        Menu menu = nav.getMenu();
        menu.clear();
        for (int i = 0; i < tabs.size(); i++) {
            PrismManifest.Tab tab = tabs.get(i);
            int itemId = i + 1;
            menu.add(Menu.NONE, itemId, i, tab.label).setIcon(icons.placeholder(tab.icon));
            icons.load(tab.icon, drawable -> {
                MenuItem item = menu.findItem(itemId);
                if (item != null && itemId <= shown.size() && shown.get(itemId - 1).equals(tab)) item.setIcon(drawable);
            });
        }

        int index = slotForTab.indexOf(selected);
        if (index < 0) index = 0;
        menu.findItem(index + 1).setChecked(true);
        select(slotForTab.get(index));
    }

    private void select(PrismTabFragment slot) {
        if (slot != selected) {
            getSupportFragmentManager().beginTransaction().hide(selected).show(slot).commitNow();
            selected = slot;
        }
        slot.showPage();
    }

    private void refreshManifest() {
        PrismManifest.refresh(this, fresh -> {
            if (fresh == null || fresh.equals(manifest)) return;
            manifest = fresh;
            List<PrismManifest.Tab> tabs = firstTabs(fresh);
            if (!tabs.equals(shown)) showTabs(tabs);
        });
    }

    /**
     * A sign-in or sign-out in one tab leaves the others showing the old state, so they reload
     * when next shown, and the tabs themselves are fetched again for the new member.
     */
    void onTabPageLoaded() {
        boolean signedIn = hasCookie(CookieManager.getInstance().getCookie(PrismShell.origin()), manifest.sessionCookieName());
        Boolean previous = hasSession;
        hasSession = signedIn;
        if (previous == null || previous == signedIn) return;
        for (PrismTabFragment slot : slots) {
            if (slot != selected) slot.markStale();
        }
        refreshManifest();
    }

    private static boolean hasCookie(String cookies, String namePrefix) {
        if (cookies == null) return false;
        for (String cookie : cookies.split(";")) {
            String[] parts = cookie.trim().split("=", 2);
            if (parts[0].startsWith(namePrefix) && parts.length == 2 && !parts[1].isEmpty()) return true;
        }
        return false;
    }

    // Notification taps reach the tab that is showing.
    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        selected.onNewIntent(intent);
    }
}
PRISM_JAVA_EOF

  cat > "$JAVA_DIR/PrismTabFragment.java" << 'PRISM_JAVA_EOF'
package @@PRISM_PACKAGE@@;

import android.content.Intent;
import android.content.res.Configuration;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.WebView;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;
import com.getcapacitor.Bridge;
import com.getcapacitor.Logger;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginLoadException;
import com.getcapacitor.PluginManager;
import com.getcapacitor.WebViewListener;
import java.util.ArrayList;
import java.util.List;

/**
 * One tab: a Capacitor bridge showing a page of the site. The bridge is built in onCreate, before
 * the fragment's view exists, because plugins register for activity results as the bridge starts
 * and a fragment only allows that while it is being created. So the view is made here too, and
 * handed back from getView() and onCreateView().
 */
public class PrismTabFragment extends Fragment {
    private static final String ARG_PATH = "path";
    private static final String ARG_LOAD = "load";

    private View root;
    private Bridge bridge;
    private String path;
    private boolean loaded;
    private boolean stale;

    static PrismTabFragment create(String path, boolean load) {
        PrismTabFragment fragment = new PrismTabFragment();
        Bundle args = new Bundle();
        args.putString(ARG_PATH, path);
        args.putBoolean(ARG_LOAD, load);
        fragment.setArguments(args);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        path = requireArguments().getString(ARG_PATH, "/");
        root = LayoutInflater.from(requireContext()).inflate(com.getcapacitor.android.R.layout.capacitor_bridge_layout_main, null, false);

        List<Class<? extends Plugin>> plugins = new ArrayList<>();
        try {
            plugins.addAll(new PluginManager(requireContext().getAssets()).loadPluginClasses());
        } catch (PluginLoadException e) {
            Logger.error("Error loading plugins.", e);
        }
        plugins.add(PrismIdentityCookiePlugin.class);

        bridge = new Bridge.Builder(this)
            .setConfig(PrismShell.configFor(requireContext(), PrismShell.urlForPath(path)))
            .addPlugins(plugins)
            .addWebViewListener(new WebViewListener() {
                @Override
                public void onPageLoaded(WebView webView) {
                    if (getActivity() instanceof MainActivity) ((MainActivity) getActivity()).onTabPageLoaded();
                }
            })
            .create();

        // A tab not yet shown waits to load its page until it is.
        loaded = requireArguments().getBoolean(ARG_LOAD);
        if (!loaded) bridge.getWebView().stopLoading();
    }

    @Nullable
    @Override
    public View getView() {
        return root;
    }

    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        if (root.getParent() instanceof ViewGroup) ((ViewGroup) root.getParent()).removeView(root);
        return root;
    }

    String path() {
        return path;
    }

    /** Shows another tab's page, from the next time this tab is shown. */
    void assign(String newPath) {
        path = newPath;
        loaded = false;
        stale = false;
    }

    /** Loads the tab's page the first time it is shown, and reloads it if it has gone stale. */
    void showPage() {
        if (!loaded) {
            loaded = true;
            stale = false;
            bridge.getWebView().loadUrl(PrismShell.urlForPath(path));
        } else if (stale) {
            stale = false;
            bridge.getWebView().reload();
        }
    }

    void markStale() {
        stale = true;
    }

    void returnToRoot() {
        bridge.getWebView().loadUrl(PrismShell.urlForPath(path));
    }

    boolean goBack() {
        WebView webView = bridge.getWebView();
        if (!webView.canGoBack()) return false;
        webView.goBack();
        return true;
    }

    void onNewIntent(Intent intent) {
        if (intent != null) bridge.onNewIntent(intent);
    }

    @Override
    public void onConfigurationChanged(@NonNull Configuration configuration) {
        super.onConfigurationChanged(configuration);
        bridge.onConfigurationChanged(configuration);
    }

    @Override
    public void onStart() {
        super.onStart();
        bridge.onStart();
    }

    @Override
    public void onResume() {
        super.onResume();
        bridge.getApp().fireStatusChange(true);
        bridge.onResume();
    }

    @Override
    public void onPause() {
        super.onPause();
        bridge.onPause();
    }

    @Override
    public void onStop() {
        super.onStop();
        bridge.getApp().fireStatusChange(false);
        bridge.onStop();
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        bridge.onDestroy();
    }
}
PRISM_JAVA_EOF

  cat > "$JAVA_DIR/PrismTabIcons.java" << 'PRISM_JAVA_EOF'
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
PRISM_JAVA_EOF

  cat > "$JAVA_DIR/PrismSvgIcon.java" << 'PRISM_JAVA_EOF'
package @@PRISM_PACKAGE@@;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PointF;
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
            Path path = parsePath(d);
            if (path == null) return null;
            combined.addPath(path);
        }
        return draw(combined, viewBox, size);
    }

    /** A single path in a 24 by 24 viewBox, as the built-in icons are. */
    static Bitmap renderPath(String d, int size) {
        Path path = parsePath(d);
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

    // SVG path data: commands M L H V C S Q T A Z, absolute or relative.
    static Path parsePath(String d) {
        Scanner scanner = new Scanner(d);
        Path path = new Path();
        PointF current = new PointF(), start = new PointF();
        PointF lastControl = null;
        char command = ' ', previousCommand = ' ';

        while (scanner.skipSeparators()) {
            Character next = scanner.command();
            if (next != null) {
                command = next;
            } else if (command == 'M') {
                command = 'L';
            } else if (command == 'm') {
                command = 'l';
            } else if (command == ' ') {
                return null;
            }
            boolean relative = Character.isLowerCase(command);
            float ox = relative ? current.x : 0, oy = relative ? current.y : 0;

            switch (Character.toUpperCase(command)) {
                case 'M': {
                    PointF p = scanner.point(ox, oy);
                    if (p == null) return null;
                    path.moveTo(p.x, p.y);
                    current = p;
                    start = p;
                    lastControl = null;
                    break;
                }
                case 'L': {
                    PointF p = scanner.point(ox, oy);
                    if (p == null) return null;
                    path.lineTo(p.x, p.y);
                    current = p;
                    lastControl = null;
                    break;
                }
                case 'H': {
                    Float x = scanner.number();
                    if (x == null) return null;
                    current = new PointF((relative ? current.x : 0) + x, current.y);
                    path.lineTo(current.x, current.y);
                    lastControl = null;
                    break;
                }
                case 'V': {
                    Float y = scanner.number();
                    if (y == null) return null;
                    current = new PointF(current.x, (relative ? current.y : 0) + y);
                    path.lineTo(current.x, current.y);
                    lastControl = null;
                    break;
                }
                case 'C': {
                    PointF c1 = scanner.point(ox, oy), c2 = scanner.point(ox, oy), p = scanner.point(ox, oy);
                    if (c1 == null || c2 == null || p == null) return null;
                    path.cubicTo(c1.x, c1.y, c2.x, c2.y, p.x, p.y);
                    current = p;
                    lastControl = c2;
                    break;
                }
                case 'S': {
                    PointF c2 = scanner.point(ox, oy), p = scanner.point(ox, oy);
                    if (c2 == null || p == null) return null;
                    PointF c1 = "CScs".indexOf(previousCommand) >= 0 ? reflect(lastControl, current) : current;
                    path.cubicTo(c1.x, c1.y, c2.x, c2.y, p.x, p.y);
                    current = p;
                    lastControl = c2;
                    break;
                }
                case 'Q': {
                    PointF c = scanner.point(ox, oy), p = scanner.point(ox, oy);
                    if (c == null || p == null) return null;
                    path.quadTo(c.x, c.y, p.x, p.y);
                    current = p;
                    lastControl = c;
                    break;
                }
                case 'T': {
                    PointF p = scanner.point(ox, oy);
                    if (p == null) return null;
                    PointF c = "QTqt".indexOf(previousCommand) >= 0 ? reflect(lastControl, current) : current;
                    path.quadTo(c.x, c.y, p.x, p.y);
                    current = p;
                    lastControl = c;
                    break;
                }
                case 'A': {
                    Float rx = scanner.number(), ry = scanner.number(), rotation = scanner.number();
                    Boolean largeArc = scanner.flag(), sweep = scanner.flag();
                    PointF p = scanner.point(ox, oy);
                    if (rx == null || ry == null || rotation == null || largeArc == null || sweep == null || p == null) return null;
                    addArc(path, current, p, rx, ry, rotation, largeArc, sweep);
                    current = p;
                    lastControl = null;
                    break;
                }
                case 'Z':
                    path.close();
                    current = start;
                    lastControl = null;
                    break;
                default:
                    return null;
            }
            previousCommand = command;
        }
        return path;
    }

    private static PointF reflect(PointF control, PointF point) {
        return control == null ? point : new PointF(2 * point.x - control.x, 2 * point.y - control.y);
    }

    // SVG elliptical arc, converted from endpoint to centre form (SVG 1.1 appendix F.6.5) and
    // drawn as cubic curves of at most a quarter turn each.
    private static void addArc(Path path, PointF p0, PointF p1, double rxIn, double ryIn, double rotation, boolean largeArc, boolean sweep) {
        double rx = Math.abs(rxIn), ry = Math.abs(ryIn);
        if (rx == 0 || ry == 0 || (p0.x == p1.x && p0.y == p1.y)) {
            path.lineTo(p1.x, p1.y);
            return;
        }
        double phi = Math.toRadians(rotation);
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
        double coefficient = Math.sqrt(Math.max(0, numerator / denominator));
        if (largeArc == sweep) coefficient = -coefficient;
        double cxPrime = coefficient * rx * y1 / ry;
        double cyPrime = -coefficient * ry * x1 / rx;
        double cx = cosPhi * cxPrime - sinPhi * cyPrime + (p0.x + p1.x) / 2.0;
        double cy = sinPhi * cxPrime + cosPhi * cyPrime + (p0.y + p1.y) / 2.0;

        double startAngle = angle(1, 0, (x1 - cxPrime) / rx, (y1 - cyPrime) / ry);
        double delta = angle((x1 - cxPrime) / rx, (y1 - cyPrime) / ry, (-x1 - cxPrime) / rx, (-y1 - cyPrime) / ry);
        if (!sweep && delta > 0) delta -= 2 * Math.PI;
        if (sweep && delta < 0) delta += 2 * Math.PI;

        int segments = (int) Math.ceil(Math.abs(delta) / (Math.PI / 2));
        double step = delta / segments;
        double k = 4.0 / 3.0 * Math.tan(step / 4);
        double theta = startAngle;
        for (int i = 0; i < segments; i++) {
            double cos1 = Math.cos(theta), sin1 = Math.sin(theta);
            double cos2 = Math.cos(theta + step), sin2 = Math.sin(theta + step);
            float[] c1 = map(cos1 - k * sin1, sin1 + k * cos1, rx, ry, cosPhi, sinPhi, cx, cy);
            float[] c2 = map(cos2 + k * sin2, sin2 - k * cos2, rx, ry, cosPhi, sinPhi, cx, cy);
            float[] end = map(cos2, sin2, rx, ry, cosPhi, sinPhi, cx, cy);
            path.cubicTo(c1[0], c1[1], c2[0], c2[1], end[0], end[1]);
            theta += step;
        }
        path.lineTo(p1.x, p1.y);
    }

    private static float[] map(double ux, double uy, double rx, double ry, double cosPhi, double sinPhi, double cx, double cy) {
        double x = ux * rx, y = uy * ry;
        return new float[] {(float) (cosPhi * x - sinPhi * y + cx), (float) (sinPhi * x + cosPhi * y + cy)};
    }

    private static double angle(double ux, double uy, double vx, double vy) {
        return Math.atan2(ux * vy - uy * vx, ux * vx + uy * vy);
    }

    private static final class Scanner {
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

        Boolean flag() {
            if (!skipSeparators() || (chars.charAt(index) != '0' && chars.charAt(index) != '1')) return null;
            return chars.charAt(index++) == '1';
        }

        PointF point(float ox, float oy) {
            Float x = number(), y = number();
            return x == null || y == null ? null : new PointF(ox + x, oy + y);
        }

        // Numbers may run together: "1.5.5" is 1.5 then .5, "2-3" is 2 then -3.
        Float number() {
            if (!skipSeparators()) return null;
            int begin = index;
            if (chars.charAt(index) == '-' || chars.charAt(index) == '+') index++;
            boolean seenDot = false, seenDigit = false;
            while (index < chars.length()) {
                char c = chars.charAt(index);
                if (c >= '0' && c <= '9') {
                    seenDigit = true;
                    index++;
                } else if (c == '.' && !seenDot) {
                    seenDot = true;
                    index++;
                } else if ((c == 'e' || c == 'E') && seenDigit) {
                    index++;
                    if (index < chars.length() && (chars.charAt(index) == '-' || chars.charAt(index) == '+')) index++;
                } else {
                    break;
                }
            }
            if (!seenDigit) {
                index = begin;
                return null;
            }
            return Float.parseFloat(chars.substring(begin, index));
        }
    }
}
PRISM_JAVA_EOF

  cat > "$JAVA_DIR/PrismIdentityCookiePlugin.java" << 'PRISM_JAVA_EOF'
package @@PRISM_PACKAGE@@;

import android.webkit.CookieManager;
import com.getcapacitor.JSArray;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

/**
 * Clears the identity provider's session cookies on sign-out (see prism-biometric-signout.js).
 * CookieManager cannot remove one host's cookies, so it clears them all. With no hosts given,
 * because the provider's pages never load in the app, it clears nothing.
 */
@CapacitorPlugin(name = "PrismIdentityCookiePlugin")
public class PrismIdentityCookiePlugin extends Plugin {
    @PluginMethod
    public void clearCookies(PluginCall call) {
        JSArray hosts = call.getArray("hosts");
        if (hosts == null || hosts.length() == 0) {
            call.resolve();
            return;
        }
        CookieManager cookieManager = CookieManager.getInstance();
        cookieManager.removeAllCookies(value -> {
            cookieManager.flush();
            call.resolve();
        });
    }
}
PRISM_JAVA_EOF
  echo "✓ Native tab shell written"

  # Debug builds also trust certificates the device user installed, such as a local development
  # certificate; release builds trust only the system's.
  mkdir -p android/app/src/main/res/xml
  cat > android/app/src/main/res/xml/network_security_config.xml << 'PRISM_XML_EOF'
<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
    <debug-overrides>
        <trust-anchors>
            <certificates src="system" />
            <certificates src="user" />
        </trust-anchors>
    </debug-overrides>
</network-security-config>
PRISM_XML_EOF
  ANDROID_MANIFEST="android/app/src/main/AndroidManifest.xml"
  if ! grep -q "networkSecurityConfig" "$ANDROID_MANIFEST"; then
    perl -0pi -e 's/<application\b/<application\n        android:networkSecurityConfig="\@xml\/network_security_config"/' "$ANDROID_MANIFEST"
  fi

  APP_GRADLE="android/app/build.gradle"
  if ! grep -q "com.google.android.material:material" "$APP_GRADLE"; then
    perl -pi -e 's/^(\s*)(implementation project\(.:capacitor-android.\))\s*$/$1$2\n$1implementation "com.google.android.material:material:1.12.0"\n/' "$APP_GRADLE"
    echo "✓ Material components added for the tab bar"
  fi
else
  echo "⚠️ $JAVA_DIR not found. Run 'npx cap add android' first."
fi
