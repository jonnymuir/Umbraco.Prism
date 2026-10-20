package @@PRISM_PACKAGE@@;

import android.content.Context;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;
import androidx.appcompat.view.ContextThemeWrapper;
import androidx.core.graphics.ColorUtils;
import com.google.android.material.color.MaterialColors;
import com.google.android.material.progressindicator.CircularProgressIndicator;

/**
 * The loading indicator for a tab: Material's circular indicator in the site's brand colour, on a
 * small disc so it stands out on any page. Android's webview keeps showing the outgoing page until
 * the next one paints, so nothing needs holding as on iOS; the ring only appears once a page has
 * taken long enough to load that the member might wonder whether anything is happening.
 */
final class PrismLoadingRing extends FrameLayout {
    /** How long a page loads before the ring appears. */
    private static final long DELAY_MS = 1000;
    private static final int DISC_DP = 72;
    private static final int RING_DP = 40;

    private final CircularProgressIndicator indicator;
    private final GradientDrawable disc;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final Runnable reveal = this::reveal;
    private boolean loading;

    PrismLoadingRing(Context context) {
        super(new ContextThemeWrapper(context, com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar));
        Context themed = getContext();
        float density = context.getResources().getDisplayMetrics().density;

        disc = new GradientDrawable();
        disc.setShape(GradientDrawable.OVAL);
        setBackground(disc);
        setElevation(6 * density);

        indicator = new CircularProgressIndicator(themed);
        indicator.setIndeterminate(true);
        indicator.setIndicatorSize(Math.round(RING_DP * density));
        indicator.setTrackThickness(Math.round(4 * density));
        indicator.setTrackCornerRadius(Math.round(2 * density));
        addView(indicator, new LayoutParams(LayoutParams.WRAP_CONTENT, LayoutParams.WRAP_CONTENT, Gravity.CENTER));

        setContentDescription("Loading");
        setVisibility(View.GONE);
    }

    /** The disc's width and height, in pixels. */
    int sizePx() {
        return Math.round(DISC_DP * getResources().getDisplayMetrics().density);
    }

    /** Called with the page's load progress; the ring appears once loading has gone on long enough. */
    void onProgress(int progress) {
        boolean nowLoading = progress < 100;
        if (nowLoading == loading) return;
        loading = nowLoading;
        main.removeCallbacks(reveal);
        if (loading) {
            main.postDelayed(reveal, DELAY_MS);
        } else if (getVisibility() == View.VISIBLE) {
            animate().alpha(0).setDuration(200).withEndAction(() -> setVisibility(View.GONE));
        }
    }

    private void reveal() {
        int accent = PrismPageColours.accent(getContext(), MaterialColors.getColor(this, androidx.appcompat.R.attr.colorPrimary));
        // The disc suits the page behind it, not the phone's own light or dark theme.
        boolean lightPage = ColorUtils.calculateLuminance(PrismPageColours.background(getContext())) > 0.5;
        disc.setColor(lightPage ? Color.argb(235, 255, 255, 255) : Color.argb(235, 32, 32, 36));
        indicator.setIndicatorColor(accent);
        indicator.setTrackColor(ColorUtils.setAlphaComponent(accent, 50));
        animate().cancel();
        setAlpha(0);
        setVisibility(View.VISIBLE);
        animate().alpha(1).setDuration(150);
    }
}
