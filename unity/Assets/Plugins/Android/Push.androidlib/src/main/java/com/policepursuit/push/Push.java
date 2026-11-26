package com.policepursuit.push;

import android.app.Activity;
import android.util.Log;

import com.google.firebase.FirebaseApp;
import com.google.firebase.messaging.FirebaseMessaging;

// Called from Core/Push.cs. Messages sent to a closed or background app are shown by Android itself.
// shortcut: nothing is shown while the game is open; add a FirebaseMessagingService if that matters.
public final class Push {
    public interface Callback {
        void onToken(String token);
    }

    // Does nothing in a build made without google-services.json (CI writes its values to res/values).
    public static void start(Activity activity, Callback callback) {
        try {
            if (FirebaseApp.getApps(activity).isEmpty() && FirebaseApp.initializeApp(activity) == null) {
                return;
            }
            FirebaseMessaging messaging = FirebaseMessaging.getInstance();
            messaging.subscribeToTopic("all");
            messaging.getToken().addOnCompleteListener(task -> {
                if (task.isSuccessful()) callback.onToken(task.getResult());
            });
        } catch (Exception e) {
            Log.w("Push", "Push not started: " + e);
        }
    }
}
