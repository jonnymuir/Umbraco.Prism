package @@PRISM_PACKAGE@@;

import androidx.annotation.NonNull;
import com.google.firebase.messaging.RemoteMessage;
import io.capawesome.capacitorjs.plugins.firebase.messaging.MessagingService;

/**
 * The app's Firebase messaging service, in place of the push plugin's own (see the manifest): a
 * journey status is shown here, as only the app can build a Live Update, and every other message
 * goes to the plugin as before.
 */
public class PrismJourneyStatusService extends MessagingService {
    @Override
    public void onMessageReceived(@NonNull RemoteMessage message) {
        if (message.getData().containsKey(PrismJourneyStatusNotifications.MARKER)) {
            PrismJourneyStatusNotifications.show(this, message.getData());
            return;
        }
        super.onMessageReceived(message);
    }
}
