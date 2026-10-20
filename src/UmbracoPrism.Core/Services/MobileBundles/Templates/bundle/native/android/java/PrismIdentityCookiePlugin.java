package @@PRISM_PACKAGE@@;

import android.webkit.CookieManager;
import com.getcapacitor.JSArray;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import java.util.List;
import org.json.JSONException;

/**
 * Expires the identity provider's session cookies on sign-out (see prism-biometric-signout.js),
 * one host at a time. The site's own cookies are never touched: the sign-out request still needs
 * them, and locally the identity provider can share the site's host name on another port.
 */
@CapacitorPlugin(name = "PrismIdentityCookiePlugin")
public class PrismIdentityCookiePlugin extends Plugin {
    @PluginMethod
    public void clearCookies(PluginCall call) {
        CookieManager cookieManager = CookieManager.getInstance();
        int cleared = 0;
        for (String host : hosts(call)) {
            if (host.equalsIgnoreCase(PrismShell.START_URL.getHost())) continue;
            cleared += expireCookies(cookieManager, host);
        }
        cookieManager.flush();
        JSObject result = new JSObject();
        result.put("cleared", cleared);
        call.resolve(result);
    }

    private static List<String> hosts(PluginCall call) {
        JSArray hosts = call.getArray("hosts");
        try {
            return hosts == null ? List.of() : hosts.toList();
        } catch (JSONException e) {
            return List.of();
        }
    }

    /** Expires each cookie the host has, whether it was set for the host alone or its domain. */
    private static int expireCookies(CookieManager cookieManager, String host) {
        String url = "https://" + host + "/";
        String cookies = cookieManager.getCookie(url);
        if (cookies == null) return 0;
        int expired = 0;
        for (String cookie : cookies.split(";")) {
            String name = cookie.split("=", 2)[0].trim();
            if (name.isEmpty()) continue;
            cookieManager.setCookie(url, name + "=; Max-Age=0; Path=/");
            cookieManager.setCookie(url, name + "=; Max-Age=0; Path=/; Domain=" + host);
            expired++;
        }
        return expired;
    }
}
