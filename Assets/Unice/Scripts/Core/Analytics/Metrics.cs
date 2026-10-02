using System;
using System.Collections.Generic;
using System.Linq;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// Google Analytics (GA4) and Crashlytics through Firebase. Events and errors logged before Firebase is ready are queued, never lost;
    /// if Firebase can't start, everything is a no-op so the game never depends on it. Off in the Editor.
    /// Every analytics event is also a Crashlytics breadcrumb, so a crash report shows what the player was doing.
    /// </summary>
    public static class Metrics
    {
        static volatile bool ready, failed;
        static readonly List<Action> pending = new List<Action>();

        public static void Init()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(check =>
            {
                if (check.IsFaulted || check.Result != DependencyStatus.Available)
                {
                    failed = true; lock (pending) pending.Clear();
                    Debug.LogWarning("firebase: " + (check.Exception?.Message ?? check.Result.ToString()));
                    return;
                }
                _ = FirebaseApp.DefaultInstance;   // Crashlytics starts with the default app
                Crashlytics.IsCrashlyticsCollectionEnabled = !Application.isEditor;
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(!Application.isEditor);
                ready = true;
                SetLanguageProperty();
                lock (pending) { foreach (var action in pending) RunWhenReady(action); pending.Clear(); }
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// An analytics event, e.g. <c>Log("round_end", ("won", true))</c>. GA4 rules: names and keys are snake_case
        /// letters, digits and underscores only (no dots or spaces), at most 40 characters.
        /// </summary>
        public static void Log(string name, params (string key, object value)[] args)
        {
            var parameters = new Parameter[args.Length];
            for (int i = 0; i < args.Length; i++) parameters[i] = ToParameter(args[i].key, args[i].value);
            string breadcrumb = args.Length == 0 ? name : name + " " + string.Join(" ", args.Select(arg => arg.key + "=" + arg.value));
            RunWhenReady(() => { FirebaseAnalytics.LogEvent(name, parameters); Crashlytics.Log(breadcrumb); });
        }

        /// <summary>A screen view (GA's "screen_view"), and the current screen as a Crashlytics key.</summary>
        public static void Screen(string name)
        {
            Log(FirebaseAnalytics.EventScreenView, (FirebaseAnalytics.ParameterScreenName, name), (FirebaseAnalytics.ParameterScreenClass, name));
            RunWhenReady(() => Crashlytics.SetCustomKey("phase", name));
        }

        /// <summary>A handled exception: logged locally and sent to Crashlytics as non-fatal, so it shows up in Firebase without crashing the game.</summary>
        public static void Error(Exception error, string where = null)
        {
            Debug.LogWarning(where == null ? error.ToString() : where + ": " + error);
            RunWhenReady(() => { if (where != null) Crashlytics.Log("error in " + where); Crashlytics.LogException(error); });
        }

        /// <summary>The in-game language as a GA user property (GA already knows the device's).</summary>
        public static void SetLanguageProperty()
        {
            if (ready) FirebaseAnalytics.SetUserProperty("game_lang", Localization.Current.ToString().ToLowerInvariant());
        }

        /// <summary>Runs now if Firebase is ready, queues it if it is still starting, drops it if Firebase failed.</summary>
        static void RunWhenReady(Action action)
        {
            if (failed) return;
            if (!ready) { lock (pending) if (!ready) { pending.Add(action); return; } }   // Error() may come from the Android UI thread
            try { action(); } catch (Exception error) { Debug.LogWarning(error); }
        }

        /// <summary>GA4 parameters are numbers or text; booleans go as 1/0.</summary>
        static Parameter ToParameter(string key, object value) => value switch
        {
            bool flag => new Parameter(key, flag ? 1L : 0L),
            int number => new Parameter(key, number),
            long number => new Parameter(key, number),
            float number => new Parameter(key, number),
            double number => new Parameter(key, number),
            _ => new Parameter(key, value?.ToString() ?? ""),
        };
    }
}
