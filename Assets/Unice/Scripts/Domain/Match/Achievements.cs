using System.Collections.Generic;

namespace Unice
{
    /// <summary>The Google Play Games achievements (docs/PLAY_GAMES.md, section 5). The A-numbers are the design's.</summary>
    public enum Achievement
    {
        /// <summary>A1: survive a round as a hider.</summary>
        NobodySawMe,
        /// <summary>A2: catch someone as the Unice.</summary>
        FoundYou,
        /// <summary>A3: still have the doll when the time runs out.</summary>
        DollsKeeper,
        /// <summary>A4: as the Unice, catch whoever has the doll.</summary>
        GiveHerBack,
        /// <summary>A5: pass the doll and your team wins the round.</summary>
        HotPotato,
        /// <summary>A6: laugh on purpose without the doll and survive the round.</summary>
        Decoy,
        /// <summary>A7: finish a match in 1st place (a tie counts).</summary>
        Champion,
        /// <summary>A8: finish an online match with another person.</summary>
        AmongFriends,
        /// <summary>A9: win an online match with 3+ people.</summary>
        TopOfTheRoom,
        /// <summary>A10 (hidden): as the Unice, catch everyone — the doll last.</summary>
        NoWitnesses,
        /// <summary>A11, incremental (25 steps): one step per finished match.</summary>
        Veteran,
        /// <summary>A12, incremental (50 steps): one step per hider caught as the Unice.</summary>
        Huntress,
    }

    /// <summary>What this player did in the round that just ended, seen from their own device.</summary>
    public sealed class RoundFacts
    {
        /// <summary>The Unice caught the doll holder (false: the time ran out).</summary>
        public bool uniceWon;
        public bool iAmUnice;
        /// <summary>I was caught without the doll and sat out the rest of the round.</summary>
        public bool iWasCaught;
        /// <summary>I had the doll at the end (<see cref="RoundResult.dollHolder"/>).</summary>
        public bool iHeldDoll;
        /// <summary>I passed the doll at some point this round.</summary>
        public bool iPassedDoll;
        /// <summary>I laughed on purpose this round while someone else had the doll.</summary>
        public bool iLaughedAsDecoy;
        /// <summary>Everyone who isn't the Unice, doll holder included.</summary>
        public int hiders;
        /// <summary>Hiders neither caught nor holding the doll at the end.</summary>
        public int hidersFree;
    }

    /// <summary>
    /// The rules for Play Games: which achievements a moment earns and whether a match counts for the "Online wins"
    /// leaderboard. Pure: <c>EventFeed</c> gathers the facts and Unice.Net sends the result (docs/PLAY_GAMES.md).
    /// Reporting an achievement already unlocked is harmless — Play Games keeps the first unlock.
    /// </summary>
    public static class Achievements
    {
        /// <summary><see cref="Achievement.NoWitnesses"/> needs at least this many hiders, so a 1v1 doesn't give it away.</summary>
        public const int NoWitnessesMinHiders = 3;
        /// <summary><see cref="Achievement.TopOfTheRoom"/>: people (not bots) in the room at the end.</summary>
        public const int TopOfTheRoomMinHumans = 3;
        /// <summary>Leaderboard filter: a real match, not a quick one in a room set to the minimum times.</summary>
        public const int LeaderboardMinRounds = 3;
        /// <summary>Leaderboard filter, in seconds on this device's clock (6 minutes): caps how fast two accounts can farm wins.</summary>
        public const float LeaderboardMinSeconds = 6 * 60;

        public static bool IsIncremental(Achievement achievement) => achievement == Achievement.Veteran || achievement == Achievement.Huntress;

        /// <summary>A catch happened; <paramref name="iCaught"/>: this player is the Unice who caught.</summary>
        public static List<Achievement> ForCatch(bool iCaught, bool withDoll)
        {
            var earned = new List<Achievement>();
            if (!iCaught) return earned;
            earned.Add(Achievement.FoundYou);
            earned.Add(Achievement.Huntress);
            if (withDoll) earned.Add(Achievement.GiveHerBack);
            return earned;
        }

        public static List<Achievement> ForRoundEnd(RoundFacts facts)
        {
            var earned = new List<Achievement>();
            bool survivedAsHider = !facts.uniceWon && !facts.iAmUnice && !facts.iWasCaught;
            if (survivedAsHider) earned.Add(Achievement.NobodySawMe);
            if (survivedAsHider && facts.iHeldDoll) earned.Add(Achievement.DollsKeeper);
            if (survivedAsHider && facts.iPassedDoll) earned.Add(Achievement.HotPotato);
            if (survivedAsHider && facts.iLaughedAsDecoy) earned.Add(Achievement.Decoy);
            if (facts.uniceWon && facts.iAmUnice && facts.hiders >= NoWitnessesMinHiders && facts.hidersFree == 0) earned.Add(Achievement.NoWitnesses);
            return earned;
        }

        /// <summary>The final scoreboard. <paramref name="humans"/>: people (not bots) still in the match.</summary>
        public static List<Achievement> ForMatchEnd(int place, int humans, bool online)
        {
            var earned = new List<Achievement> { Achievement.Veteran };
            if (place == 1) earned.Add(Achievement.Champion);
            if (online && humans >= 2) earned.Add(Achievement.AmongFriends);
            if (online && humans >= TopOfTheRoomMinHumans && place == 1) earned.Add(Achievement.TopOfTheRoom);
            return earned;
        }

        /// <summary>
        /// The win counts for "Online wins": online, with another person, at least <see cref="LeaderboardMinRounds"/> rounds
        /// and <see cref="LeaderboardMinSeconds"/> on this device's clock. Solo never counts (one human).
        /// </summary>
        public static bool CountsForLeaderboard(int place, int humans, bool online, int totalRounds, float seconds) =>
            place == 1 && online && humans >= 2 && totalRounds >= LeaderboardMinRounds && seconds >= LeaderboardMinSeconds;
    }
}
