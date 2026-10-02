using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unice
{
    public sealed partial class Ui
    {
        // ---------- messages, cues and world text ----------
        Label banner, caption, pillTxt, count, countSub, prompt; VisualElement toasts, pill, captionBox, blackout, promptBox, vignette, slats, flash, cuesEl;
        float bannerT, bannerDur, captionT, flashT, flashDur; Color flashColor;
        readonly List<(Label l, float until)> toastList = new List<(Label, float)>();

        void BuildMessages()
        {
            blackout = Ig(E(safe, "layer", "blackout"));
            count = Ig(L(blackout, "", "count")) as Label; countSub = Ig(LL(blackout, "game.countdown_sub", "count-sub")) as Label;
            pill = Ig(E(safe, "pill")); pillTxt = Ig(L(pill, "", "pill-in")) as Label;
            captionBox = Ig(E(safe, "caption")); caption = Ig(L(captionBox, "", "caption-in")) as Label;
            promptBox = Ig(E(safe, "prompt")); prompt = Ig(L(promptBox, "", "prompt-in")) as Label;
            if (mobile) promptBox.style.bottom = 150;
            toasts = Ig(E(safe, "toasts"));
            banner = Ig(L(safe, "", "banner")) as Label;
            Show(banner, false); Show(blackout, false); Show(pill, false); Show(captionBox, false); Show(promptBox, false);
        }

        public void Banner(string text, string kind, float dur)
        {
            banner.text = text; foreach (var k in new[] { "info", "rose", "blood", "moss", "candle" }) banner.EnableInClassList("banner-" + k, k == kind);
            bannerT = 0; bannerDur = dur; Show(banner, true);
        }

        public void Toast(string text)
        {
            var l = Ig(L(toasts, text, "toast")) as Label; l.RemoveFromHierarchy(); toasts.Insert(0, l);
            toastList.Add((l, Time.unscaledTime + 3.4f));
            while (toasts.childCount > 3) { var last = toasts[toasts.childCount - 1]; last.RemoveFromHierarchy(); }
        }

        public void Caption(string text) { caption.text = text; captionT = 2.4f; }

        /// <summary>A quick full-screen color flash (the catch moment) — no new art, just an alpha-decaying overlay.</summary>
        public void Flash(Color color, float dur) { flashColor = color; flashDur = Mathf.Max(.01f, dur); flashT = flashDur; }

        struct CueArc { public float ang, g, t, life; public Color col; }
        readonly List<CueArc> cues = new List<CueArc>();
        public void Cue(float ang, float g, Color col) { cues.Add(new CueArc { ang = ang, g = g, life = 1.3f, col = col }); }

        Vector2 cueCenter; float cueR;
        void DrawCues(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D; p.lineCap = LineCap.Round;
            foreach (var q in cues)
            {
                float k = q.t / q.life; var c = q.col; c.a = (1 - k) * Mathf.Clamp(.35f + q.g * 1.5f, .35f, 1);
                p.strokeColor = c; p.lineWidth = 3 + q.g * 9;
                p.BeginPath(); p.Arc(cueCenter, cueR + k * 10, Angle.Radians(q.ang - .28f), Angle.Radians(q.ang + .28f)); p.Stroke();
            }
        }

        struct FxText { public float x, y, size, life, t; public string text; public Color color; public bool global; public Label l; }
        readonly List<FxText> fx = new List<FxText>();
        public void Fx(float x, float y, string text, Color color, float size, float life, bool global = false)
        {
            var l = Ig(L(labels, text, "fxtext")) as Label; l.style.color = color;
            fx.Add(new FxText { x = x, y = y, text = text, color = color, size = size, life = life, global = global, l = l });
        }

        readonly List<Label> nameTags = new List<Label>();
    }
}
