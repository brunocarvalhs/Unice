using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// Positional proximity voice over Vivox: one channel per room, named after the room code. Best-effort —
    /// any failure just logs and leaves voice off; it never blocks or breaks EnterRoom/gameplay.
    /// </summary>
    public sealed class VivoxVoice : IVoiceChat
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => VoiceRegistry.Factory = VoiceRegistry.Enabled ? () => new VivoxVoice() : null;

        // ponytail: fixed falloff tuned to the house's scale (world units = house px / 100, see Art.PxPerUnit). Expose as settings if it ever needs per-room tuning.
        static readonly Channel3DProperties ChannelProperties = new Channel3DProperties(
            audibleDistance: 8, conversationalDistance: 2, audioFadeIntensityByDistanceaudio: 1f, audioFadeModel: AudioFadeModel.InverseByDistance);

        /// <summary>The room's channel (the room code), or null.</summary>
        string channel;
        bool loggedIn, joined;
        /// <summary>What was last applied to the mic: unmuted, and at whisper volume.</summary>
        bool speaking, whispering;
        /// <summary>An object placed where this player is, used as the listening/speaking position.</summary>
        GameObject ear;
        /// <summary>This device in the channel; its speech detection drives <see cref="IsSpeaking"/>.</summary>
        VivoxParticipant self;
        /// <summary>Players this device chose not to hear (by voice id).</summary>
        readonly HashSet<string> mutedPlayers = new HashSet<string>();
        /// <summary>Players the game rule keeps this device from hearing right now (see <see cref="VoiceRule.CanHear"/>).</summary>
        readonly HashSet<string> unheardPlayers = new HashSet<string>();

        public bool MicOn { get; set; }
        public string VoicePlayerId => loggedIn ? AuthenticationService.Instance.PlayerId : null;
        public bool IsSpeaking { get; private set; }

        public async Task JoinRoomAsync(string channelName)
        {
            try
            {
                await EnsureLoggedInAsync();
                RequestMicPermission();
                channel = channelName;
                VivoxService.Instance.ChannelJoined += OnChannelJoined;
                VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
                await VivoxService.Instance.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, ChannelProperties);
                // LeaveRoomAsync ran while joining: it skipped the channel we weren't in yet, so leave it now
                if (channel != channelName) { await VivoxService.Instance.LeaveChannelAsync(channelName); return; }
                VivoxService.Instance.MuteInputDevice();   // Tick() unmutes once allowed to speak
            }
            catch (Exception error)
            {
                Metrics.Error(error, "voice");
                if (channel != null)
                {
                    VivoxService.Instance.ChannelJoined -= OnChannelJoined;
                    VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
                    VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
                    channel = null;
                }
            }
        }

        public async Task LeaveRoomAsync()
        {
            var leaving = channel; channel = null; joined = false; speaking = false; whispering = false; IsSpeaking = false;
            DetachSelf();
            VivoxService.Instance.ChannelJoined -= OnChannelJoined;
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            // still joining (or the join failed): nothing to leave yet, JoinRoomAsync leaves once the join lands
            if (leaving == null || !VivoxService.Instance.ActiveChannels.ContainsKey(leaving)) return;
            try { await VivoxService.Instance.LeaveChannelAsync(leaving); }
            catch (Exception error) { Metrics.Error(error, "voice"); }
        }

        public void Tick(bool speak, float x, float y, bool hasPosition, bool whisper)
        {
            if (!joined) return;
            try
            {
                if (speak != speaking) { speaking = speak; if (speak) VivoxService.Instance.UnmuteInputDevice(); else VivoxService.Instance.MuteInputDevice(); }
                if (whisper != whispering) { whispering = whisper; VivoxService.Instance.SetInputDeviceVolume(whisper ? -50 : 0); }
                if (hasPosition) VivoxService.Instance.Set3DPosition(Ear(x, y), channel);
            }
            catch (Exception error) { Metrics.Error(error, "voice"); }
        }

        void OnChannelJoined(string name) { if (name == channel) joined = true; }

        public bool IsPlayerMuted(string voicePlayerId) => voicePlayerId != null && mutedPlayers.Contains(voicePlayerId);

        public void SetUnheard(HashSet<string> voicePlayerIds)
        {
            if (unheardPlayers.SetEquals(voicePlayerIds)) return;
            unheardPlayers.Clear(); unheardPlayers.UnionWith(voicePlayerIds);
            if (channel == null) return;
            try
            {
                if (!VivoxService.Instance.ActiveChannels.TryGetValue(channel, out var participants)) return;
                foreach (var participant in participants) ApplyMute(participant);
            }
            catch (Exception error) { Metrics.Error(error, "voice"); }
        }

        public void SetPlayerMuted(string voicePlayerId, bool muted)
        {
            if (string.IsNullOrEmpty(voicePlayerId)) return;
            if (muted) mutedPlayers.Add(voicePlayerId); else mutedPlayers.Remove(voicePlayerId);
            if (channel == null) return;
            try
            {
                if (!VivoxService.Instance.ActiveChannels.TryGetValue(channel, out var participants)) return;
                foreach (var participant in participants)
                    if (participant.PlayerId == voicePlayerId) ApplyMute(participant);
            }
            catch (Exception error) { Metrics.Error(error, "voice"); }
        }

        void ApplyMute(VivoxParticipant participant)
        {
            if (participant.IsSelf) return;
            if (Silenced(participant.PlayerId)) participant.MutePlayerLocally(); else participant.UnmutePlayerLocally();
        }

        bool Silenced(string voicePlayerId) => mutedPlayers.Contains(voicePlayerId) || unheardPlayers.Contains(voicePlayerId);

        void OnParticipantAdded(VivoxParticipant participant)
        {
            if (participant.ChannelName != channel) return;
            if (!participant.IsSelf)
            {
                // someone muted earlier (or caught) who joins (again) stays muted
                if (Silenced(participant.PlayerId)) ApplyMute(participant);
                return;
            }
            DetachSelf();
            self = participant; self.ParticipantSpeechDetected += OnSelfSpeechDetected; IsSpeaking = self.SpeechDetected;
        }

        void OnParticipantRemoved(VivoxParticipant participant) { if (participant == self) DetachSelf(); }

        void OnSelfSpeechDetected() { if (self != null) IsSpeaking = self.SpeechDetected; }

        void DetachSelf()
        {
            if (self != null) { self.ParticipantSpeechDetected -= OnSelfSpeechDetected; self = null; }
            IsSpeaking = false;
        }

        async Task EnsureLoggedInAsync()
        {
            if (loggedIn) return;
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            if (!VivoxService.Instance.IsLoggedIn)
            {
                await VivoxService.Instance.InitializeAsync();
                await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = AuthenticationService.Instance.PlayerId });
            }
            loggedIn = true;
        }

        static void RequestMicPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone); }
            catch (Exception error) { Metrics.Error(error, "voice"); }
#endif
        }

        GameObject Ear(float x, float y)
        {
            if (ear == null) { ear = new GameObject("VoiceEar"); UnityEngine.Object.DontDestroyOnLoad(ear); }
            ear.transform.position = Art.ToWorld(x, y);
            return ear;
        }
    }
}
