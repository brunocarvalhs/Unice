using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// Every screen and the in-game HUD, built in code with UI Toolkit (styles in Resources/UI/unice.uss).
    /// Same screens and texts as the web version, plus touch controls.
    /// This class only assembles the parts, switches screens and owns what several screens share
    /// (sound/mic/whisper/language buttons, localization); each screen lives in its own class.
    /// </summary>
    public sealed class Ui
    {
        public static readonly Color Rose = new Color32(239, 115, 148, 255), Candle = new Color32(240, 176, 87, 255), Muted = new Color32(173, 154, 140, 255),
            Blood = new Color32(226, 74, 82, 255), Moss = new Color32(143, 203, 156, 255), Ink = new Color32(241, 231, 216, 255);

        internal readonly App app;
        Game G => app.G;
        internal readonly VisualElement root, safe;
        internal readonly bool mobile = Application.isMobilePlatform;
        internal string screen = "";
        float hudT;
        Rect lastSafe;

        internal readonly Overlay overlay;
        internal readonly KidCustomizer kid;
        readonly Hud hud;
        readonly Controls controls;
        readonly HomeMenu home;
        readonly LobbyScreen lobby;
        readonly ResultScreen result;
        readonly PauseMenu pause;

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

            // creation order = drawing order
            overlay = new Overlay(this);          // world layers: vignette, slats, flash, labels, cues
            safe = Ig(E(root, "layer"));
            hud = new Hud(this);
            controls = new Controls(this);
            overlay.BuildMessages();              // countdown, captions, prompt, toasts, banner — above HUD and touch
            kid = new KidCustomizer(this);
            home = new HomeMenu(this); lobby = new LobbyScreen(this); result = new ResultScreen(this); pause = new PauseMenu(this);
            RefreshToggleBtns();
        }

        // ---------- what App calls ----------
        public bool Typing
        {
            get
            {
                var f = root.panel?.focusController?.focusedElement as VisualElement;
                return f != null && (f is TextField || f.GetFirstAncestorOfType<TextField>() != null);
            }
        }
        public Vector2 Joy => controls.Joy;
        public bool SneakOn => controls.SneakOn;
        public bool MenuBack() => home.Back();
        public void ShowPause(bool on, bool online) => pause.Show(on, online);
        public void Banner(string text, string kind, float dur) => overlay.Banner(text, kind, dur);
        public void Toast(string text) => overlay.Toast(text);
        public void Caption(string text) => overlay.Caption(text);
        public void Flash(Color color, float dur) => overlay.Flash(color, dur);
        public void Cue(float ang, float g, Color col) => overlay.Cue(ang, g, col);
        public void Fx(float x, float y, string text, Color color, float size, float life, bool global = false) => overlay.Fx(x, y, text, color, size, life, global);

        // ---------- localized text built once (menus, rules, static labels) ----------
        // Most text is re-applied every tick from Render*/Update* via Loc.T(...) and already follows a language
        // change automatically. These two are for text built ONCE: they remember the key so a language switch
        // can re-apply it later via Relocalize().
        internal readonly List<Action> relocalize = new List<Action>();
        internal Label LL(VisualElement p, string key, params string[] cls) { var l = L(p, Loc.T(key), cls); relocalize.Add(() => l.text = Loc.T(key)); return l; }
        internal Button BB(VisualElement p, string key, Action click, params string[] cls) { var b = B(p, Loc.T(key), click, cls); relocalize.Add(() => b.text = Loc.T(key)); return b; }
        void Relocalize() { foreach (var a in relocalize) a(); RefreshToggleBtns(); lobby.Invalidate(); hud.Invalidate(); }

        /// <summary>The look changed (name/kit): the waiting room shows it.</summary>
        internal void KitChanged() => lobby.Invalidate();

        // ---------- sound / mic / whisper / language: the same buttons on several screens ----------
        readonly List<Button> soundBtns = new List<Button>(), micBtns = new List<Button>(), whisperBtns = new List<Button>(), langBtns = new List<Button>();
        internal Button SoundBtn(VisualElement p, params string[] cls) { var b = B(p, "", ToggleSound, cls); soundBtns.Add(b); return b; }
        /// <summary>Personal choice: when on, the room hears you through proximity voice chat as long as you're not hidden.</summary>
        internal Button MicBtn(VisualElement p, params string[] cls) { var b = B(p, "", ToggleMic, cls); micBtns.Add(b); return b; }
        /// <summary>Personal choice: "whisper" transmits quieter and never counts as noise for the Unice. Needs the mic on.</summary>
        internal Button WhisperBtn(VisualElement p, params string[] cls) { var b = B(p, "", ToggleWhisper, cls); whisperBtns.Add(b); return b; }
        internal Button LangBtn(VisualElement p, params string[] cls) { var b = B(p, "", CycleLang, cls); langBtns.Add(b); return b; }

        void ToggleSound() { app.sfx.On = !app.sfx.On; RefreshToggleBtns(); }
        void ToggleMic() { app.MicOn = !app.MicOn; RefreshToggleBtns(); }
        void ToggleWhisper() { app.Whisper = !app.Whisper; RefreshToggleBtns(); }
        void CycleLang() { Loc.Set((Lang)(((int)Loc.Current + 1) % 3)); Relocalize(); }

        /// <summary>Re-applies every toggle/language button's text, in every screen it appears, from current state.</summary>
        void RefreshToggleBtns()
        {
            string snd = Loc.T(app.sfx.On ? "menu.sound_on" : "menu.sound_off"), mic = Loc.T(app.MicOn ? "menu.mic_on" : "menu.mic_off"),
                   wh = Loc.T(app.Whisper ? "menu.voice_whisper" : "menu.voice_normal"), lang = $"{Loc.T("menu.lang_prefix")}: {Loc.Current.ToString().ToUpperInvariant()}";
            foreach (var b in soundBtns) b.text = snd;
            foreach (var b in micBtns) b.text = mic;
            foreach (var b in whisperBtns) { b.text = wh; b.SetEnabled(app.MicOn); }
            foreach (var b in langBtns) b.text = lang;
        }

        // ---------- per step (20/s): screens, HUD, prompt ----------
        public void Step(float dt)
        {
            SyncScreens();
            hudT -= dt; if (hudT <= 0 && screen == "game") { hudT = .2f; hud.Tick(); }
            controls.UpdatePrompt();
        }

        void SyncScreens()
        {
            var ph = G.ph;
            string want = ph == Phase.Home ? "home" : ph == Phase.Lobby ? "wait" : ph == Phase.Result ? "result" : ph == Phase.Final ? "final" : "game";
            if (want != screen)
            {
                string was = screen; screen = want;
                Show(home.Root, want == "home"); Show(lobby.Root, want == "wait"); Show(result.Root, want == "result" || want == "final");
                Show(hud.Root, want == "game"); Show(controls.Root, want == "game" && mobile);
                if (want == "home") home.OnShow();
                if (want == "wait") lobby.OnShow(was);
                if (want == "result" || want == "final") result.Render(want == "final");
                if (want == "game") hudT = 0;
                if (want != "game") { pause.Show(false, false); app.paused = false; controls.ResetSneak(); }
                hud.Invalidate();
            }
            if (want == "home") home.Sync();
            if (want == "wait") lobby.Render();
            if (want == "result" || want == "final") result.Sync(want == "final");
        }

        // ---------- per frame: world text, cues, overlays ----------
        public void Frame(float dt, Camera cam)
        {
            if (root.panel == null) return;
            ApplySafeArea(); home.LiftForKeyboard();
            float H = root.layout.height; if (float.IsNaN(H) || H < 1) return;
            overlay.Frame(dt, cam);
            if (screen == "home") home.Frame();
        }

        void ApplySafeArea()
        {
            var sa = Screen.safeArea; if (sa == lastSafe || Screen.height < 1) return; lastSafe = sa;
            float k = root.layout.height > 0 ? root.layout.height / Screen.height : 720f / Screen.height;
            safe.style.left = sa.xMin * k; safe.style.right = (Screen.width - sa.xMax) * k;
            safe.style.top = (Screen.height - sa.yMax) * k; safe.style.bottom = sa.yMin * k;
            foreach (var el in new[] { home.Root, lobby.Root, result.Root }) { el.style.left = safe.style.left; el.style.right = safe.style.right; el.style.top = safe.style.top; el.style.bottom = safe.style.bottom; }
        }
    }
}
