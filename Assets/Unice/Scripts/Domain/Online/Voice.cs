using System;
using System.Threading.Tasks;

namespace Unice
{
    /// <summary>
    /// Proximity voice chat for a room. Implemented in Unice.Net (Vivox); optional, like <see cref="INetBackend"/> — the game plays fine without it.
    /// </summary>
    public interface IVoiceChat
    {
        bool MicOn { get; set; }
        /// <summary>True in the instants this device is actually transmitting audio (real voice activity, not just MicOn/CanSpeak).</summary>
        bool IsSpeaking { get; }
        Task JoinRoomAsync(string channelName);
        Task LeaveRoomAsync();
        /// <summary>
        /// Every step. <paramref name="speak"/>: this device should be heard right now. <paramref name="hasPosition"/>: x/y are a live
        /// match position (otherwise everyone stays at the same spot, e.g. the waiting room). <paramref name="whisper"/>: transmit quieter.
        /// </summary>
        void Tick(bool speak, float x, float y, bool hasPosition, bool whisper);

        /// <summary>This device's id in the voice channel (sent in its presence), or null before it joins voice.</summary>
        string VoicePlayerId { get; }
        /// <summary>Stops (or resumes) hearing one player, on this device only. Remembered while the app runs, even if they rejoin.</summary>
        void SetPlayerMuted(string voicePlayerId, bool muted);
        bool IsPlayerMuted(string voicePlayerId);
    }

    public static class VoiceRegistry
    {
        /// <summary>Set by Unice.Net at startup when com.unity.services.vivox is present.</summary>
        public static Func<IVoiceChat> Factory;
    }

    public static class VoiceRule
    {
        /// <summary>
        /// You're heard by the room only if your mic is on and you're not hidden (hidden covers both the Unice and hiders mid-round;
        /// outside a round <paramref name="myCharacter"/> is null, which also passes).
        /// </summary>
        public static bool CanSpeak(bool micOn, Character myCharacter) => micOn && (myCharacter == null || myCharacter.hidingSpot == null);
    }
}
