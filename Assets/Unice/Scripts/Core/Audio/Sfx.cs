using System.Collections.Generic;
using UnityEngine;
using static Unice.ToneSynth;

namespace Unice
{
    /// <summary>
    /// The game's sound effects: the same synthesized sounds as the web version (no audio files, see <see cref="ToneSynth"/>),
    /// played with a volume, a left/right pan, and a muffled filter when there is a wall in between. Voice 0 is kept for the
    /// sounds that carry game information (<see cref="IsCritical"/>), so a burst of footsteps never cuts the giggle.
    /// </summary>
    public sealed class Sfx
    {
        /// <summary>How many sounds can play at once; the oldest is cut when a new one starts.</summary>
        const int Voices = 12;
        /// <summary>The voice only <see cref="IsCritical"/> sounds use.</summary>
        const int ReservedVoice = 0;
        /// <summary>PlayerPrefs key of the sound on/off setting (1 = on).</summary>
        const string SoundKey = "unice.sound";
        /// <summary>Low-pass cutoff (Hz) for sounds heard through a wall.</summary>
        const float MuffledCutoff = 700;

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<(AudioSource source, AudioLowPassFilter muffle)> voices = new List<(AudioSource, AudioLowPassFilter)>();
        int nextVoice = ReservedVoice + 1;
        bool enabled = PlayerPrefs.GetInt(SoundKey, 1) != 0;

        /// <summary>The player's sound on/off setting, saved on the device.</summary>
        public bool Enabled
        {
            get => enabled;
            set { enabled = value; PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// Volume multiplier per sound, so the mix is even: the synthesized heart beat is ~3x louder than the giggle, and
        /// footsteps repeat all the time. Sounds not listed play at 1. Tune by ear on a phone speaker and on headphones.
        /// </summary>
        static readonly Dictionary<string, float> Gains = new Dictionary<string, float> { ["heart"] = .35f, ["step"] = .6f };

        /// <summary>The sounds that tell the player what is happening: the giggle, a catch, the Unice coming in.</summary>
        public static bool IsCritical(string name) => name == "giggle" || name == "sting" || name == "boom";

        public static float GainFor(string name) => Gains.TryGetValue(name, out float gain) ? gain : 1;

        /// <summary>
        /// The voice a sound plays on: critical sounds always get the reserved voice; the rest take turns on the others
        /// (<paramref name="nextVoice"/> advances, skipping the reserved one).
        /// </summary>
        public static int PickVoice(bool critical, ref int nextVoice, int voiceCount)
        {
            if (critical) return ReservedVoice;
            int voice = nextVoice;
            nextVoice = voice + 1 < voiceCount ? voice + 1 : ReservedVoice + 1;
            return voice;
        }

        public Sfx(Transform parent)
        {
            for (int i = 0; i < Voices; i++)
            {
                var voiceObject = new GameObject("sfx" + i); voiceObject.transform.SetParent(parent, false);
                var source = voiceObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
                var muffle = voiceObject.AddComponent<AudioLowPassFilter>(); muffle.cutoffFrequency = MuffledCutoff; muffle.enabled = false;
                voices.Add((source, muffle));
            }
            Register("giggle", ToneSequence(new[] { 1250f, 1480, 1180, 1420, 1060, 1300 }, .085f, Wave.Triangle, .07f, .1f, 1.22f));
            Register("heart", MakeTone(95, .13f, Wave.Triangle, .22f, 55), MakeTone(85, .12f, Wave.Triangle, .16f, 50, .17f));
            Register("cane", MakeTone(900, .03f, Wave.Square, .05f, 500), MakeTone(240, .07f, Wave.Triangle, .26f, 140));   // the Unice's cane: toc
            Register("creak", MakeTone(220, .32f, Wave.Saw, .03f, 120));
            Register("sting", MakeTone(900, .5f, Wave.Saw, .05f, 90));
            Register("swish", MakeTone(500, .16f, Wave.Triangle, .06f, 1500));
            Register("step", MakeTone(160, .045f, Wave.Square, .03f));
            Register("tick", MakeTone(1500, .03f, Wave.Square, .02f));
            Register("clock", MakeTone(2200, .02f, Wave.Square, .025f));
            Register("boom", MakeTone(120, .8f, Wave.Saw, .06f, 38));
            Register("bad", MakeTone(230, .15f, Wave.Square, .035f, 150));
            Register("win", ToneSequence(new[] { 523f, 659, 784, 1046 }, .22f, Wave.Triangle, .06f, .14f, 1));
            Register("lose", ToneSequence(new[] { 392f, 330, 262, 196 }, .3f, Wave.Triangle, .06f, .22f, 1));
        }

        void Register(string name, params Tone[] tones) => clips[name] = Synthesize(name, tones);

        /// <param name="volume">0..1.</param>
        /// <param name="pan">-1 (left) .. 1 (right).</param>
        /// <param name="muffled">Heard through a wall.</param>
        public void Play(string name, float volume = 1, float pan = 0, bool muffled = false)
        {
            if (!Enabled || volume <= 0 || !clips.TryGetValue(name, out var clip)) return;
            var (source, muffle) = voices[PickVoice(IsCritical(name), ref nextVoice, voices.Count)];
            source.Stop(); source.clip = clip;
            source.volume = Mathf.Clamp01(volume * GainFor(name)); source.panStereo = Mathf.Clamp(pan, -1, 1); muffle.enabled = muffled;
            source.Play();
        }
    }
}
