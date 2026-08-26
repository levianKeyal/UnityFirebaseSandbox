using System;
using System.Threading.Tasks;
using UnityEngine;

public static class GoogleAuthBridge
{
    public static Task<string> RequestIdTokenAsync(string webClientId)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        TaskCompletionSource<string> completionSource = new TaskCompletionSource<string>();

        try
        {
            using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass bridgeClass = new AndroidJavaClass("com.ffds.unityauthsandbox.GoogleAuthBridge"))
            {
                bridgeClass.CallStatic(
                    "requestIdToken",
                    activity,
                    webClientId,
                    new GoogleAuthBridgeCallback(completionSource)
                );
            }
        }
        catch (Exception exception)
        {
            completionSource.TrySetException(exception);
        }

        return completionSource.Task;
#else
        return Task.FromResult(string.Empty);
#endif
    }

    private sealed class GoogleAuthBridgeCallback : AndroidJavaProxy
    {
        private readonly TaskCompletionSource<string> completionSource;

        public GoogleAuthBridgeCallback(TaskCompletionSource<string> completionSource)
            : base("com.ffds.unityauthsandbox.GoogleAuthBridge$Callback")
        {
            this.completionSource = completionSource;
        }

        public void onSuccess(string idToken)
        {
            completionSource.TrySetResult(idToken);
        }

        public void onCanceled()
        {
            completionSource.TrySetResult(string.Empty);
        }

        public void onError(string message)
        {
            completionSource.TrySetException(new Exception(message));
        }
    }
}
