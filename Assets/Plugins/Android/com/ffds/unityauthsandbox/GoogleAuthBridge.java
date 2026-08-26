package com.ffds.unityauthsandbox;

import android.app.Activity;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.CustomCredential;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

public final class GoogleAuthBridge {
    public interface Callback {
        void onSuccess(String idToken);

        void onCanceled();

        void onError(String message);
    }

    private GoogleAuthBridge() {
    }

    public static void requestIdToken(Activity activity, String webClientId, final Callback callback) {
        if (activity == null) {
            callback.onError("Activity nula.");
            return;
        }

        if (webClientId == null || webClientId.trim().isEmpty()) {
            callback.onError("Web Client ID vacio.");
            return;
        }

        try {
            CredentialManager credentialManager = CredentialManager.create(activity);
            GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(webClientId).build();
            GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(option)
                .build();

            credentialManager.getCredentialAsync(
                activity,
                request,
                null,
                activity.getMainExecutor(),
                new CredentialManagerCallback<androidx.credentials.GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(androidx.credentials.GetCredentialResponse result) {
                        Credential credential = result.getCredential();

                        if (credential instanceof CustomCredential) {
                            CustomCredential customCredential = (CustomCredential) credential;
                            if (GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(customCredential.getType())) {
                                try {
                                    GoogleIdTokenCredential googleCredential =
                                        GoogleIdTokenCredential.createFrom(customCredential.getData());
                                    callback.onSuccess(googleCredential.getIdToken());
                                    return;
                                } catch (Exception exception) {
                                    callback.onError(
                                        "No se pudo interpretar la credencial de Google: " + exception.getMessage()
                                    );
                                    return;
                                }
                            }
                        }

                        callback.onError("La credencial devuelta no es un token de Google valido.");
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        if (e instanceof GetCredentialCancellationException || e instanceof NoCredentialException) {
                            callback.onCanceled();
                            return;
                        }

                        callback.onError(e.getMessage() != null ? e.getMessage() : e.toString());
                    }
                }
            );
        } catch (Exception exception) {
            callback.onError(exception.getMessage() != null ? exception.getMessage() : exception.toString());
        }
    }
}
