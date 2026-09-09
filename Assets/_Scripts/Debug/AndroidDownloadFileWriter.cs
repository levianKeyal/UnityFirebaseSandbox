using System;
using UnityEngine;

public static class AndroidDownloadFileWriter
{
    public static bool TrySaveText(
        string fileName,
        string content,
        out string location,
        out string error
    )
    {
        location = null;
        error = null;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "A report file name is required.";
            return false;
        }

        if (content == null)
        {
            error = "Report content is required.";
            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass(
                "com.ffds.unityauthsandbox.DownloadFileBridge"
            ))
            {
                string result = bridge.CallStatic<string>(
                    "saveTextToDownloads",
                    fileName,
                    content
                );

                if (string.IsNullOrEmpty(result))
                {
                    error = "Android download bridge returned no result.";
                    return false;
                }

                string[] parts = result.Split(new[] { '|' }, 3);
                if (parts.Length > 0 && parts[0] == "OK")
                {
                    location = parts.Length > 2 ? parts[2] : "Downloads/UnityFirebaseSandbox";
                    return true;
                }

                error = parts.Length > 1 ? parts[1] : result;
                return false;
            }
        }
        catch (Exception exception)
        {
            error = $"Android download bridge failed: {exception.Message}";
            return false;
        }
#else
        error = "Android Downloads export is only available in an Android build.";
        return false;
#endif
    }
}
