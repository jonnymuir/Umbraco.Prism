package @@PRISM_PACKAGE@@;

import android.app.Activity;
import android.app.Application;
import android.content.Context;
import android.os.Bundle;
import android.util.Log;
import android.webkit.CookieManager;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Tells the site this app shows journey statuses, with the member's session cookie, when the app
 * starts, comes to the front and the member signs in. Until the page has registered for push
 * notifications, which statuses travel through, the site answers 409 and the app tries again
 * when next brought to the front. Signing out clears every status.
 */
final class PrismJourneyStatusDevice {
    private static final ExecutorService sender = Executors.newSingleThreadExecutor();
    private static volatile boolean registered;

    private PrismJourneyStatusDevice() {}

    static void start(Activity activity) {
        Context context = activity.getApplicationContext();
        register();
        activity.getApplication().registerActivityLifecycleCallbacks(new Resumed());
        PrismShell.addSessionListener(signedIn -> {
            registered = false;
            if (signedIn) {
                register();
            } else {
                Log.i(PrismShell.LOG_TAG, "Signed out; ending journey statuses");
                PrismJourneyStatusNotifications.endAll(context);
            }
        });
    }

    private static void register() {
        if (registered) return;
        sender.execute(() -> registered = post("{\"platform\":\"android\"}"));
    }

    private static boolean post(String body) {
        try {
            HttpURLConnection connection = (HttpURLConnection) new URL(PrismShell.origin() + "/umbraco/prism/journey-status/device").openConnection();
            connection.setRequestMethod("POST");
            connection.setInstanceFollowRedirects(false);
            connection.setRequestProperty("Content-Type", "application/json");
            String cookies = CookieManager.getInstance().getCookie(PrismShell.origin());
            if (cookies != null) connection.setRequestProperty("Cookie", cookies);
            connection.setDoOutput(true);
            try (OutputStream out = connection.getOutputStream()) {
                out.write(body.getBytes(StandardCharsets.UTF_8));
            }
            int status = connection.getResponseCode();
            Log.i(PrismShell.LOG_TAG, "Journey status device sent: " + status);
            return status == HttpURLConnection.HTTP_NO_CONTENT;
        } catch (Exception e) {
            Log.w(PrismShell.LOG_TAG, "Journey status device not sent: " + e.getMessage());
            return false;
        }
    }

    private static final class Resumed implements Application.ActivityLifecycleCallbacks {
        @Override public void onActivityResumed(Activity activity) { register(); }
        @Override public void onActivityCreated(Activity activity, Bundle state) {}
        @Override public void onActivityStarted(Activity activity) {}
        @Override public void onActivityPaused(Activity activity) {}
        @Override public void onActivityStopped(Activity activity) {}
        @Override public void onActivitySaveInstanceState(Activity activity, Bundle state) {}
        @Override public void onActivityDestroyed(Activity activity) {}
    }
}
