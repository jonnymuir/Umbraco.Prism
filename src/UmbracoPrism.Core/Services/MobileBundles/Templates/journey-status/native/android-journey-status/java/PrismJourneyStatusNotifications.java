package @@PRISM_PACKAGE@@;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import android.os.Bundle;
import android.service.notification.StatusBarNotification;
import android.util.Log;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * A journey's status as a notification (Design/mobile-journey-status.md). While the member waits
 * it is ongoing, and on Android 16 and later asks to be promoted to a Live Update, with the
 * journey's steps as progress. When the wait is over it shows the last status for a short while.
 * Tagged with the journey's key, so each status replaces the last.
 */
final class PrismJourneyStatusNotifications {
    /** Marks a data message as a journey status, matching the site's PrismJourneyStatusMessages. */
    static final String MARKER = "prismJourneyStatus";

    private static final String CHANNEL = "prism-journey-status";
    private static final int ID = 1;
    private static final long DONE_SHOWS_FOR_MILLIS = 15 * 60 * 1000L;
    private static final String EXTRA_REQUEST_PROMOTED_ONGOING = "android.requestPromotedOngoing";

    private PrismJourneyStatusNotifications() {}

    static void show(Context context, Map<String, String> data) {
        String key = data.get("key");
        String path = data.get("path");
        if (key == null || !PrismShell.isSitePath(path)) return;

        NotificationManager manager = context.getSystemService(NotificationManager.class);
        boolean waiting = "Waiting".equals(data.get("kind"));
        Notification.Builder builder = builder(context, manager)
            .setSmallIcon(context.getApplicationInfo().icon)
            .setContentTitle(data.get("serviceName"))
            .setContentText(data.get("status"))
            .setContentIntent(openPage(context, key, path))
            .setOnlyAlertOnce(true)
            .setOngoing(waiting)
            .setAutoCancel(!waiting);
        if (waiting) {
            builder.setCategory(Notification.CATEGORY_PROGRESS);
            showSteps(builder, number(data.get("step")), number(data.get("steps")));
        } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            builder.setTimeoutAfter(DONE_SHOWS_FOR_MILLIS);
        }

        try {
            manager.notify(key, ID, builder.build());
        } catch (SecurityException e) {
            Log.i(PrismShell.LOG_TAG, "Journey status not shown: notifications are not allowed");
        }
    }

    /** Clears every journey status, when the member signs out. */
    static void endAll(Context context) {
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        for (StatusBarNotification shown : manager.getActiveNotifications()) {
            if (shown.getId() == ID && shown.getTag() != null) manager.cancel(shown.getTag(), ID);
        }
    }

    private static Notification.Builder builder(Context context, NotificationManager manager) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return new Notification.Builder(context);
        manager.createNotificationChannel(new NotificationChannel(CHANNEL, "Journey status", NotificationManager.IMPORTANCE_DEFAULT));
        return new Notification.Builder(context, CHANNEL);
    }

    private static void showSteps(Notification.Builder builder, Integer step, Integer steps) {
        boolean hasSteps = step != null && steps != null && steps > 0;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.BAKLAVA) {
            Notification.ProgressStyle style = new Notification.ProgressStyle();
            if (hasSteps) {
                List<Notification.ProgressStyle.Segment> segments = new ArrayList<>();
                for (int i = 0; i < steps; i++) segments.add(new Notification.ProgressStyle.Segment(1));
                style.setProgressSegments(segments).setProgress(step);
            } else {
                style.setProgressIndeterminate(true);
            }
            Bundle promoted = new Bundle();
            promoted.putBoolean(EXTRA_REQUEST_PROMOTED_ONGOING, true);
            builder.setStyle(style).addExtras(promoted);
        } else {
            builder.setProgress(hasSteps ? steps : 0, hasSteps ? step : 0, !hasSteps);
        }
    }

    private static PendingIntent openPage(Context context, String key, String path) {
        Intent intent = new Intent(context, MainActivity.class)
            .putExtra(PrismShell.EXTRA_OPEN_PATH, path)
            .addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        return PendingIntent.getActivity(context, key.hashCode(), intent, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    private static Integer number(String value) {
        try {
            return value == null ? null : Integer.valueOf(value);
        } catch (NumberFormatException e) {
            return null;
        }
    }
}
