package @@PRISM_PACKAGE@@;

import android.content.Intent;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import androidx.activity.result.ActivityResultCaller;
import androidx.activity.result.ActivityResultLauncher;
import androidx.browser.auth.AuthTabIntent;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

/**
 * Opens the system sign-in sheet for prism-system-sign-in.js and returns the callback URL. The
 * callback is an https URL on the tenant's own site, which Chrome's Auth Tab hands only to this
 * app because the site's assetlinks.json names it. Where the browser has no Auth Tab, a Custom Tab
 * opens instead and the callback arrives as a verified App Link (see MainActivity.onNewIntent).
 */
@CapacitorPlugin(name = "PrismSystemSignIn")
public class PrismSystemSignInPlugin extends Plugin {
    static final String CALLBACK_PATH = "/umbraco/prism/mobile/auth/callback";

    private static PrismSystemSignInPlugin waiting;

    private ActivityResultLauncher<Intent> launcher;
    private PluginCall pendingCall;

    @Override
    public void load() {
        // Loaded while the tab's fragment is being created, the only time it may register for results.
        ActivityResultCaller caller = bridge.getFragment() != null ? bridge.getFragment() : getActivity();
        launcher = AuthTabIntent.registerActivityResultLauncher(caller, this::onAuthResult);
    }

    @PluginMethod
    public void authenticate(PluginCall call) {
        Uri url = Uri.parse(call.getString("url", ""));
        if (!PrismShell.isOwnSite(url)
            || !PrismShell.START_URL.getHost().equals(call.getString("callbackHost"))
            || !CALLBACK_PATH.equals(call.getString("callbackPath"))) {
            call.reject("Sign-in must start and end on this app's own site.");
            return;
        }

        Log.i(PrismShell.LOG_TAG, "Sign-in sheet opening");
        if (waiting != null) waiting.finish(null, "Another sign-in started.");
        pendingCall = call;
        waiting = this;
        new AuthTabIntent.Builder()
            .setEphemeralBrowsingEnabled(true)
            .build()
            .launch(launcher, url, PrismShell.START_URL.getHost(), CALLBACK_PATH);
    }

    private void onAuthResult(AuthTabIntent.AuthResult result) {
        if (result.resultCode == AuthTabIntent.RESULT_OK) {
            Log.i(PrismShell.LOG_TAG, "Sign-in sheet returned to the app");
            finish(result.resultUri, null);
            return;
        }
        if (result.resultCode == AuthTabIntent.RESULT_VERIFICATION_FAILED || result.resultCode == AuthTabIntent.RESULT_VERIFICATION_TIMED_OUT) {
            // The site's assetlinks.json does not name this app and signing certificate.
            Log.w(PrismShell.LOG_TAG, "Sign-in callback not verified for this app (result " + result.resultCode + "); check assetlinks.json");
        }
        // A Custom Tab closes as the App Link arrives, so give that a moment to finish first.
        new Handler(Looper.getMainLooper()).postDelayed(() -> finish(null, "Sign-in was cancelled."), 1000);
    }

    /** The App Link for the callback, when a Custom Tab rather than an Auth Tab ran the sign-in. */
    static boolean deliver(Uri uri) {
        if (uri == null || !CALLBACK_PATH.equals(uri.getPath()) || !PrismShell.isOwnSite(uri)) return false;
        Log.i(PrismShell.LOG_TAG, "Sign-in callback arrived as an App Link");
        if (waiting != null) waiting.finish(uri, null);
        return true;
    }

    private void finish(Uri callback, String error) {
        PluginCall call = pendingCall;
        if (call == null) return;
        pendingCall = null;
        if (waiting == this) waiting = null;
        if (callback != null) {
            JSObject result = new JSObject();
            result.put("url", callback.toString());
            call.resolve(result);
        } else {
            call.reject(error, "CANCELLED");
        }
    }
}
