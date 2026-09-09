package com.ffds.unityauthsandbox;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;

import com.unity3d.player.UnityPlayer;

import java.io.OutputStream;
import java.nio.charset.StandardCharsets;

public final class DownloadFileBridge {
    private DownloadFileBridge() {
    }

    public static String saveTextToDownloads(String fileName, String text) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            return "ERROR|Unity activity is unavailable";
        }

        if (fileName == null || fileName.trim().isEmpty()) {
            return "ERROR|File name is required";
        }

        if (text == null) {
            return "ERROR|File content is required";
        }

        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
            return "ERROR|Android Downloads export requires Android 10 or newer";
        }

        ContentResolver resolver = activity.getContentResolver();
        ContentValues values = new ContentValues();
        values.put(MediaStore.Downloads.DISPLAY_NAME, fileName);
        values.put(MediaStore.Downloads.MIME_TYPE, "text/plain");
        values.put(
            MediaStore.Downloads.RELATIVE_PATH,
            Environment.DIRECTORY_DOWNLOADS + "/UnityFirebaseSandbox"
        );
        values.put(MediaStore.Downloads.IS_PENDING, 1);

        Uri uri = null;
        try {
            uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values);
            if (uri == null) {
                return "ERROR|MediaStore could not create the download entry";
            }

            try (OutputStream outputStream = resolver.openOutputStream(uri)) {
                if (outputStream == null) {
                    throw new IllegalStateException("MediaStore output stream is unavailable");
                }

                outputStream.write(text.getBytes(StandardCharsets.UTF_8));
                outputStream.flush();
            }

            ContentValues completedValues = new ContentValues();
            completedValues.put(MediaStore.Downloads.IS_PENDING, 0);
            resolver.update(uri, completedValues, null, null);
            return "OK|" + fileName + "|Downloads/UnityFirebaseSandbox";
        } catch (Exception exception) {
            if (uri != null) {
                resolver.delete(uri, null, null);
            }

            return "ERROR|" + exception.getClass().getSimpleName() + ": " + exception.getMessage();
        }
    }
}
