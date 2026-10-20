package @@PRISM_PACKAGE@@;

import android.annotation.SuppressLint;
import android.content.Context;
import android.content.Intent;
import android.content.res.Configuration;
import android.net.Uri;
import android.graphics.Color;
import android.os.Bundle;
import android.util.Log;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.CookieManager;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import androidx.activity.OnBackPressedCallback;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.view.menu.MenuBuilder;
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
        icons = new PrismTabIcons(this);
        manifest = PrismManifest.cached(this);
        List<PrismManifest.Tab> tabs = firstTabs(manifest);

        int containerId = buildLayout();
        createTabSlots(containerId, tabs);
        listenForNavigation();
        showTabs(tabs);
        refreshManifest();
    }

    /** The tab pages above a Material bottom navigation bar, drawn edge to edge. Returns the pages' container. */
    private int buildLayout() {
        Context themed = new ContextThemeWrapper(this, com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(MaterialColors.getColor(themed, com.google.android.material.R.attr.colorSurface, Color.WHITE));

        FrameLayout container = new FrameLayout(this);
        container.setId(View.generateViewId());
        root.addView(container, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));

        nav = new BottomNavigationView(themed);
        nav.setLabelVisibilityMode(NavigationBarView.LABEL_VISIBILITY_LABELED);
        root.addView(nav, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        setContentView(root);
        fitSystemBars(root);
        return container.getId();
    }

    /**
     * The system bars show the app's own surface behind them, as on Android 15 and later. Pages
     * sit below the status bar; while the keyboard is up it takes the tab bar's place.
     */
    private void fitSystemBars(View root) {
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        getWindow().setStatusBarColor(Color.TRANSPARENT);
        getWindow().setNavigationBarColor(Color.TRANSPARENT);
        boolean night = (getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK) == Configuration.UI_MODE_NIGHT_YES;
        WindowCompat.getInsetsController(getWindow(), root).setAppearanceLightStatusBars(!night);

        ViewCompat.setOnApplyWindowInsetsListener(root, (view, insets) -> {
            Insets bars = insets.getInsets(WindowInsetsCompat.Type.systemBars() | WindowInsetsCompat.Type.displayCutout());
            boolean keyboard = insets.isVisible(WindowInsetsCompat.Type.ime());
            int bottom = keyboard ? insets.getInsets(WindowInsetsCompat.Type.ime()).bottom : 0;
            view.setPadding(bars.left, bars.top, bars.right, bottom);
            nav.setVisibility(keyboard ? View.GONE : View.VISIBLE);
            return insets;
        });
    }

    /** One bridge per possible tab, all created now (see the class remarks); only the first loads its page. */
    private void createTabSlots(int containerId, List<PrismManifest.Tab> tabs) {
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
    }

    /** Tab taps, reselecting a tab to return to its first page, and Back. */
    private void listenForNavigation() {
        nav.setOnItemSelectedListener(item -> {
            select(slotForTab.get(item.getItemId() - 1));
            return true;
        });
        nav.setOnItemReselectedListener(item -> selected.returnToRoot());
        getOnBackPressedDispatcher().addCallback(this, new OnBackPressedCallback(true) {
            @Override
            public void handleOnBackPressed() {
                goBack();
            }
        });
    }

    /** Back goes through the tab's own history, then to the first tab, then leaves the app. */
    private void goBack() {
        if (selected.goBack()) return;
        if (!slotForTab.isEmpty() && selected != slotForTab.get(0)) {
            nav.setSelectedItemId(1);
            return;
        }
        moveTaskToBack(true);
    }

    private static List<PrismManifest.Tab> firstTabs(PrismManifest manifest) {
        return new ArrayList<>(manifest.tabs.subList(0, Math.min(MAX_TABS, manifest.tabs.size())));
    }

    /** Points a bridge at each tab, keeping the bridge that already shows a tab's page. */
    private void showTabs(List<PrismManifest.Tab> tabs) {
        List<PrismTabFragment> assigned = assignSlots(tabs);
        shown.clear();
        shown.addAll(tabs);
        slotForTab.clear();
        slotForTab.addAll(assigned);

        rebuildMenu(tabs);

        int index = slotForTab.indexOf(selected);
        if (index < 0) index = 0;
        nav.getMenu().findItem(index + 1).setChecked(true);
        select(slotForTab.get(index));
    }

    /** A bridge for each tab: the one already showing its page where there is one, otherwise a free one. */
    private List<PrismTabFragment> assignSlots(List<PrismManifest.Tab> tabs) {
        List<PrismTabFragment> free = new ArrayList<>(slots);
        List<PrismTabFragment> assigned = new ArrayList<>();
        for (PrismManifest.Tab tab : tabs) {
            PrismTabFragment existing = free.stream().filter(slot -> slot.path().equals(tab.path)).findFirst().orElse(null);
            assigned.add(existing);
            free.remove(existing);
        }
        for (int i = 0; i < tabs.size(); i++) {
            if (assigned.get(i) == null) {
                PrismTabFragment slot = free.remove(0);
                slot.assign(tabs.get(i).path);
                assigned.set(i, slot);
            }
        }
        return assigned;
    }

    /**
     * Replaces the tab bar's items in one update. Material lays the bar out after every change to
     * its menu, and items added one at a time can keep a width measured partway through, cutting
     * a label short; so the menu holds its updates until every item is in.
     */
    @SuppressLint("RestrictedApi")
    private void rebuildMenu(List<PrismManifest.Tab> tabs) {
        MenuBuilder menu = (MenuBuilder) nav.getMenu();
        menu.stopDispatchingItemsChanged();
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
        menu.startDispatchingItemsChanged();
    }

    private void select(PrismTabFragment slot) {
        if (slot != selected) {
            // The keyboard belongs to the tab being left, and would otherwise cover the tab bar.
            WindowCompat.getInsetsController(getWindow(), getWindow().getDecorView()).hide(WindowInsetsCompat.Type.ime());
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

    /** Switches to the tab whose first page this is, if it is not the tab asking. True when it did. */
    boolean selectTab(Uri url, PrismTabFragment source) {
        if (!PrismShell.isOwnSite(url)) return false;
        String path = PrismShell.normalizedPath(url.getPath());
        for (int i = 0; i < shown.size(); i++) {
            if (PrismShell.normalizedPath(shown.get(i).path).equals(path) && slotForTab.get(i) != source) {
                Log.i(PrismShell.LOG_TAG, "Opening " + path + " in its own tab");
                // A handed-over redirect may follow a sign-in or sign-out with no page loading on the way.
                checkSession();
                int itemId = i + 1;
                nav.post(() -> nav.setSelectedItemId(itemId));
                return true;
            }
        }
        return false;
    }

    /**
     * A sign-in or sign-out in one tab leaves the others showing the old state, so they reload
     * when next shown, and the tabs themselves are fetched again for the new member. Android has no
     * cookie observer, so tabs ask for this check after each page load and each handed-over link.
     */
    void checkSession() {
        boolean signedIn = hasCookie(CookieManager.getInstance().getCookie(PrismShell.origin()), manifest.sessionCookieName());
        Boolean previous = hasSession;
        hasSession = signedIn;
        if (previous == null || previous == signedIn) return;
        Log.i(PrismShell.LOG_TAG, "Member signed " + (signedIn ? "in" : "out") + "; other tabs reload when next shown");
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

    // The sign-in callback goes to the sign-in in progress; notification taps reach the tab that
    // is showing.
    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        if (PrismSystemSignInPlugin.deliver(intent.getData())) return;
        setIntent(intent);
        selected.onNewIntent(intent);
    }
}
