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
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(t =>
            {
                if (t.IsFaulted || t.Result != DependencyStatus.Available) { failed = true; lock (pending) pending.Clear(); Debug.LogWarning("firebase: " + (t.Exception?.Message ?? t.Result.ToString())); return; }
                _ = FirebaseApp.DefaultInstance;   // Crashlytics starts with the default app
                Crashlytics.IsCrashlyticsCollectionEnabled = !Application.isEditor;
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(!Application.isEditor);
                ready = true;
                SetLang();
                lock (pending) { foreach (var a in pending) Run(a); pending.Clear(); }
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static void Log(string name, params (string k, object v)[] args)
        {
            var ps = new Parameter[args.Length];
            for (int i = 0; i < args.Length; i++) ps[i] = P(args[i].k, args[i].v);
            string crumb = args.Length == 0 ? name : name + " " + string.Join(" ", args.Select(a => a.k + "=" + a.v));
            Run(() => { FirebaseAnalytics.LogEvent(name, ps); Crashlytics.Log(crumb); });
        }

        public static void Screen(string name)
        {
            Log(FirebaseAnalytics.EventScreenView, (FirebaseAnalytics.ParameterScreenName, name), (FirebaseAnalytics.ParameterScreenClass, name));
            Run(() => Crashlytics.SetCustomKey("phase", name));
        }

        /// <summary>A handled exception: logged locally and sent to Crashlytics as non-fatal, so it shows up in Firebase without crashing the game.</summary>
        public static void Error(Exception x, string where = null)
        {
            Debug.LogWarning(where == null ? x.ToString() : where + ": " + x);
            Run(() => { if (where != null) Crashlytics.Log("error in " + where); Crashlytics.LogException(x); });
        }

        /// <summary>The in-game language as a GA user property (GA already knows the device's).</summary>
        public static void SetLang()
        {
            if (ready) FirebaseAnalytics.SetUserProperty("game_lang", Loc.Current.ToString().ToLowerInvariant());
        }

        static void Run(Action a)
        {
            if (failed) return;
            if (!ready) { lock (pending) if (!ready) { pending.Add(a); return; } }   // Error() may come from the Android UI thread
            try { a(); } catch (Exception x) { Debug.LogWarning(x); }
        }

        static Parameter P(string k, object v) => v switch
        {
            bool b => new Parameter(k, b ? 1L : 0L),
            int i => new Parameter(k, i),
            long l => new Parameter(k, l),
            float f => new Parameter(k, f),
            double d => new Parameter(k, d),
            _ => new Parameter(k, v?.ToString() ?? ""),
        };
    }
}
