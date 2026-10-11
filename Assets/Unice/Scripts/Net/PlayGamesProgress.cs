using System;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
#endif

namespace Unice
{
    /// <summary>
    /// Sends achievements and the "Online wins" score to Google Play Games, through <see cref="AccountRegistry"/> (wired by
    /// <see cref="PlayerSignIn"/> on Android). Fire and forget: does nothing unless Play Games signed in, never throws to
    /// the game (errors go to <see cref="Metrics.Error"/>). An empty id below means "not created yet": that one isn't sent.
    /// </summary>
    static class PlayGamesProgress
    {
        // The only place with the Play Console ids. Copy each one from Play Console → Play Games Services → Achievements /
        // Leaderboards (or the "Get resources" XML: <string name="achievement_...">); docs/PLAY_GAMES.md, section 5.
        const string NobodySawMeId = "";    // A1
        const string FoundYouId = "";       // A2
        const string DollsKeeperId = "";    // A3
        const string GiveHerBackId = "";    // A4
        const string HotPotatoId = "";      // A5
        const string DecoyId = "";          // A6
        const string ChampionId = "";       // A7
        const string AmongFriendsId = "";   // A8
        const string TopOfTheRoomId = "";   // A9
        const string NoWitnessesId = "";    // A10
        const string VeteranId = "";        // A11 (incremental, 25)
        const string HuntressId = "";       // A12 (incremental, 50)
        const string OnlineWinsLeaderboardId = "";   // L1

        static string IdFor(Achievement achievement) => achievement switch
        {
            Achievement.NobodySawMe => NobodySawMeId,
            Achievement.FoundYou => FoundYouId,
            Achievement.DollsKeeper => DollsKeeperId,
            Achievement.GiveHerBack => GiveHerBackId,
            Achievement.HotPotato => HotPotatoId,
            Achievement.Decoy => DecoyId,
            Achievement.Champion => ChampionId,
            Achievement.AmongFriends => AmongFriendsId,
            Achievement.TopOfTheRoom => TopOfTheRoomId,
            Achievement.NoWitnesses => NoWitnessesId,
            Achievement.Veteran => VeteranId,
            Achievement.Huntress => HuntressId,
            _ => "",
        };

#if UNITY_ANDROID && !UNITY_EDITOR
        public static void Report(Achievement achievement)
        {
            string id = IdFor(achievement);
            if (id.Length == 0) return;
            try
            {
                var platform = PlayGamesPlatform.Instance;
                if (!platform.IsAuthenticated()) return;
                if (Achievements.IsIncremental(achievement)) platform.IncrementAchievement(id, 1, null);
                else platform.UnlockAchievement(id);
            }
            catch (Exception error) { Metrics.Error(error, "play_games"); }
        }

        public static void ReportOnlineWins(long total)
        {
            if (OnlineWinsLeaderboardId.Length == 0) return;
            try
            {
                var platform = PlayGamesPlatform.Instance;
                if (platform.IsAuthenticated()) platform.ReportScore(total, OnlineWinsLeaderboardId, null);
            }
            catch (Exception error) { Metrics.Error(error, "play_games"); }
        }
#endif
    }
}
