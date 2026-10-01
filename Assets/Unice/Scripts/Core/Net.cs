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
        Task InitAsync();
        Task<string> CreateAsync();
        Task JoinAsync(string code);
        Task LeaveAsync();
        bool InRoom { get; }
        string Code { get; }
        string MyId { get; }
        /// <summary>Everyone connected to the room, this device included.</summary>
        IReadOnlyList<PeerInfo> Peers { get; }
        void SetPresence(Presence p);
        void BroadcastSnapshot(byte[] data, int ep);
        /// <summary>Raised when the connection to the room is lost for good. The text explains why.</summary>
        event Action<string> Lost;
    }

    public class NetException : Exception { public NetException(string msg) : base(msg) { } }

    public static class NetRegistry
    {
        /// <summary>Set by Unice.Net at startup when the multiplayer packages are present.</summary>
        public static Func<INetBackend> Factory;
    }
}
