using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Every step: the players this device must not hear right now by <see cref="VoiceRule.CanHear"/>. Separate from
        /// <see cref="SetPlayerMuted"/>: it doesn't touch the player's own mute choices and lifts as soon as the rule allows.
        /// </summary>
        void SetUnheard(HashSet<string> voicePlayerIds);
    }

    public static class VoiceRegistry
    {
        /// <summary>
        /// Voice chat off for M1: Brazil's ECA Digital (Lei 15.211/2025) requires user-to-user voice chat to be limited by
        /// default for minors, with parental consent and age verification, which the game doesn't have yet. With this off,
        /// <see cref="Factory"/> is never set, so there's no Vivox login, no mic request and no voice UI.
        /// To turn voice back on: set this to true AND delete the "voice off" removals in Assets/Plugins/Android/AndroidManifest.xml.
        /// </summary>
        public const bool Enabled = false;

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

        /// <summary>
        /// Mid-round, a caught player is heard only by other caught players, so they can't tip off the ones still playing (the Unice
        /// included); a caught listener still hears everyone. Outside a round (<paramref name="live"/> false) everyone hears everyone,
        /// even though <c>outOfRound</c> stays set on the result screen. <paramref name="listener"/> is null when this device has no character.
        /// </summary>
        public static bool CanHear(bool live, Character listener, Character speaker) =>
            !live || listener == null || listener.outOfRound || speaker == null || !speaker.outOfRound;
    }
}
