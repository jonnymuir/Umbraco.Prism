package @@PRISM_PACKAGE@@;

import android.content.Intent;
import android.content.res.Configuration;
import android.os.Bundle;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.WebResourceRequest;
import android.webkit.WebView;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.coordinatorlayout.widget.CoordinatorLayout;
import androidx.fragment.app.Fragment;
import com.getcapacitor.Bridge;
import com.getcapacitor.BridgeWebChromeClient;
import com.getcapacitor.BridgeWebViewClient;
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
    private boolean returnsToRootWhenShown;
    private PrismLoadingRing loadingRing;

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
        loadingRing = new PrismLoadingRing(requireContext());

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
                    pageLoaded(webView);
                }
            })
            .create();
        bridge.setWebViewClient(new TabWebViewClient(bridge));
        bridge.getWebView().setWebChromeClient(new TabChromeClient(bridge));
        CoordinatorLayout.LayoutParams ringPlacement = new CoordinatorLayout.LayoutParams(loadingRing.sizePx(), loadingRing.sizePx());
        ringPlacement.gravity = Gravity.CENTER;
        ((ViewGroup) root).addView(loadingRing, ringPlacement);
        bridge.getWebView().setBackgroundColor(PrismPageColours.background(requireContext()));

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

    private void pageLoaded(WebView webView) {
        PrismPageColours.read(webView, webView::setBackgroundColor);
        if (getActivity() instanceof MainActivity) ((MainActivity) getActivity()).checkSession();
    }

    /**
     * Loads the tab's page the first time it is shown, and reloads it if it has gone stale. The
     * load waits until the tab is on screen: one started while the webview is still hidden leaves
     * screen readers with only the start of the page.
     */
    void showPage() {
        if (!loaded || returnsToRootWhenShown) {
            loaded = true;
            stale = false;
            returnsToRootWhenShown = false;
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

    /**
     * Capacitor's own client, except that another tab's first page opens in that tab. When the
     * member did not simply tap a link (a redirect after signing in or out, a form, a script), this
     * tab's own page is out of date, so it goes back to its first page the next time it is shown.
     */
    private final class TabWebViewClient extends BridgeWebViewClient {
        TabWebViewClient(Bridge bridge) {
            super(bridge);
        }

        @Override
        public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
            if (request.isForMainFrame() && openInAnotherTab(request)) return true;
            return super.shouldOverrideUrlLoading(view, request);
        }

        private boolean openInAnotherTab(WebResourceRequest request) {
            MainActivity activity = getActivity() instanceof MainActivity ? (MainActivity) getActivity() : null;
            if (activity == null || !activity.selectTab(request.getUrl(), PrismTabFragment.this)) return false;
            boolean followsLink = request.hasGesture() && !request.isRedirect();
            if (!followsLink) returnsToRootWhenShown = true;
            return true;
        }
    }

    /** Capacitor's own chrome client, also telling the loading ring how far the page has loaded. */
    private final class TabChromeClient extends BridgeWebChromeClient {
        TabChromeClient(Bridge bridge) {
            super(bridge);
        }

        @Override
        public void onProgressChanged(WebView view, int progress) {
            super.onProgressChanged(view, progress);
            loadingRing.onProgress(progress);
        }
    }
}
