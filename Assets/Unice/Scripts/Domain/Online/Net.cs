using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unice
{
    /// <summary>
    /// Online rooms. Implemented in the Unice.Net assembly, which only compiles once the multiplayer packages are installed,
    /// so the game always builds and plays solo even without them.
    /// </summary>
    public interface INetBackend
    {
        /// <summary>Signs in to the online services (once per app run).</summary>
        Task InitAsync();
        /// <summary>Creates a room; returns its code.</summary>
        Task<string> CreateAsync();
        Task JoinAsync(string roomCode);
        Task LeaveAsync();
        bool InRoom { get; }
        string Code { get; }
        /// <summary>This device's id in the room.</summary>
        string LocalId { get; }
        /// <summary>Everyone connected to the room, this device included.</summary>
        IReadOnlyList<PeerInfo> Peers { get; }
        /// <summary>What this device tells the others about its player (sent right away).</summary>
        void SetPresence(Presence presence);
        /// <summary>Host only: sends the match snapshot to everyone.</summary>
        void BroadcastSnapshot(byte[] snapshot, int epoch);
        /// <summary>Raised when the connection to the room is lost for good. The text explains why, for the player.</summary>
        event Action<string> Lost;
    }

    /// <summary>A room operation failed; the message is written for the player.</summary>
    public class NetException : Exception { public NetException(string message) : base(message) { } }

    /// <summary>
    /// The wire protocol this build speaks, from the snapshot and presence format versions. The room creator stores it in
    /// the session; a device with a different value can't play with it and is told to update the app instead of joining.
    /// </summary>
    public static class Protocol
    {
        public const int Id = Game.SnapshotVersion * 256 + PresenceCodec.Version;
    }

    public static class NetRegistry
    {
        /// <summary>Set by Unice.Net at startup when the multiplayer packages are present.</summary>
        public static Func<INetBackend> Factory;
    }

    /// <summary>
    /// The player's Google Play Games account. Set by Unice.Net at startup on Android when Play Games is configured
    /// (docs/PLAY_GAMES.md); null in the Editor, on other platforms and without the Play Console ids.
    /// </summary>
    public static class AccountRegistry
    {
        /// <summary>The Play Games sign-in started when the app opened: the player's display name, or null if it failed.</summary>
        public static Func<Task<string>> PlayGamesName;
        /// <summary>Unlocks (or adds one step to, if incremental) an achievement. Does nothing unless Play Games signed in.</summary>
        public static Action<Achievement> ReportAchievement;
        /// <summary>Sends this device's "Online wins" total to the leaderboard. Does nothing unless Play Games signed in.</summary>
        public static Action<long> ReportOnlineWins;
    }
}
