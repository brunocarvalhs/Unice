using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>
    /// Every screen and the in-game HUD, built in code with UI Toolkit (styles in Resources/UI/unice.uss).
    /// Same screens and texts as the web version, plus touch controls.
    /// </summary>
    public sealed partial class Ui
    {
        public static readonly Color Rose = new Color32(239, 115, 148, 255), Candle = new Color32(240, 176, 87, 255), Muted = new Color32(173, 154, 140, 255),
            Blood = new Color32(226, 74, 82, 255), Moss = new Color32(143, 203, 156, 255), Ink = new Color32(241, 231, 216, 255);

        readonly App app;
        Game G => app.G;
        readonly VisualElement root, safe, labels, hud, touch;
        readonly bool mobile = Application.isMobilePlatform;
        string screen = "";
        float hudT;
        Rect lastSafe;

        public Ui(App app)
        {
            this.app = app;
            // The panel asset made by Unice > Configurar projeto keeps the UI shaders in the build; the fallback is for the editor.
            var ps = Resources.Load<PanelSettings>("UI/UnicePanel");
            if (ps == null) ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize; ps.referenceResolution = new Vector2Int(1280, 720);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; ps.match = 1;
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/UniceTheme");
            var go = new GameObject("UI"); go.SetActive(false); go.transform.SetParent(app.transform, false);
            var doc = go.AddComponent<UIDocument>(); doc.panelSettings = ps; go.SetActive(true);
            root = doc.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/unice"));
            root.AddToClassList("root"); root.pickingMode = PickingMode.Ignore;

            vignette = Ig(E(root, "layer"));
            vignette.style.backgroundImage = new StyleBackground(Art.Centered("vignette", 1));
            slats = Ig(E(root, "layer"));
            for (int i = 0; i < 60; i++) Ig(E(slats, "slat")).style.top = i * 26;
            flash = Ig(E(root, "layer")); Show(flash, false);
            labels = Ig(E(root, "layer"));
            cuesEl = Ig(E(root, "layer")); cuesEl.generateVisualContent += DrawCues;
            safe = Ig(E(root, "layer"));
            hud = Ig(E(safe, "layer")); BuildHud();
            touch = Ig(E(safe, "layer")); BuildTouch();
            BuildMessages();
            BuildHome(); BuildWait(); BuildResult(); BuildPause();
            RefreshToggleBtns();
        }

        // ---------- tiny builders ----------
        static T Add<T>(VisualElement p, T el, string[] cls) where T : VisualElement { foreach (var c in cls) el.AddToClassList(c); p.Add(el); return el; }
        static VisualElement E(VisualElement p, params string[] cls) => Add(p, new VisualElement(), cls);
        static Label L(VisualElement p, string text, params string[] cls) => Add(p, new Label(text), cls);
        static Button B(VisualElement p, string text, Action click, params string[] cls) => Add(p, new Button(click) { text = text }, cls);
        static VisualElement Ig(VisualElement e) { e.pickingMode = PickingMode.Ignore; return e; }
        static void Show(VisualElement e, bool on) { var d = on ? DisplayStyle.Flex : DisplayStyle.None; if (e.style.display != d) e.style.display = d; }
        static void Txt(Label l, string t) { if (l.text != t) l.text = t; }
        static TextField Field(VisualElement p, string value, int max, params string[] cls)
        {
            var f = new TextField { value = value, maxLength = max }; f.AddToClassList("field"); foreach (var c in cls) f.AddToClassList(c); p.Add(f); return f;
        }
        static VisualElement Sheet(VisualElement p, out VisualElement overlay)
        {
            overlay = E(p, "overlay");
            var sc = new ScrollView(ScrollViewMode.Vertical); sc.AddToClassList("sheet-scroll"); overlay.Add(sc);
            return E(sc, "sheet");
        }

        // ---------- localized text built once (menus, rules, static labels) ----------
        // Most text is re-applied every tick from Render*/Update* via Loc.T(...) and already follows a language
        // change automatically. These two are for text built ONCE in a Build* method: they remember the key so
        // a language switch can re-apply it later via Relocalize().
        readonly List<Action> relocalize = new List<Action>();
        Label LL(VisualElement p, string key, params string[] cls) { var l = L(p, Loc.T(key), cls); relocalize.Add(() => l.text = Loc.T(key)); return l; }
        Button BB(VisualElement p, string key, Action click, params string[] cls) { var b = B(p, Loc.T(key), click, cls); relocalize.Add(() => b.text = Loc.T(key)); return b; }
        void Relocalize() { foreach (var a in relocalize) a(); RefreshToggleBtns(); wSig = null; plSig = null; }

        public bool Typing
        {
            get
            {
                var f = root.panel?.focusController?.focusedElement as VisualElement;
                return f != null && (f is TextField || f.GetFirstAncestorOfType<TextField>() != null);
            }
        }

        // ---------- per step (20/s): screens, HUD, prompt ----------
        public void Step(float dt)
        {
            SyncScreens();
            hudT -= dt; if (hudT <= 0 && screen == "game") { hudT = .2f; UpdateHud(); mini.MarkDirtyRepaint(); }
            UpdatePrompt();
        }

        void SyncScreens()
        {
            var ph = G.ph;
            string want = ph == Phase.Home ? "home" : ph == Phase.Lobby ? "wait" : ph == Phase.Result ? "result" : ph == Phase.Final ? "final" : "game";
            if (want != screen)
            {
                string was = screen; screen = want;
                Show(home, want == "home"); Show(wait, want == "wait"); Show(resultOv, want == "result" || want == "final");
                Show(hud, want == "game"); Show(touch, want == "game" && mobile);
                if (want == "home") Page(app.netErr && app.netText.Length > 0 ? "online" : "main");
                if (want == "wait") { if (was == "final") app.rdy = false; wSig = null; }
                if (want == "result" || want == "final") RenderResult(want == "final");
                if (want == "game") hudT = 0;
                if (want != "game") { ShowPause(false, false); app.paused = false; SneakOn = false; actS.EnableInClassList("act-on", false); }
                plSig = null;
            }
            if (want == "home")
            {
                bool ok = app.NetAvailable && !app.busy;
                createBtn.SetEnabled(ok); joinBtn.SetEnabled(ok); localBtn.SetEnabled(!app.busy); onlineBtn.SetEnabled(!app.busy);
                Txt(netLbl, app.netText.Length > 0 ? app.netText : Loc.T(app.NetAvailable ? "menu.net_ready" : "menu.net_unavailable"));
                netLbl.EnableInClassList("net-err", app.netErr && app.netText.Length > 0);
            }
            if (want == "wait") RenderWait();
            if (want == "result" && nextIn != null) Txt(nextIn, Mathf.Max(0, Mathf.CeilToInt(G.tl)).ToString());
            if (want == "final" && againBtn != null) { Show(againBtn, G.isHost); Show(waitHost, !G.isHost); }
        }

        void UpdatePrompt()
        {
            var p = app.me; var parts = new List<string>(); string eLbl = null; bool qShow = false, qOk = false, rShow = false, rOk = false;
            string K(string key) => mobile ? "" : $"<b>{key}</b> ";
            bool active = p != null && G.Live && !p.outOfRound;
            if (active)
            {
                if (p.IsUnice) { if (G.ph == Phase.Hunt && p.stun <= 0) { var s = Game.NearSpotFurn(p); if (s != null) { parts.Add($"{K("E")}{Loc.T("prompt.look", Loc.Spot(s))}"); eLbl = "prompt.look_short"; } } }
                else
                {
                    if (p.hidden != null) { parts.Add($"{K("E")}{Loc.T("prompt.exit_hide")}"); eLbl = "prompt.exit_short"; }
                    else
                    {
                        var s = Game.NearSpotFurn(p);
                        if (s != null) { if (s.occ != null) parts.Add(Loc.T("prompt.occupied", s.occ.name, Loc.Spot(s))); else { parts.Add($"{K("E")}{Loc.T("prompt.hide", Loc.Spot(s))}"); eLbl = "prompt.hide_short"; } }
                    }
                    if (G.doll.holder == p)
                    {
                        qShow = true; var m = G.PassTarget(p);
                        if (m != null) { qOk = p.passCd <= 0; parts.Add(p.passCd > 0 ? Loc.T("prompt.pass_in", Mathf.CeilToInt(p.passCd)) : $"{K("Q")}{Loc.T("prompt.pass_to", m.name)}"); }
                    }
                    rShow = true; rOk = p.laughCd <= 0;
                    parts.Add(p.laughCd > 0 ? Loc.T("prompt.laugh_in", Mathf.CeilToInt(p.laughCd)) : $"{K("R")}{Loc.T("prompt.laugh")}");
                }
            }
            string txt = string.Join("    ", parts);
            if (prompt.text != txt) { prompt.text = txt; Show(promptBox, txt.Length > 0); }
            if (!mobile) return;
            Show(actE, active); Show(actQ, qShow); Show(actR, rShow); Show(actS, active && !p.IsUnice);
            if (active)
            {
                SetActEIcon(p.IsUnice ? "eye" : "door");
                actE.style.opacity = eLbl != null ? 1 : .45f; actQ.style.opacity = qOk ? 1 : .45f; actR.style.opacity = rOk ? 1 : .45f;
            }
        }

        // ---------- per frame: world text, cues, overlays ----------
        public void Frame(float dt, Camera cam)
        {
            var panel = root.panel; if (panel == null) return;
            ApplySafeArea(); LiftForKeyboard();
            float H = root.layout.height, W = root.layout.width; if (float.IsNaN(H) || H < 1) return;
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
                tag.style.color = Un ? Rose : e == me ? Candle : Ink; tag.style.opacity = a;
                tag.style.fontSize = 12 * pxPerHouse;
                var pp = ToPanel(e.x, e.y + (Un ? -66 : -54)); tag.style.left = pp.x; tag.style.top = pp.y;
            }

            // floating texts
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i]; f.t += dt;
                if (f.t >= f.life) { f.l.RemoveFromHierarchy(); fx.RemoveAt(i); continue; }
                fx[i] = f;
                bool vis = inGame && (f.global || app.Seen(f.x, f.y + 24));
                Show(f.l, vis); if (!vis) continue;
                float k = f.t / f.life, sc = k < .15f ? .6f + k / .15f * .5f : 1.1f - .1f * Mathf.Min(1, (k - .15f) / .2f);
                var pp = ToPanel(f.x, f.y - k * 24); f.l.style.left = pp.x; f.l.style.top = pp.y;
                f.l.style.fontSize = f.size * pxPerHouse; f.l.style.scale = new Scale(new Vector3(sc, sc, 1));
                f.l.style.opacity = 1 - Mathf.Max(0, (k - .6f) / .4f);
            }

            // sound cues around the player
            for (int i = cues.Count - 1; i >= 0; i--) { var q = cues[i]; q.t += dt; if (q.t >= q.life) cues.RemoveAt(i); else cues[i] = q; }
            if (app.hasView) { cueCenter = ToPanel(app.view.x, app.view.y - 18); cueR = Mathf.Min(W, H) * .44f; }
            if (cues.Count > 0 || cuesDrawn) { cuesEl.MarkDirtyRepaint(); cuesDrawn = cues.Count > 0; }

            // vignette: dim attract mode, red heartbeat when the Unice is near
            app.beat = Mathf.Max(0, app.beat - dt * 2.5f);
            if (!inGame) { vignette.style.unityBackgroundImageTintColor = new Color(0, 0, 0, .88f); vignette.style.backgroundColor = new Color(0, 0, 0, .3f); Show(vignette, true); }
            else if (app.heartI > 0 && live) { vignette.style.unityBackgroundImageTintColor = new Color(.886f, .29f, .32f, .18f + .4f * app.heartI * app.beat); vignette.style.backgroundColor = Color.clear; Show(vignette, true); }
            else Show(vignette, false);
            Show(slats, live && me != null && me.hidden != null);

            if (flashT > 0)
            {
                flashT -= dt; float fk = Mathf.Max(0, flashT) / flashDur;
                var c = flashColor; c.a *= fk; flash.style.backgroundColor = c; Show(flash, true);
            }
            else Show(flash, false);

            if (screen == "home" && page == "kid")
            {
                float t = Time.unscaledTime; var d = PrevDirs[(int)(t / 1.4f) % 4];
                preview.Set(app.kit, d.y < -.6f, 1 + Mathf.FloorToInt(t * 13 / (Mathf.PI * 2) * 6) % 6, d.x < -.3f);
            }

            // hiding phase: the Unice waits with eyes closed, the others see a countdown
            bool hidePh = G.ph == Phase.Hide; int n = Mathf.CeilToInt(G.tl);
            Show(blackout, hidePh && me != null && me.IsUnice);
            Show(pill, hidePh && (me == null || !me.IsUnice));
            if (hidePh) { Txt(count, n.ToString()); Txt(pillTxt, Loc.T("game.unice_enters_in", n)); }
        }
        bool cuesDrawn;
        static readonly Vector2[] PrevDirs = { new Vector2(.01f, 1), new Vector2(1, .2f), new Vector2(.01f, -1), new Vector2(-1, .2f) };

        /// <summary>Mobile: the on-screen keyboard covers the lower half of the menu, so slide it up until the field being typed in is visible.</summary>
        void LiftForKeyboard()
        {
            float lift = 0;
            if (Typing && TouchScreenKeyboard.visible && root.panel.focusController.focusedElement is VisualElement f && Screen.height > 0)
            {
                float k = root.layout.height / Screen.height, kb = TouchScreenKeyboard.area.height;
                if (kb < 1) kb = Screen.height * .5f;   // some Android keyboards don't report their size
                float kbTop = root.layout.height - kb * k - 16, fieldBottom = f.worldBound.yMax - home.resolvedStyle.translate.y;
                lift = Mathf.Max(0, fieldBottom - kbTop);
            }
            if (home.resolvedStyle.translate.y != -lift) home.style.translate = new Translate(0, -lift);
        }

        void ApplySafeArea()
        {
            var sa = Screen.safeArea; if (sa == lastSafe || Screen.height < 1) return; lastSafe = sa;
            float k = root.layout.height > 0 ? root.layout.height / Screen.height : 720f / Screen.height;
            safe.style.left = sa.xMin * k; safe.style.right = (Screen.width - sa.xMax) * k;
            safe.style.top = (Screen.height - sa.yMax) * k; safe.style.bottom = sa.yMin * k;
            foreach (var el in new[] { home, wait, resultOv }) { el.style.left = safe.style.left; el.style.right = safe.style.right; el.style.top = safe.style.top; el.style.bottom = safe.style.bottom; }
        }
    }
}
