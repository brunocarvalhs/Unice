using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>Which full screen is showing; follows <see cref="Game.phase"/>.</summary>
    enum UiScreen { None, Home, Lobby, Game, Result, Final }

    /// <summary>
    /// Every screen and the in-game HUD, built in code with UI Toolkit (styles in Resources/UI/unice.uss).
    /// Same screens and texts as the web version, plus touch controls.
    /// This class only assembles the parts, switches screens and owns what several screens share
    /// (localized text, the setting buttons); each screen and overlay lives in its own class.
    /// </summary>
    public sealed class Ui
    {
        public static readonly Color Rose = new Color32(239, 115, 148, 255), Candle = new Color32(240, 176, 87, 255), Muted = new Color32(173, 154, 140, 255),
            Blood = new Color32(226, 74, 82, 255), Moss = new Color32(143, 203, 156, 255), Ink = new Color32(241, 231, 216, 255);

        const float HudRefreshSeconds = .2f;

        /// <summary>
        /// Panel size in USS pixels, matched by height: the panel is always 540 px tall and as wide as the screen's aspect gives
        /// (960 at 16:9, 1200 at 20:9, 720 at 4:3). 540 (not 720) makes 1 px ≈ 0.79 dp on a 1080p ~400 ppi phone, so a 64 px
        /// button is ≥ 48 dp.
        /// </summary>
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(960, 540);

        internal readonly App app;
        Game game => app.game;
        internal readonly VisualElement root;
        /// <summary>The part of the screen not covered by notches and system bars; HUD and messages live inside it.</summary>
        internal readonly VisualElement safeArea;
        internal readonly bool isMobile = Application.isMobilePlatform;
        internal UiScreen currentScreen = UiScreen.None;
        float hudRefreshTimer;
        Rect lastSafeRect;

        internal readonly Messages messages;
        internal readonly KidCustomizer kidCustomizer;
        internal readonly SettingToggles settings;
        readonly ScreenEffects screenEffects;
        readonly WorldLabels worldLabels;
        readonly SoundCues soundCues;
        readonly Hud hud;
        readonly TouchControls controls;
        readonly HomeMenu homeMenu;
        readonly LobbyScreen lobbyScreen;
        readonly ResultScreen resultScreen;
        readonly PauseMenu pauseMenu;
        readonly KeyboardLift keyboardLift;

        public Ui(App app)
        {
            this.app = app;
            // The panel asset made by Unice > Configurar projeto keeps the UI shaders in the build; the fallback is for the editor.
            var panelSettings = Resources.Load<PanelSettings>("UI/UnicePanel");
            if (panelSettings == null) panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize; panelSettings.referenceResolution = ReferenceResolution;
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panelSettings.match = 1;
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/UniceTheme");
            var uiObject = new GameObject("UI"); uiObject.SetActive(false); uiObject.transform.SetParent(app.transform, false);
            var document = uiObject.AddComponent<UIDocument>(); document.panelSettings = panelSettings; uiObject.SetActive(true);
            root = document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/unice"));
            root.AddToClassList("root"); root.pickingMode = PickingMode.Ignore;

            // creation order = drawing order (later parts are drawn on top)
            screenEffects = new ScreenEffects(this);
            worldLabels = new WorldLabels(this);
            soundCues = new SoundCues(this);
            safeArea = IgnorePointer(AddElement(root, "layer"));
            hud = new Hud(this);
            controls = new TouchControls(this);
            messages = new Messages(this);
            settings = new SettingToggles(this, Relocalize);
            kidCustomizer = new KidCustomizer(this);
            homeMenu = new HomeMenu(this); lobbyScreen = new LobbyScreen(this); resultScreen = new ResultScreen(this); pauseMenu = new PauseMenu(this);
            keyboardLift = new KeyboardLift(this, homeMenu.Root);   // the only screen with text fields
            settings.Refresh();
        }

        // ---------- what App and the gameplay code call ----------
        /// <summary>A text field has focus: keyboard keys type instead of controlling the game.</summary>
        public bool Typing
        {
            get
            {
                var focused = root.panel?.focusController?.focusedElement as VisualElement;
                return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
            }
        }
        public Vector2 JoystickDirection => controls.JoystickDirection;
        public bool SneakToggled => controls.SneakToggled;
        public void ReleaseTouchControls() => controls.Release();
        /// <summary>Back button on the menu: returns false when already on the main page.</summary>
        public bool MenuBack() => homeMenu.Back();
        public void ShowPause(bool visible, bool online) => pauseMenu.Show(visible, online);
        public void Banner(string text, BannerStyle style, float seconds) => messages.Banner(text, style, seconds);
        public void Toast(string text) => messages.Toast(text);
        public void Caption(string text) => messages.Caption(text);
        public void Flash(Color color, float seconds) => screenEffects.Flash(color, seconds);
        public void ShowSoundCue(float angle, float loudness, Color color) => soundCues.ShowSoundCue(angle, loudness, color);
        public void ShowWorldText(float x, float y, string text, Color color, float size, float lifetime, bool alwaysVisible = false) =>
            worldLabels.ShowWorldText(x, y, text, color, size, lifetime, alwaysVisible);

        // ---------- localized text built once (menus, rules, static labels) ----------
        // Most text is re-applied every step from the Render/Refresh methods via Localization.Get(...) and follows a language
        // change automatically. These two are for text built ONCE: they remember the key so a language switch
        // can re-apply it later via Relocalize().
        internal readonly List<Action> relocalizers = new List<Action>();
        internal Label LocalizedLabel(VisualElement parent, string key, params string[] classes)
        {
            var label = AddLabel(parent, Localization.Get(key), classes);
            relocalizers.Add(() => label.text = Localization.Get(key));
            return label;
        }
        internal Button LocalizedButton(VisualElement parent, string key, Action onClick, params string[] classes)
        {
            var button = AddButton(parent, Localization.Get(key), onClick, classes);
            relocalizers.Add(() => button.text = Localization.Get(key));
            return button;
        }
        void Relocalize()
        {
            foreach (var relocalize in relocalizers) relocalize();
            settings.Refresh(); lobbyScreen.Invalidate(); hud.Invalidate();
        }

        /// <summary>The look changed (name/kit): the waiting room shows it.</summary>
        internal void OnKitChanged() => lobbyScreen.Invalidate();

        // ---------- per step (20/s): screens, HUD, prompt ----------
        public void Step(float dt)
        {
            SyncScreens();
            hudRefreshTimer -= dt;
            if (hudRefreshTimer <= 0 && currentScreen == UiScreen.Game) { hudRefreshTimer = HudRefreshSeconds; hud.Tick(); }
            var hints = ActionHints.For(game, app.myCharacter, showKeys: !isMobile);
            messages.SetPrompt(hints.text);
            if (isMobile) controls.ShowAvailable(hints);
        }

        static UiScreen ScreenFor(Phase phase) => phase switch
        {
            Phase.Home => UiScreen.Home,
            Phase.Lobby => UiScreen.Lobby,
            Phase.Result => UiScreen.Result,
            Phase.Final => UiScreen.Final,
            _ => UiScreen.Game,
        };

        /// <summary>Shows the screen for the current phase (running each screen's on-show step once), then refreshes it.</summary>
        void SyncScreens()
        {
            var wanted = ScreenFor(game.phase);
            bool resultLike = wanted == UiScreen.Result || wanted == UiScreen.Final;
            if (wanted != currentScreen)
            {
                var previous = currentScreen; currentScreen = wanted;
                Show(homeMenu.Root, wanted == UiScreen.Home); Show(lobbyScreen.Root, wanted == UiScreen.Lobby); Show(resultScreen.Root, resultLike);
                Show(hud.Root, wanted == UiScreen.Game); Show(controls.Root, wanted == UiScreen.Game && isMobile);
                if (wanted == UiScreen.Home) homeMenu.OnShow();
                if (wanted == UiScreen.Lobby) lobbyScreen.OnShow(cameFromFinal: previous == UiScreen.Final);
                if (resultLike) resultScreen.Render(wanted == UiScreen.Final);
                if (wanted == UiScreen.Game) hudRefreshTimer = 0;
                // any screen change closes the pause menu (it can be open on the result screen when the next round starts)
                pauseMenu.Show(false, false); app.paused = false; controls.Release();
                hud.Invalidate();
            }
            if (wanted == UiScreen.Home) homeMenu.Refresh();
            if (wanted == UiScreen.Lobby) lobbyScreen.Render();
            if (resultLike) resultScreen.Refresh(wanted == UiScreen.Final);
        }

        // ---------- per frame: world text, cues, overlays ----------
        public void Frame(float dt, Camera camera)
        {
            if (root.panel == null) return;
            ApplySafeArea(); keyboardLift.Update();
            float panelHeight = root.layout.height;
            if (float.IsNaN(panelHeight) || panelHeight < 1) return;   // no layout yet
            messages.Frame(dt);
            worldLabels.Frame(dt, camera);
            soundCues.Frame(dt, camera);
            screenEffects.Frame(dt);
            if (currentScreen == UiScreen.Home) homeMenu.Frame();
        }

        /// <summary>Keeps the HUD and the full screens inside Screen.safeArea (notches, rounded corners, system bars).</summary>
        void ApplySafeArea()
        {
            var safeRect = Screen.safeArea;
            if (safeRect == lastSafeRect || Screen.height < 1) return;
            lastSafeRect = safeRect;
            float pixelsToPanel = root.layout.height > 0 ? root.layout.height / Screen.height : (float)ReferenceResolution.y / Screen.height;
            safeArea.style.left = safeRect.xMin * pixelsToPanel; safeArea.style.right = (Screen.width - safeRect.xMax) * pixelsToPanel;
            safeArea.style.top = (Screen.height - safeRect.yMax) * pixelsToPanel; safeArea.style.bottom = safeRect.yMin * pixelsToPanel;
            foreach (var screen in new[] { homeMenu.Root, lobbyScreen.Root, resultScreen.Root })
            {
                screen.style.left = safeArea.style.left; screen.style.right = safeArea.style.right;
                screen.style.top = safeArea.style.top; screen.style.bottom = safeArea.style.bottom;
            }
        }
    }
}
