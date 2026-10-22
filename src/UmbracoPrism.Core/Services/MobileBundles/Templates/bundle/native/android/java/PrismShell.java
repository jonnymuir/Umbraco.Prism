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
import java.util.List;
import java.util.Objects;
import java.util.concurrent.CopyOnWriteArrayList;
import org.json.JSONException;
import org.json.JSONObject;

/**
 * The tenant's site, and the Capacitor configuration each tab's bridge opens it with. Every shell
 * message is logged with the tag "PrismShell": read them with {@code adb logcat -s PrismShell}.
 */
final class PrismShell {
    static final String LOG_TAG = "PrismShell";
    static final Uri START_URL = Uri.parse("@@PRISM_START_URL@@");
    static final String MANIFEST_PATH = "/umbraco/prism/mobile/manifest";

    /** An intent extra naming a page of the site to open, such as the one a tapped journey status points to. */
    static final String EXTRA_OPEN_PATH = "prismOpenPath";

    /** Told when the member signs in or out. */
    interface SessionListener {
        void sessionChanged(boolean signedIn);
    }

    private static final List<SessionListener> sessionListeners = new CopyOnWriteArrayList<>();

    private PrismShell() {}

    static void addSessionListener(SessionListener listener) {
        sessionListeners.add(listener);
    }

    static void sessionChanged(boolean signedIn) {
        for (SessionListener listener : sessionListeners) listener.sessionChanged(signedIn);
    }

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

    /** A path on this site, such as /orders: one leading slash, so never another site's address. */
    static boolean isSitePath(String path) {
        return path != null && path.startsWith("/") && !path.startsWith("//");
    }

    /** A site path without its query or trailing slash, so a tab's path and a link can be compared. */
    static String normalizedPath(String path) {
        String withoutQuery = path == null ? "/" : path.split("\\?", 2)[0];
        if (withoutQuery.length() > 1 && withoutQuery.endsWith("/")) withoutQuery = withoutQuery.substring(0, withoutQuery.length() - 1);
        return withoutQuery.isEmpty() ? "/" : withoutQuery;
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
