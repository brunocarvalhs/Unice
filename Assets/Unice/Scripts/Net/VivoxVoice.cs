using System;
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
        static void Register() => VoiceRegistry.Factory = () => new VivoxVoice();

        // ponytail: fixed falloff tuned to the house's scale (world units = house px / 100, see Art.PxPerUnit). Expose as settings if it ever needs per-room tuning.
        static readonly Channel3DProperties Props = new Channel3DProperties(
            audibleDistance: 8, conversationalDistance: 2, audioFadeIntensityByDistanceaudio: 1f, audioFadeModel: AudioFadeModel.InverseByDistance);

        string channel;
        bool loggedIn, joined, speaking, whispering;
        GameObject ear;
        VivoxParticipant self;

        public bool MicOn { get; set; }
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
                await VivoxService.Instance.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, Props);
                VivoxService.Instance.MuteInputDevice();   // Tick() unmutes once allowed to speak
            }
            catch (Exception x)
            {
                Metrics.Error(x, "voice");
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
            var ch = channel; channel = null; joined = false; speaking = false; whispering = false; IsSpeaking = false;
            DetachSelf();
            VivoxService.Instance.ChannelJoined -= OnChannelJoined;
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            if (ch == null) return;
            try { await VivoxService.Instance.LeaveChannelAsync(ch); }
            catch (Exception x) { Metrics.Error(x, "voice"); }
        }

        public void Tick(bool speak, float x, float y, bool hasPos, bool whisper)
        {
            if (!joined) return;
            try
            {
                if (speak != speaking) { speaking = speak; if (speak) VivoxService.Instance.UnmuteInputDevice(); else VivoxService.Instance.MuteInputDevice(); }
                if (whisper != whispering) { whispering = whisper; VivoxService.Instance.SetInputDeviceVolume(whisper ? -50 : 0); }
                if (hasPos) VivoxService.Instance.Set3DPosition(Ear(x, y), channel);
            }
            catch (Exception ex) { Metrics.Error(ex, "voice"); }
        }

        void OnChannelJoined(string name) { if (name == channel) joined = true; }

        void OnParticipantAdded(VivoxParticipant p)
        {
            if (!p.IsSelf || p.ChannelName != channel) return;
            DetachSelf();
            self = p; self.ParticipantSpeechDetected += OnSelfSpeechDetected; IsSpeaking = self.SpeechDetected;
        }

        void OnParticipantRemoved(VivoxParticipant p) { if (p == self) DetachSelf(); }

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
            catch (Exception x) { Metrics.Error(x, "voice"); }
#endif
        }

        GameObject Ear(float x, float y)
        {
            if (ear == null) { ear = new GameObject("VoiceEar"); UnityEngine.Object.DontDestroyOnLoad(ear); }
            ear.transform.position = Art.W(x, y);
            return ear;
        }
    }
}
