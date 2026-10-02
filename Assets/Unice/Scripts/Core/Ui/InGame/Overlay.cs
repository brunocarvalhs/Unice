using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// Everything drawn over the world: banners, toasts, captions, the action prompt, sound cues around the player,
    /// floating texts and name tags, the hiding-phase countdown, and the vignette / slats / flash layers.
    /// </summary>
    sealed class Overlay : UiPart
    {
        readonly VisualElement vignette, slats, flash, labels, cuesEl;
        Label banner, caption, pillTxt, count, prompt; VisualElement toasts, pill, captionBox, blackout, promptBox;
        float bannerT, bannerDur, captionT, flashT, flashDur; Color flashColor;
        readonly List<(Label l, float until)> toastList = new List<(Label, float)>();
        readonly List<Label> nameTags = new List<Label>();

        /// <summary>Builds the world layers; <see cref="BuildMessages"/> adds the message layers once the HUD and touch layers exist.</summary>
        public Overlay(Ui ui) : base(ui)
        {
            var root = ui.root;
            vignette = Ig(E(root, "layer"));
            vignette.style.backgroundImage = new StyleBackground(Art.Centered("vignette", 1));
            slats = Ig(E(root, "layer"));
            for (int i = 0; i < 60; i++) Ig(E(slats, "slat")).style.top = i * 26;
            flash = Ig(E(root, "layer")); Show(flash, false);
            labels = Ig(E(root, "layer"));
            cuesEl = Ig(E(root, "layer")); cuesEl.generateVisualContent += DrawCues;
        }

        public void BuildMessages()
        {
            var safe = ui.safe;
            blackout = Ig(E(safe, "layer", "blackout"));
            count = Ig(L(blackout, "", "count")) as Label; Ig(LL(blackout, "game.countdown_sub", "count-sub"));
            pill = Ig(E(safe, "pill")); pillTxt = Ig(L(pill, "", "pill-in")) as Label;
            captionBox = Ig(E(safe, "caption")); caption = Ig(L(captionBox, "", "caption-in")) as Label;
            promptBox = Ig(E(safe, "prompt")); prompt = Ig(L(promptBox, "", "prompt-in")) as Label;
            if (ui.mobile) promptBox.style.bottom = 150;
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

        /// <summary>The key hints line ("E hide under the bed    Q pass to Bia").</summary>
        public void SetPrompt(string txt) { if (prompt.text != txt) { prompt.text = txt; Show(promptBox, txt.Length > 0); } }

        /// <summary>A quick full-screen color flash (the catch moment) — no new art, just an alpha-decaying overlay.</summary>
        public void Flash(Color color, float dur) { flashColor = color; flashDur = Mathf.Max(.01f, dur); flashT = flashDur; }

        struct CueArc { public float ang, g, t, life; public Color col; }
        readonly List<CueArc> cues = new List<CueArc>();
        public void Cue(float ang, float g, Color col) { cues.Add(new CueArc { ang = ang, g = g, life = 1.3f, col = col }); }

        Vector2 cueCenter; float cueR; bool cuesDrawn;
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

        /// <summary>Per frame. Call only once the panel has a layout.</summary>
        public void Frame(float dt, Camera cam)
        {
            var panel = ui.root.panel;
            float H = ui.root.layout.height, W = ui.root.layout.width;
            float pxPerHouse = H / (cam.orthographicSize * 2 * Art.PxPerUnit);
            Vector2 ToPanel(float x, float y) => RuntimePanelUtils.CameraTransformWorldToPanel(panel, Art.W(x, y), cam);
            var me = app.me; bool live = G.Live, inGame = live || G.ph == Phase.Result;

            // banner pop
            if (bannerDur > 0)
            {
                bannerT += dt; float k = bannerT / bannerDur;
                if (k >= 1) { bannerDur = 0; Show(banner, false); }
                else { banner.style.opacity = k < .12f ? k / .12f : k > .75f ? (1 - k) / .25f : 1; float sc = k < .12f ? .85f + k / .12f * .15f : 1; banner.style.scale = new Scale(new Vector3(sc, sc, 1)); }
            }
            for (int i = toastList.Count - 1; i >= 0; i--) if (Time.unscaledTime > toastList[i].until) { toastList[i].l.RemoveFromHierarchy(); toastList.RemoveAt(i); }
            if (captionT > 0) captionT -= dt;
            Show(captionBox, captionT > 0 && me != null && me.hidden != null);

            // name tags
            var shown = app.world.Shown;
            for (int i = 0; i < G.ents.Count || i < nameTags.Count; i++)
            {
                if (i >= nameTags.Count) nameTags.Add(Ig(L(labels, "", "wlabel")) as Label);
                var tag = nameTags[i];
                float a = inGame && i < G.ents.Count && i < shown.Count ? shown[i] : 0;
                if (a <= 0) { Show(tag, false); continue; }
                var e = G.ents[i]; bool Un = e.IsUnice;
                Show(tag, true);
                Txt(tag, Un ? (e == me ? Loc.T("tag.unice_you") : Loc.T("tag.unice_named", e.name)) : e.name);
                tag.style.color = Un ? Ui.Rose : e == me ? Ui.Candle : Ui.Ink; tag.style.opacity = a;
                tag.style.fontSize = 12 * pxPerHouse;
                var pp = ToPanel(e.x, e.y + (Un ? -66 : -54)); tag.style.left = pp.x; tag.style.top = pp.y;
            }

            // floating texts
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i]; f.t += dt;
                if (f.t >= f.life) { f.l.RemoveFromHierarchy(); fx.RemoveAt(i); continue; }
                fx[i] = f;
                bool vis = inGame && (f.global || app.pov.Seen(f.x, f.y + 24));
                Show(f.l, vis); if (!vis) continue;
                float k = f.t / f.life, sc = k < .15f ? .6f + k / .15f * .5f : 1.1f - .1f * Mathf.Min(1, (k - .15f) / .2f);
                var pp = ToPanel(f.x, f.y - k * 24); f.l.style.left = pp.x; f.l.style.top = pp.y;
                f.l.style.fontSize = f.size * pxPerHouse; f.l.style.scale = new Scale(new Vector3(sc, sc, 1));
                f.l.style.opacity = 1 - Mathf.Max(0, (k - .6f) / .4f);
            }

            // sound cues around the player
            for (int i = cues.Count - 1; i >= 0; i--) { var q = cues[i]; q.t += dt; if (q.t >= q.life) cues.RemoveAt(i); else cues[i] = q; }
            if (app.pov.hasView) { cueCenter = ToPanel(app.pov.view.x, app.pov.view.y - 18); cueR = Mathf.Min(W, H) * .44f; }
            if (cues.Count > 0 || cuesDrawn) { cuesEl.MarkDirtyRepaint(); cuesDrawn = cues.Count > 0; }

            // vignette: dim attract mode, red heartbeat when the Unice is near
            app.sound.beat = Mathf.Max(0, app.sound.beat - dt * 2.5f);
            if (!inGame) { vignette.style.unityBackgroundImageTintColor = new Color(0, 0, 0, .88f); vignette.style.backgroundColor = new Color(0, 0, 0, .3f); Show(vignette, true); }
            else if (app.sound.heartI > 0 && live) { vignette.style.unityBackgroundImageTintColor = new Color(.886f, .29f, .32f, .18f + .4f * app.sound.heartI * app.sound.beat); vignette.style.backgroundColor = Color.clear; Show(vignette, true); }
            else Show(vignette, false);
            Show(slats, live && me != null && me.hidden != null);

            if (flashT > 0)
            {
                flashT -= dt; float fk = Mathf.Max(0, flashT) / flashDur;
                var c = flashColor; c.a *= fk; flash.style.backgroundColor = c; Show(flash, true);
            }
            else Show(flash, false);

            // hiding phase: the Unice waits with eyes closed, the others see a countdown
            bool hidePh = G.ph == Phase.Hide; int n = Mathf.CeilToInt(G.tl);
            Show(blackout, hidePh && me != null && me.IsUnice);
            Show(pill, hidePh && (me == null || !me.IsUnice));
            if (hidePh) { Txt(count, n.ToString()); Txt(pillTxt, Loc.T("game.unice_enters_in", n)); }
        }
    }
}
