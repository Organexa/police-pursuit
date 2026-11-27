package com.policepursuit.signin;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

// Called from Core/Auth.cs.
public final class GoogleSignIn {
    public interface Callback {
        // Both empty means the player closed the account list.
        void onResult(String idToken, String error);
    }

    public static void signIn(Activity activity, String webClientId, String hashedNonce, Callback callback) {
        activity.runOnUiThread(() -> {
            try {
                GetCredentialRequest request = new GetCredentialRequest.Builder()
                        .addCredentialOption(new GetSignInWithGoogleOption.Builder(webClientId)
                                .setNonce(hashedNonce)
                                .build())
                        .build();
                CredentialManager.create(activity).getCredentialAsync(activity, request,
                        new CancellationSignal(), Runnable::run,
                        new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                            @Override
                            public void onResult(GetCredentialResponse response) {
                                deliver(response.getCredential(), callback);
                            }

                            @Override
                            public void onError(GetCredentialException e) {
                                // Closing the account list is silent. Google also "cancels" for setup
                                // faults ("[16] Account reauth failed"), and those are shown.
                                boolean closed = e instanceof GetCredentialCancellationException
                                        && !e.toString().contains("reauth");
                                send(callback, null, closed ? null : e.toString());
                            }
                        });
            } catch (Exception e) {
                send(callback, null, e.toString());
            }
        });
    }

    // Forgets the chosen account so the picker is shown again next time.
    public static void signOut(Activity activity) {
        CredentialManager.create(activity).clearCredentialStateAsync(new ClearCredentialStateRequest(),
                new CancellationSignal(), Runnable::run,
                new CredentialManagerCallback<Void, ClearCredentialException>() {
                    @Override
                    public void onResult(Void result) { }

                    @Override
                    public void onError(ClearCredentialException e) { }
                });
    }

    // Never pass null to Unity: its Java proxy cannot match the C# method for a null argument, and the
    // answer is then lost without a word.
    private static void send(Callback callback, String idToken, String error) {
        callback.onResult(idToken == null ? "" : idToken, error == null ? "" : error);
    }

    private static void deliver(Credential credential, Callback callback) {
        try {
            if (credential instanceof CustomCredential
                    && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                CustomCredential custom = (CustomCredential) credential;
                send(callback, GoogleIdTokenCredential.createFrom(custom.getData()).getIdToken(), null);
            } else {
                send(callback, null, "unexpected credential type");
            }
        } catch (Exception e) {
            send(callback, null, e.toString());
        }
    }
}
