using System;
using System.Threading.Tasks;

namespace Unice
{
    /// <summary>
    /// Proximity voice chat for a room. Implemented in Unice.Net (Vivox); optional, like INetBackend — the game plays fine without it.
    /// </summary>
    public interface IVoiceChat
    {
        bool MicOn { get; set; }
        /// <summary>True in the instants this device is actually transmitting audio (real voice activity, not just MicOn/CanSpeak).</summary>
        bool IsSpeaking { get; }
        Task JoinRoomAsync(string channelName);
        Task LeaveRoomAsync();
        /// <summary>Every tick: speak = this device should be heard right now; hasPos = x/y are a live match position (else everyone stays at the same spot, e.g. the lobby); whisper = transmit quieter.</summary>
        void Tick(bool speak, float x, float y, bool hasPos, bool whisper);
    }

    public static class VoiceRegistry
    {
        /// <summary>Set by Unice.Net at startup when com.unity.services.vivox is present.</summary>
        public static Func<IVoiceChat> Factory;
    }

    public static class VoiceRule
    {
        /// <summary>You're heard by the room iff your mic is on and you're not hidden (hidden covers both the Unice and hiders mid-round; outside a round `me` is null, which also passes).</summary>
        public static bool CanSpeak(bool micOn, Ent me) => micOn && (me == null || me.hidden == null);
    }
}
