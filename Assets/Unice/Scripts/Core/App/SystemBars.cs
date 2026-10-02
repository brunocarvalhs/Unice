using System;
using UnityEngine;

namespace Unice
{
    static class SystemBars
    {
        /// <summary>Android: hide the status and navigation bars (they come back after the keyboard or leaving the app).</summary>
        public static void Hide()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using var version = new AndroidJavaClass("android.os.Build$VERSION");
                        var window = activity.Call<AndroidJavaObject>("getWindow");
                        if (version.GetStatic<int>("SDK_INT") >= 30)
                        {
                            using var types = new AndroidJavaClass("android.view.WindowInsets$Type");
                            var ctrl = window.Call<AndroidJavaObject>("getInsetsController");
                            ctrl.Call("hide", types.CallStatic<int>("statusBars") | types.CallStatic<int>("navigationBars"));
                            ctrl.Call("setSystemBarsBehavior", 2);   // BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
                        }
                        else window.Call<AndroidJavaObject>("getDecorView").Call("setSystemUiVisibility", 5894);   // immersive sticky
                    }
                    catch (Exception x) { Metrics.Error(x); }
                }));
            }
            catch (Exception x) { Metrics.Error(x); }
#endif
        }
    }
}
