using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace Unice
{
    /// <summary>
    /// Signing in, in one place for every Unity Services user (rooms, voice). On Android, Google Play Games signs in by
    /// itself (silently) when the app opens; Unity Services then signs in with that account, otherwise anonymously — a
    /// missing, refused or failed Play Games sign-in never stops the game, solo or online. Its display name reaches Core
    /// through <see cref="AccountRegistry"/> (first-run profile name), and so do achievements and the leaderboard
    /// (<see cref="PlayGamesProgress"/>).
    /// In the Editor, on other platforms and without the Play Console ids (docs/PLAY_GAMES.md): anonymous only.
    /// </summary>
    static class PlayerSignIn
    {
        /// <summary>Going online waits at most this long (seconds) for a Play Games answer before signing in anonymously.</summary>
        const int PlayGamesWaitSeconds = 10;

        /// <summary>The Play Games sign-in started when the app opened: true if signed in. Null when Play Games isn't available.</summary>
        static Task<bool> playGames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => playGames = null;   // Play mode without domain reload

#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            // without the Play Console ids nobody can sign in to Play Games: don't even start it
            var settings = PlayGamesSettings.LoadInstance();
            if (settings == null || string.IsNullOrEmpty(settings.AppId)) return;
            playGames = AuthenticateAsync();
            AccountRegistry.PlayGamesName = async () => await playGames ? PlayGamesPlatform.Instance.GetUserDisplayName() : null;
            AccountRegistry.ReportAchievement = PlayGamesProgress.Report;
            AccountRegistry.ReportOnlineWins = PlayGamesProgress.ReportOnlineWins;
        }

        static Task<bool> AuthenticateAsync()
        {
            var result = new TaskCompletionSource<bool>();
            try { PlayGamesPlatform.Activate().Authenticate(status => result.TrySetResult(status == SignInStatus.Success)); }
            catch (Exception error) { Metrics.Error(error, "play_games"); result.TrySetResult(false); }
            return result.Task;
        }

        /// <summary>A one-time code for Unity Authentication to verify this Play Games player (needs the web client id).</summary>
        static Task<string> ServerAuthCodeAsync()
        {
            var result = new TaskCompletionSource<string>();
            PlayGamesPlatform.Instance.RequestServerSideAccess(false, code => result.TrySetResult(code));
            return result.Task;
        }
#else
        static Task<string> ServerAuthCodeAsync() => Task.FromResult<string>(null);
#endif

        /// <summary>
        /// Starts Unity Services and signs in once per run: with Play Games if it signed in, otherwise anonymously.
        /// Throws only if Unity Services or the anonymous sign-in fail (callers turn that into their message).
        /// </summary>
        public static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            var authentication = AuthenticationService.Instance;
            if (authentication.IsSignedIn) return;
            try
            {
                string code = await PlayGamesCodeAsync();
                if (!string.IsNullOrEmpty(code)) { await authentication.SignInWithGooglePlayGamesAsync(code); return; }
            }
            catch (Exception error) { Metrics.Error(error, "play_games"); }
            await authentication.SignInAnonymouslyAsync();
        }

        /// <summary>The server code if Play Games signed in within <see cref="PlayGamesWaitSeconds"/>; null otherwise.</summary>
        static async Task<string> PlayGamesCodeAsync()
        {
            if (playGames == null) return null;
            var timeout = Task.Delay(TimeSpan.FromSeconds(PlayGamesWaitSeconds));
            if (await Task.WhenAny(playGames, timeout) == timeout || !playGames.Result) return null;
            var code = ServerAuthCodeAsync();
            return await Task.WhenAny(code, timeout) == code ? code.Result : null;
        }
    }
}
