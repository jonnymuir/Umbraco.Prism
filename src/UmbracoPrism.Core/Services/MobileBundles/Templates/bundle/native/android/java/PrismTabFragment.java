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
        plugins.add(PrismSystemSignInPlugin.class);

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

    View root() {
        return root;
    }

    /** Shows another tab's page, from the next time this tab is shown. */
    void assign(String newPath) {
        path = newPath;
        loaded = false;
        stale = false;
    }

    /**
     * Loads the tab's page the first time it is shown, and reloads it if it has gone stale. The
     * load waits until the tab is on screen: one started while the webview is still hidden leaves
     * screen readers with only the start of the page.
     */
    void showPage() {
        if (!loaded) {
            loaded = true;
            stale = false;
            root.post(() -> bridge.getWebView().loadUrl(PrismShell.urlForPath(path)));
        } else if (stale) {
            stale = false;
            root.post(() -> bridge.getWebView().reload());
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
