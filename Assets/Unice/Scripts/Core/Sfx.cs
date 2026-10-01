using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// The same synthesized sounds as the web version (no audio files), played with distance, left/right pan
    /// and a muffled filter when there is a wall in between.
    /// </summary>
    public sealed class Sfx
    {
        const int Rate = 44100;
        enum Wave { Square, Triangle, Saw }
        struct Tone { public float f, d, v, f2, delay; public Wave w; }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<(AudioSource src, AudioLowPassFilter lp)> pool = new List<(AudioSource, AudioLowPassFilter)>();
        int next;
        public bool On = true;

        public Sfx(Transform parent)
        {
            for (int i = 0; i < 12; i++)
            {
                var go = new GameObject("sfx" + i); go.transform.SetParent(parent, false);
                var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0;
                var lp = go.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = 700; lp.enabled = false;
                pool.Add((s, lp));
            }
            Make("giggle", Seq(new[] { 1250f, 1480, 1180, 1420, 1060, 1300 }, .085f, Wave.Triangle, .07f, .1f, 1.22f));
            Make("heart", new[] { T(95, .13f, Wave.Triangle, .22f, 55), T(85, .12f, Wave.Triangle, .16f, 50, .17f) });
            Make("cane", new[] { T(900, .03f, Wave.Square, .05f, 500), T(240, .07f, Wave.Triangle, .26f, 140) });   // the Unice's cane: toc
            Make("creak", new[] { T(220, .32f, Wave.Saw, .03f, 120) });
            Make("sting", new[] { T(900, .5f, Wave.Saw, .05f, 90) });
            Make("swish", new[] { T(500, .16f, Wave.Triangle, .06f, 1500) });
            Make("step", new[] { T(160, .045f, Wave.Square, .03f) });
            Make("tick", new[] { T(1500, .03f, Wave.Square, .02f) });
            Make("clock", new[] { T(2200, .02f, Wave.Square, .025f) });
            Make("boom", new[] { T(120, .8f, Wave.Saw, .06f, 38) });
            Make("bad", new[] { T(230, .15f, Wave.Square, .035f, 150) });
            Make("win", Seq(new[] { 523f, 659, 784, 1046 }, .22f, Wave.Triangle, .06f, .14f, 1));
            Make("lose", Seq(new[] { 392f, 330, 262, 196 }, .3f, Wave.Triangle, .06f, .22f, 1));
        }

        static Tone T(float f, float d, Wave w, float v, float f2 = 0, float delay = 0) => new Tone { f = f, d = d, w = w, v = v, f2 = f2, delay = delay };
        static Tone[] Seq(float[] fs, float d, Wave w, float v, float gap, float bend)
        {
            var r = new Tone[fs.Length];
            for (int i = 0; i < fs.Length; i++) r[i] = T(fs[i], d, w, v, bend != 1 ? fs[i] * bend : 0, i * gap);
            return r;
        }

        void Make(string name, Tone[] tones)
        {
            float len = 0; foreach (var t in tones) len = Mathf.Max(len, t.delay + t.d + .03f);
            int n = Mathf.CeilToInt(len * Rate); var data = new float[n];
            foreach (var t in tones)
            {
                int s0 = (int)(t.delay * Rate), sn = (int)(t.d * Rate); double ph = 0;
                for (int i = 0; i < sn && s0 + i < n; i++)
                {
                    float k = (float)i / sn;
                    float f = t.f2 > 0 ? t.f * Mathf.Pow(t.f2 / t.f, k) : t.f;
                    float g = t.v * Mathf.Pow(.0001f / t.v, k);
                    ph += f / Rate; float p = (float)(ph - Math.Floor(ph));
                    float w = t.w == Wave.Square ? (p < .5f ? 1 : -1) : t.w == Wave.Triangle ? (p < .5f ? 4 * p - 1 : 3 - 4 * p) : 2 * p - 1;
                    data[s0 + i] += w * g * 3.2f;
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1, 1);
            var clip = AudioClip.Create(name, n, 1, Rate, false); clip.SetData(data, 0);
            clips[name] = clip;
        }

        public void Play(string name, float gain = 1, float pan = 0, bool muffle = false)
        {
            if (!On || gain <= 0 || !clips.TryGetValue(name, out var clip)) return;
            var (src, lp) = pool[next]; next = (next + 1) % pool.Count;
            src.Stop(); src.clip = clip; src.volume = Mathf.Clamp01(gain); src.panStereo = Mathf.Clamp(pan, -1, 1); lp.enabled = muffle;
            src.Play();
        }
    }
}
