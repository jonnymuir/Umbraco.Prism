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
