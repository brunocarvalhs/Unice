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
    public sealed class Ui
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

        // ---------- main menu (over the house map) ----------
        VisualElement home, menuMain, menuOnline, menuHelp, menuKid, kidSlot, custom; TextField nameIn, codeIn; Label netLbl;
        Button createBtn, joinBtn, soundBtn, micBtn, whisperBtn, langBtn, localBtn, onlineBtn; string page = "main";

        void BuildHome()
        {
            home = E(root, "menu");
            L(home, "v" + Application.version, "menu-version");

            menuMain = E(home, "menu-page");
            L(menuMain, "Unice", "menu-title");
            LL(menuMain, "menu.sub", "menu-sub");
            var grid = E(menuMain, "menu-grid");
            localBtn = BB(grid, "menu.local", () => app.PlaySolo(), "mbtn");
            onlineBtn = BB(grid, "menu.online", () => { app.SetNet(""); Page("online"); }, "mbtn");
            BB(grid, "menu.howto", () => Page("help"), "mbtn", "mbtn-sm");
            BB(grid, "menu.mykid", () => Page("kid"), "mbtn", "mbtn-sm");

            // online: create a room (the code is generated for you) or join one with a code
            var on = Panel(out menuOnline, "menu.online_title");
            LL(on, "menu.online_desc", "mnote");
            createBtn = BB(on, "menu.create_room", () => app.EnterRoom(true, null), "mbtn", "mbtn-wide");
            LL(on, "menu.or_join", "menu-or");
            var jr = E(on, "row");
            codeIn = new TextField { maxLength = 10 }; codeIn.AddToClassList("mfield"); codeIn.AddToClassList("grow"); jr.Add(codeIn);
            joinBtn = BB(jr, "menu.join", JoinFromInput, "mbtn", "mbtn-join");
            netLbl = L(on, "", "mnote", "mnet");
            BB(on, "menu.back", () => Page("main"), "mbtn", "mbtn-back");

            // how to play
            var hp = Panel(out menuHelp, "menu.howto_title");
            var sc = new ScrollView(ScrollViewMode.Vertical); sc.AddToClassList("mscroll"); hp.Add(sc);
            void Rule(string tKey, string bKey, string cls) { var x = E(sc, "mrule"); var tl = LL(x, tKey, "mrule-t"); if (cls != null) tl.AddToClassList(cls); LL(x, bKey, "mnote"); }
            Rule("rule.unice.t", "rule.unice.b", "mrule-rose");
            Rule("rule.teamwin.t", "rule.teamwin.b", "mrule-moss");
            Rule("rule.unicewin.t", "rule.unicewin.b", "mrule-rose");
            Rule("rule.hidetime.t", "rule.hidetime.b", null);
            Rule("rule.dark.t", "rule.dark.b", null);
            Rule("rule.gigglerule.t", "rule.gigglerule.b", "mrule-rose");
            Rule("rule.laugh.t", "rule.laugh.b", "mrule-rose");
            Rule("rule.rotation.t", "rule.rotation.b", null);
            Rule("rule.controls.t", "rule.controls.b", null);
            BB(hp, "menu.back", () => Page("main"), "mbtn", "mbtn-back");

            // my child: the same customization as in the waiting room
            var kp = Panel(out menuKid, "menu.mykid_title");
            var ks = new ScrollView(ScrollViewMode.Vertical); ks.AddToClassList("mscroll"); kp.Add(ks);
            kidSlot = E(ks, "mkid");
            BB(kp, "menu.ready", () => Page("main"), "mbtn", "mbtn-back");
            custom = BuildCustom();

            var foot = E(home, "menu-foot");
            soundBtn = B(foot, "", ToggleSound, "mpill");
            micBtn = B(foot, "", ToggleMic, "mpill");
            whisperBtn = B(foot, "", ToggleWhisper, "mpill");
            langBtn = B(foot, "", CycleLang, "mpill");
            BB(home, "menu.quit", () => Application.Quit(), "mbtn", "mexit");
            Page("main");
        }

        VisualElement Panel(out VisualElement page, string titleKey)
        {
            page = E(home, "menu-page");
            var p = E(page, "mpanel");
            LL(p, titleKey, "mpanel-t");
            return p;
        }

        void Page(string p)
        {
            page = p;
            Show(menuMain, p == "main"); Show(menuOnline, p == "online"); Show(menuHelp, p == "help"); Show(menuKid, p == "kid");
            if (p == "kid") { kidSlot.Add(custom); RenderOpts(); }
        }

        /// <summary>Back button on the menu: sub-pages go back to the main page. False when already there.</summary>
        public bool MenuBack() { if (screen != "home" || page == "main") return false; Page("main"); return true; }

        /// <summary>Name, preview and look options. Lives in the waiting room or in "Minha criança".</summary>
        VisualElement BuildCustom()
        {
            var c = new VisualElement();
            LL(c, "lbl.name", "lbl");
            nameIn = Field(c, app.myName, 12);
            nameIn.RegisterValueChangedCallback(e => { app.myName = U.CleanName(e.newValue); app.SaveProfile(); });
            LL(c, "lbl.kid", "lbl").style.marginTop = 10;
            var pr = E(c, "row");
            var pv = E(pr, "preview"); pv.style.alignItems = Align.Center; pv.style.justifyContent = Justify.Center;
            preview = new Doll(100); pv.Add(preview);
            BB(pr, "menu.randomize", () => { app.SetKit(Kit.Random()); RenderOpts(); wSig = null; }, "btn", "btn-ghost");
            opts = E(c); opts.style.marginTop = 10;
            BuildOpts();
            return c;
        }

        void JoinFromInput()
        {
            var c = U.CleanCode(codeIn.value);
            if (c.Length < 4) { app.SetNet(Loc.T("menu.need_code"), true); return; }
            app.EnterRoom(false, c);
        }

        void ToggleSound() { app.sfx.On = !app.sfx.On; RefreshToggleBtns(); }

        /// <summary>Personal choice: when on, the room hears you through proximity voice chat as long as you're not hidden.</summary>
        void ToggleMic() { app.MicOn = !app.MicOn; RefreshToggleBtns(); }

        /// <summary>Personal choice: "whisper" transmits quieter and never counts as noise for the Unice. Needs the mic on.</summary>
        void ToggleWhisper() { app.Whisper = !app.Whisper; RefreshToggleBtns(); }

        void CycleLang() { Loc.Set((Lang)(((int)Loc.Current + 1) % 3)); Relocalize(); }

        /// <summary>Re-applies every toggle/language button's text, in every screen it appears, from current state.</summary>
        void RefreshToggleBtns()
        {
            string snd = Loc.T(app.sfx.On ? "menu.sound_on" : "menu.sound_off");
            soundBtn.text = snd; pauseSound.text = snd;
            string mic = Loc.T(app.MicOn ? "menu.mic_on" : "menu.mic_off");
            micBtn.text = mic; pauseMic.text = mic; waitMic.text = mic;
            string t = Loc.T(app.Whisper ? "menu.voice_whisper" : "menu.voice_normal");
            whisperBtn.text = t; whisperBtn.SetEnabled(app.MicOn);
            waitWhisper.text = t; waitWhisper.SetEnabled(app.MicOn);
            pauseWhisper.text = t; pauseWhisper.SetEnabled(app.MicOn);
            langBtn.text = $"{Loc.T("menu.lang_prefix")}: {Loc.Current.ToString().ToUpperInvariant()}";
        }

        // ---------- waiting room (full screen, made for a phone on its side) ----------
        VisualElement wait, wList, hostOpts, opts, sidePartida, sideAparencia; VisualElement sideTabPartida, sideTabAparencia;
        Label wTitle, wCode, wNote, wCount, wStatus, optsInfo, botsVal, roundsVal, hideVal, huntVal, giggleVal, mapVal;
        Button copyBtn, actBtn, leave2, waitMic, waitWhisper; Doll preview; string wSig, sideTab = "tab.match"; bool lobbyHost, waitCanGo;
        static readonly int[] RoundChoices = { 3, 5, 7 };

        void BuildWait()
        {
            wait = E(root, "lobby");
            var top = E(wait, "lobby-top");
            leave2 = B(top, "", () => app.LeaveRoom(), "mbtn", "mbtn-top");
            var head = E(top, "lobby-head");
            wTitle = L(head, "", "lobby-title"); wCode = L(head, "", "lobby-code");
            copyBtn = BB(head, "lobby.copy_code", CopyCode, "mpill");
            waitMic = B(top, "", ToggleMic, "mpill");
            waitWhisper = B(top, "", ToggleWhisper, "mpill");
            wCount = L(top, "", "lobby-count");
            var body = E(wait, "lobby-body");
            wList = E(body, "lobby-players");
            var side = E(body, "mpanel", "lobby-side");
            var sideTabs = E(side, "tabbar");
            sideTabPartida = BB(sideTabs, "tab.match", () => { sideTab = "tab.match"; RefreshSideTab(); }, "tabbtn");
            sideTabAparencia = BB(sideTabs, "tab.appearance", () => { sideTab = "tab.appearance"; RefreshSideTab(); }, "tabbtn");
            var sidePartidaSv = new ScrollView(ScrollViewMode.Vertical); sidePartidaSv.style.flexGrow = 1; sidePartidaSv.style.maxHeight = 320;
            sidePartidaSv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            side.Add(sidePartidaSv); sidePartida = sidePartidaSv;
            hostOpts = E(sidePartida);
            botsVal = Stepper(hostOpts, "lobby.bots_plural", d => { int max = Math.Max(0, Rules.MaxPlayers - app.LobbyHumans().Count); G.bots = Mathf.Clamp(Math.Min(G.bots, max) + d, 0, max); wSig = null; });
            roundsVal = Stepper(hostOpts, "lobby.rounds_label", d => { int i = Array.IndexOf(RoundChoices, G.rounds); G.rounds = RoundChoices[Mathf.Clamp((i < 0 ? 1 : i) + d, 0, RoundChoices.Length - 1)]; wSig = null; });
            hideVal = Stepper(hostOpts, "lobby.hide_label", d => { G.hideTime = Mathf.Clamp(G.hideTime + d * 5, 15, 60); wSig = null; });
            huntVal = Stepper(hostOpts, "lobby.hunt_label", d => { G.roundTime = Mathf.Clamp(G.roundTime + d * 15, 60, 240); wSig = null; });
            giggleVal = Stepper(hostOpts, "lobby.giggle_label", d => { G.giggleEvery = Mathf.Clamp(G.giggleEvery + d * 5, 5, 20); wSig = null; });
            mapVal = Stepper(hostOpts, "lobby.map_label", d => { int i = Array.IndexOf(Maps.AllIds, G.mapId); G.mapId = Maps.AllIds[((i < 0 ? 0 : i) + d + Maps.AllIds.Length) % Maps.AllIds.Length]; wSig = null; });
            optsInfo = L(sidePartida, "", "mnote");
            var sideAparenciaSv = new ScrollView(ScrollViewMode.Vertical); sideAparenciaSv.style.flexGrow = 1; sideAparenciaSv.style.maxHeight = 320;
            sideAparenciaSv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            side.Add(sideAparenciaSv); sideAparencia = sideAparenciaSv;
            wStatus = L(side, "", "mnote", "lobby-status");
            actBtn = B(side, "", () =>
            {
                if (!lobbyHost) { app.ToggleReady(); wSig = null; return; }
                if (!waitCanGo) return;
                app.StartMatch(Math.Min(G.bots, Math.Max(0, Rules.MaxPlayers - app.LobbyHumans().Count)), G.rounds);
            }, "mbtn", "mbtn-go");
            wNote = L(wait, "", "lobby-note");
            RefreshSideTab();
        }

        void RefreshSideTab()
        {
            Show(sidePartida, sideTab == "tab.match"); Show(sideAparencia, sideTab == "tab.appearance");
            sideTabPartida.EnableInClassList("tabbtn-on", sideTab == "tab.match");
            sideTabAparencia.EnableInClassList("tabbtn-on", sideTab == "tab.appearance");
            if (sideTab == "tab.appearance" && custom.parent != sideAparencia) { sideAparencia.Add(custom); RenderOpts(); }
        }

        Label Stepper(VisualElement p, string labelKey, Action<int> change)
        {
            var s = E(p, "mstep");
            LL(s, labelKey, "mstep-l");
            B(s, "‹", () => change(-1), "mstep-btn");
            var v = L(s, "", "mstep-v");
            B(s, "›", () => change(1), "mstep-btn");
            return v;
        }

        void CopyCode()
        {
            GUIUtility.systemCopyBuffer = app.code;
            copyBtn.text = Loc.T("lobby.copied");
            copyBtn.schedule.Execute(() => copyBtn.text = Loc.T("lobby.copy_code")).ExecuteLater(1800);
        }

        static readonly (string labelKey, int slot, string[] list, bool color, string tabKey)[] OptRows =
        {
            ("opt.skin", 0, Kit.Skins, true, "tab.color"), ("opt.haircolor", 2, Kit.HairColors, true, "tab.color"), ("opt.outfitcolor", 4, Kit.OutfitColors, true, "tab.color"),
            ("opt.hair", 1, Kit.Hairs, false, "tab.hair"), ("opt.outfit", 3, Kit.Outfits, false, "tab.outfit"),
            ("opt.hat", 5, Kit.Hats, false, "tab.hat"), ("opt.accessory", 6, Kit.Accessories, false, "tab.accessory")
        };
        static readonly string[] OptTabs = { "tab.color", "tab.hair", "tab.outfit", "tab.hat", "tab.accessory" };
        readonly List<(int slot, int v, VisualElement el)> swatches = new List<(int, int, VisualElement)>();
        readonly Dictionary<int, Label> cycleVals = new Dictionary<int, Label>();
        readonly List<(string tab, VisualElement el)> optRowEls = new List<(string, VisualElement)>();
        readonly Dictionary<string, VisualElement> optTabBtns = new Dictionary<string, VisualElement>();
        string optTab = OptTabs[0];

        void BuildOpts()
        {
            var tabBar = E(opts, "tabbar");
            foreach (var tk in OptTabs)
            {
                var btn = B(tabBar, "", () => { optTab = tk; RenderOpts(); }, "tabbtn");
                relocalize.Add(() => btn.text = Loc.T(tk).ToUpperInvariant());
                optTabBtns[tk] = btn;
            }
            var rows = E(opts);
            foreach (var (labelKey, slot, list, color, tab) in OptRows)
            {
                var row = E(rows, "opt");
                LL(row, labelKey, "opt-l");
                if (color)
                {
                    var sws = E(row, "sws");
                    for (int j = 0; j < list.Length; j++)
                    {
                        int v = j;
                        var b = B(sws, "", () => { var k = (int[])app.kit.Clone(); k[slot] = v; app.SetKit(k); RenderOpts(); wSig = null; }, "sw");
                        b.style.backgroundColor = Art.Hex(list[j]);
                        swatches.Add((slot, j, b));
                    }
                }
                else
                {
                    var cy = E(row, "cy");
                    void Cycle(int d) { var k = (int[])app.kit.Clone(); k[slot] = (k[slot] + d + Kit.Max[slot]) % Kit.Max[slot]; app.SetKit(k); RenderOpts(); wSig = null; }
                    B(cy, "‹", () => Cycle(-1), "cy-btn");
                    cycleVals[slot] = L(cy, "", "cy-val");
                    B(cy, "›", () => Cycle(1), "cy-btn");
                }
                optRowEls.Add((tab, row));
            }
            RenderOpts();
        }

        void RenderOpts()
        {
            foreach (var (slot, v, el) in swatches) el.EnableInClassList("sw-on", app.kit[slot] == v);
            foreach (var (labelKey, slot, list, color, tab) in OptRows) if (!color) cycleVals[slot].text = Loc.KitOpt(slot, app.kit[slot]);
            foreach (var (tab, el) in optRowEls) Show(el, tab == optTab);
            foreach (var kv in optTabBtns) kv.Value.EnableInClassList("tabbtn-on", kv.Key == optTab);
        }

        void RenderWait()
        {
            var hs = app.LobbyHumans(); var host = app.Online ? app.FindHost() : null; string hostId = host != null ? host.id : app.Online ? null : app.myId;
            int maxBots = Math.Max(0, Rules.MaxPlayers - hs.Count), bots = Math.Min(G.bots, maxBots);
            string sig = string.Join("|", hs.Select(h => h.id + h.name + Kit.Pack(h.k) + h.rdy)) + hostId + G.isHost + bots + G.rounds + G.hideTime + G.roundTime + G.giggleEvery + G.mapId + app.rdy + app.code;
            if (sig == wSig) return; wSig = sig;
            bool on = app.Online;
            Txt(wTitle, Loc.T(on ? "lobby.room" : "lobby.local")); Txt(wCode, on ? app.code : ""); Show(wCode, on); Show(copyBtn, on);
            Txt(wCount, $"{hs.Count + bots}/{Rules.MaxPlayers}");
            Txt(wNote, Loc.T(on ? "lobby.note_online" : "lobby.note_local"));
            wList.Clear();
            foreach (var h in hs)
            {
                var card = E(wList, "pcard"); if (h.me) card.AddToClassList("pcard-me");
                var d = new Doll(62); d.Set(h.k); card.Add(d);
                L(card, h.name, "pcard-name");
                var st = L(card, Loc.T(h.id == hostId ? "lobby.host" : h.rdy ? "lobby.ready" : "lobby.waiting"), "pcard-st");
                if (h.id == hostId || h.rdy) st.AddToClassList("pcard-ok");
            }
            if (bots > 0) { var bc = E(wList, "pcard", "pcard-bots"); L(bc, "+" + bots, "pcard-plus"); L(bc, Loc.T(bots > 1 ? "lobby.bots_plural" : "lobby.bot_singular"), "pcard-st"); }
            Txt(botsVal, bots.ToString()); Txt(roundsVal, G.rounds.ToString());
            Txt(hideVal, $"{G.hideTime:0}s"); Txt(huntVal, $"{G.roundTime:0}s"); Txt(giggleVal, $"{G.giggleEvery:0}s");
            Txt(mapVal, Loc.T("map." + G.mapId + ".name"));
            Show(hostOpts, G.isHost);
            Txt(optsInfo, G.isHost ? "" : Loc.T("lobby.info", bots, G.rounds, G.hideTime, G.roundTime, G.giggleEvery, Loc.T("map." + G.mapId + ".name")));
            Show(optsInfo, !G.isHost);
            int waiting = hs.Count(h => h.id != hostId && !h.rdy);
            lobbyHost = G.isHost; waitCanGo = hs.Count + bots >= 2;
            Txt(wStatus, G.isHost ? (hs.Count + bots < 2 ? Loc.T("lobby.need_bot") : waiting > 0 ? Loc.T("lobby.can_start", waiting) : Loc.T("lobby.all_ready"))
                                  : Loc.T("lobby.host_will_start"));
            actBtn.text = Loc.T(G.isHost ? "lobby.start" : app.rdy ? "lobby.cancel" : "lobby.im_ready");
            actBtn.SetEnabled(!G.isHost || waitCanGo);
            actBtn.EnableInClassList("mbtn-on", !G.isHost && app.rdy);
            leave2.text = Loc.T(on ? "lobby.leave" : "menu.back");
        }

        // ---------- result / final (full screen, no scrolling) ----------
        VisualElement resultOv, resHead, resCards, resSide; Label nextIn; Button againBtn; Label waitHost;

        void BuildResult()
        {
            resultOv = E(root, "lobby", "result");
            resHead = E(resultOv, "res-head");
            var body = E(resultOv, "lobby-body");
            resCards = E(body, "lobby-players");
            resSide = E(body, "mpanel", "lobby-side");
        }

        void RenderResult(bool final)
        {
            resHead.Clear(); resCards.Clear(); resSide.Clear(); nextIn = null; againBtn = null; waitHost = null;
            var res = G.res ?? new[] { 0, -1, -1, -1 };
            bool unWin = res[0] == 1; var who = G.EntAt(res[1]); var Un = G.unice; var me = app.me; var nx = G.EntAt(res[3]);
            bool? won = me != null ? unWin == me.IsUnice : (bool?)null;
            string title = Loc.T(unWin ? (Un == me ? "res.doll_back_you" : "res.doll_back_unice") : (Un == me ? "res.timeout_unice" : "res.team_won"));
            var spot = res[2] >= 0 && res[2] < Map.Spots.Length ? Map.Spots[res[2]] : null;
            string reason = who == null ? "" : unWin ? Loc.T("res.reason_won", app.NameUp(Un), app.NameOf(who), Loc.RoomIn(Map.RoomOf(who.x, who.y)))
                                                     : Loc.T("res.reason_lost", app.NameOf(who), spot != null ? " " + Loc.Spot(spot) : "");
            if (final)
            {
                int top = G.ents.Count > 0 ? G.ents.Max(e => e.score) : 0; var ws = G.ents.Where(e => e.score == top).ToList();
                bool iWon = me != null && ws.Contains(me);
                string head = iWon ? Loc.T(ws.Count > 1 ? "res.tie" : "res.you_won") : Loc.T("res.winners", string.Join(" e ", ws.Select(w => w.name)), Loc.T(ws.Count > 1 ? "res.won_plural" : "res.won_single"));
                L(resHead, Loc.T("res.final_header", G.rounds), "res-eyebrow");
                L(resHead, head, "res-title", iWon ? "res-win" : "res-lose");
                L(resHead, Loc.T("res.last_round", title, reason), "res-reason");
                Cards(me, ws);
                L(resSide, Loc.T("res.final_score"), "mstep-l", "res-lbl");
                againBtn = B(resSide, Loc.T("res.new_match"), () => app.NewMatchLobby(), "mbtn", "mbtn-go");
                waitHost = L(resSide, Loc.T("res.waiting_host"), "mnote", "lobby-status");
                B(resSide, Loc.T(app.Online ? "res.leave_room" : "res.back_home"), () => app.LeaveRoom(), "mbtn", "mbtn-back");
            }
            else
            {
                L(resHead, Loc.T("res.round_header", G.r, G.rounds), "res-eyebrow");
                var t = L(resHead, title, "res-title"); if (won != null) t.AddToClassList(won.Value ? "res-win" : "res-lose");
                L(resHead, reason, "res-reason");
                Cards(me, null);
                L(resSide, Loc.T("res.next_unice"), "mstep-l", "res-lbl");
                var nu = E(resSide, "res-next"); var d = new Doll(46); d.SetUnice(); nu.Add(d); L(nu, app.NameUp(nx), "res-next-name");
                L(resSide, Loc.T(unWin ? "res.next_rule_won" : "res.next_rule_timeout"), "mnote", "lobby-status");
                var nr = E(resSide, "res-count"); L(nr, Loc.T("res.next_in"), "mnote"); nextIn = L(nr, "", "res-count-n"); L(nr, Loc.T("res.seconds_suffix"), "mnote");
                L(resSide, Loc.T("res.scoring_rules"), "res-rules");
            }
        }

        /// <summary>Score cards, best first. Winners of the match get a green border.</summary>
        void Cards(Ent me, List<Ent> winners)
        {
            int i = 0;
            foreach (var e in G.ents.OrderByDescending(e => e.score))
            {
                var card = E(resCards, "pcard"); if (e == me) card.AddToClassList("pcard-me"); if (winners != null && winners.Contains(e)) card.AddToClassList("pcard-win");
                L(card, Loc.Ordinal(++i), "pcard-rank");
                var d = new Doll(50); d.Set(e.k); card.Add(d);
                L(card, e.name + (e.bot ? Loc.T("res.bot_tag") : ""), "pcard-name");
                L(card, e.score.ToString(), "pcard-pts");
            }
        }

        // ---------- pause / menu ----------
        VisualElement pauseOv; Label pauseTitle, pauseNote; Button pauseSound, pauseMic, pauseWhisper, pauseLeave;

        void BuildPause()
        {
            var sh = Sheet(root, out pauseOv);
            pauseTitle = L(sh, "", "title", "title-md");
            pauseNote = L(sh, "", "muted");
            var r = E(sh, "row", "gap", "wrap");
            BB(r, "pause.continue", () => app.TogglePause(), "btn");
            pauseSound = B(r, "", ToggleSound, "btn", "btn-ghost");
            pauseMic = B(r, "", ToggleMic, "btn", "btn-ghost");
            pauseWhisper = B(r, "", ToggleWhisper, "btn", "btn-ghost");
            pauseLeave = B(r, "", () => app.LeaveRoom(), "btn", "btn-link");
            Show(pauseOv, false);
        }

        public void ShowPause(bool on, bool online)
        {
            Show(pauseOv, on);
            pauseTitle.text = Loc.T(online ? "pause.online_title" : "pause.title");
            pauseNote.text = Loc.T(online ? "pause.note_online" : "pause.note_local");
            pauseLeave.text = Loc.T(online ? "res.leave_room" : "pause.leave_match");
        }

        // ---------- HUD ----------
        Label hRound, hClock, hPhase, hWhere, hRoom, hRole, hGoal, hDollWho, hDollNote, hNoiseTxt;
        VisualElement roleCard, gigWrap, gigFill, noiseBox, noiseFill, plist, plRows, mini;
        string plSig;

        void BuildHud()
        {
            var tl = E(hud, "hud-card", "hud-tl"); Ig(tl);
            var rr = E(tl, "row"); LL(rr, "hud.round", "hud-lbl"); hRound = L(rr, "", "hud-lbl");
            hClock = L(tl, "0:30", "clock"); hPhase = L(tl, "", "phase"); hWhere = L(tl, "", "where"); hRoom = L(tl, "", "roomline");

            roleCard = E(hud, "hud-card", "hud-tr"); Ig(roleCard);
            hRole = L(roleCard, "", "role-t"); hGoal = L(roleCard, "", "role-g");
            var dr = E(roleCard, "row"); dr.style.marginTop = 8; LL(dr, "hud.doll", "hud-lbl"); hDollWho = L(dr, "", "hud-lbl");
            gigWrap = E(roleCard, "meter"); gigFill = E(gigWrap, "meter-fill");
            hDollNote = L(roleCard, "", "note");
            noiseBox = E(roleCard); noiseBox.style.marginTop = 8;
            var nr = E(noiseBox, "row"); LL(nr, "hud.noise", "hud-lbl"); hNoiseTxt = L(nr, "", "hud-lbl");
            var nm = E(noiseBox, "meter", "meter-noise"); noiseFill = E(nm, "meter-fill");
            foreach (var l in roleCard.Query<Label>().ToList()) Ig(l);
            foreach (var l in tl.Query<Label>().ToList()) Ig(l);

            var btns = E(hud, "hud-btns"); Ig(btns);
            BB(btns, "hud.map_btn", () => { Show(plist, plist.style.display == DisplayStyle.None); plSig = null; }, "hud-btn");
            BB(btns, "hud.menu_btn", () => app.TogglePause(), "hud-btn");
            plist = E(hud, "hud-card", "plist"); Ig(plist);
            LL(plist, "hud.house_plan", "hud-lbl");
            mini = Ig(E(plist, "mini")); mini.generateVisualContent += DrawMini;
            LL(plist, "hud.players", "hud-lbl").style.marginTop = 8;
            plRows = Ig(E(plist));
            Show(plist, false);
        }

        void UpdateHud()
        {
            var me = app.me; var Un = G.unice; var h = G.doll.holder; bool tv = app.TeamView, live = G.Live;
            Txt(hRound, $"{G.r}/{G.rounds}");
            Txt(hClock, U.Mmss(live ? G.tl : 0));
            hClock.EnableInClassList("clock-low", G.ph == Phase.Hunt && G.tl <= 15);
            string phaseKey = G.ph == Phase.Hide ? "hud.phase_hide" : G.ph == Phase.Hunt ? (Un == me ? "hud.phase_hunt_you" : "hud.phase_hunt_other") : G.ph == Phase.Result ? "hud.phase_result" : null;
            Txt(hPhase, phaseKey != null ? Loc.T(phaseKey) : "");
            Txt(hWhere, me != null && !me.outOfRound && me.x > 0 && live ? Loc.T("hud.where", Loc.RoomIn(Map.RoomOf(me.x, me.y))) : "");
            Txt(hRoom, app.Online ? Loc.T("hud.room_online", app.code) + (G.isHost ? Loc.T("hud.room_host_suffix") : "") : Loc.T("hud.room_solo"));
            string role, goal, cls = "";
            if (me == null) { role = Loc.T("hud.role.watch"); goal = Loc.T("hud.goal.watch"); }
            else if (me.IsUnice) { role = Loc.T("hud.role.unice"); cls = "role-unice"; goal = Loc.T(G.ph == Phase.Hide ? "hud.goal.unice_hide" : "hud.goal.unice_hunt"); }
            else if (me.outOfRound) { role = Loc.T("hud.role.caught"); cls = "role-out"; goal = Loc.T("hud.goal.caught"); }
            else if (h == me) { role = Loc.T("hud.role.holder"); cls = "role-holder"; goal = Loc.T("hud.goal.holder", G.giggleEvery.ToString("0")); }
            else { role = Loc.T("hud.role.hiding"); goal = Loc.T(me.hidden != null ? "hud.goal.hiding_spot" : "hud.goal.hiding_out"); }
            Txt(hRole, role); Txt(hGoal, goal);
            foreach (var c in new[] { "role-unice", "role-out", "role-holder" }) roleCard.EnableInClassList(c, c == cls);
            float pct = 0; string note;
            if (h != null && tv)
            {
                Txt(hDollWho, h == me ? Loc.T("hud.doll_with_you") : Loc.T("hud.doll_with", h.name.ToUpperInvariant()));
                pct = G.ph == Phase.Hunt ? 1 - G.doll.giggleT / G.giggleEvery : 0;
                note = G.ph == Phase.Hide ? Loc.T("hud.giggle_wait_enter") : h.hidden != null ? Loc.T("hud.giggle_in", Loc.Spot(h.hidden), Mathf.CeilToInt(G.doll.giggleT)) : Loc.T("hud.giggle_quiet");
                if (h == me && me.passCd > 0) note += Loc.T("hud.repass_in", Mathf.CeilToInt(me.passCd));
                Show(gigWrap, true);
            }
            else
            {
                Txt(hDollWho, h != null ? Loc.T("hud.doll_hidden") : "");
                note = G.doll.hasLast ? Loc.T("hud.giggle_last", Loc.RoomIn(Map.RoomOf(G.doll.lastX, G.doll.lastY)), Mathf.Max(0, Mathf.FloorToInt(G.t - G.doll.lastT))) : Loc.T("hud.giggle_every", G.giggleEvery.ToString("0"));
                Show(gigWrap, false);
            }
            gigFill.style.width = Length.Percent(Mathf.Clamp01(pct) * 100);
            Txt(hDollNote, note);
            Show(noiseBox, me != null && !me.outOfRound && !me.IsUnice);
            if (me != null)
            {
                noiseFill.style.width = Length.Percent(Mathf.Clamp01(me.noise) * 100);
                Txt(hNoiseTxt, Loc.T(me.hidden != null ? "hud.noise_hidden" : me.noise > .5f ? "hud.noise_loud" : me.moving ? "hud.noise_quiet" : "hud.noise_stopped"));
            }
            if (plist.style.display == DisplayStyle.None) return;
            string sig = string.Join("|", G.ents.Select(e => $"{e.pid},{e.role},{e.outOfRound},{e.hidden != null},{e.moving},{e.sneak},{e == h},{e.score},{Kit.Pack(e.k)}")) + me?.pid + me?.role + G.ph + tv + Loc.Current;
            if (sig == plSig) return; plSig = sig;
            var hostP = app.Online ? app.FindHost() : null; string hostId = hostP != null ? hostP.id : app.Online ? null : app.myId;
            plRows.Clear();
            foreach (var e in G.ents)
            {
                string st = Loc.T(e.IsUnice ? "hud.st_unice" : e.outOfRound ? "hud.st_out" : tv ? (e.hidden != null ? "hud.st_hidden" : e.moving ? (e.sneak ? "hud.st_sneaking" : "hud.st_running") : "hud.st_still") : "hud.st_somewhere");
                var row = Ig(E(plRows, "plrow")); if (e.outOfRound) row.AddToClassList("plout");
                var d = new Doll(24); if (e.IsUnice && live) d.SetUnice(); else d.Set(e.k); row.Add(d);
                var col = Ig(E(row, "grow")); col.style.marginLeft = 8;
                var nl = Ig(E(col, "row"));
                Ig(L(nl, e.name + (e == me ? Loc.T("hud.you_suffix") : ""), "plname"));
                if (e.bot) Ig(L(nl, Loc.T("hud.bot_tag"), "tag"));
                if (app.Online && e.pid == hostId) Ig(L(nl, Loc.T("hud.host_tag"), "tag"));
                Ig(L(col, st, "plst"));
                if (tv && e == h) { var di = Ig(E(row)); di.style.width = 18; di.style.height = 24; di.style.backgroundImage = new StyleBackground(Art.Centered("doll", 1)); }
                Ig(L(row, e.score.ToString(), "pts")).style.marginLeft = 8;
            }
        }

        void DrawMini(MeshGenerationContext mgc)
        {
            var r = mini.contentRect; float w = r.width, h = r.height; if (w < 2 || h < 2) return;
            float s = Mathf.Min(w / Map.WorldW, h / Map.WorldH), ox = (w - Map.WorldW * s) / 2, oy = (h - Map.WorldH * s) / 2;
            var p = mgc.painter2D;
            Vector2 Pt(float x, float y) => new Vector2(ox + x * s, oy + y * s);
            void Box(float x, float y, float bw, float bh, Color c)
            {
                p.fillColor = c; p.BeginPath(); p.MoveTo(Pt(x, y)); p.LineTo(Pt(x + bw, y)); p.LineTo(Pt(x + bw, y + bh)); p.LineTo(Pt(x, y + bh)); p.ClosePath(); p.Fill();
            }
            void Dot(float x, float y, float rad, Color? fill, Color? ring)
            {
                p.BeginPath(); p.Arc(Pt(x, y), rad * s, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath();
                if (fill.HasValue) { p.fillColor = fill.Value; p.Fill(); }
                if (ring.HasValue) { p.strokeColor = ring.Value; p.lineWidth = Mathf.Max(1, 12 * s); p.Stroke(); }
            }
            Box(14, 14, Map.WorldW - 28, Map.WorldH - 28, new Color32(42, 28, 36, 255));
            foreach (var rm in Map.Rooms) Box(rm.x, rm.y, rm.w, rm.h, new Color32(74, 53, 39, 255));
            foreach (var d in Map.Doors) Box(d.r.x, d.r.y, d.r.w, d.r.h, new Color32(74, 53, 39, 255));
            foreach (var f in Map.Furniture) Box(f.x, f.y, f.w, f.h, new Color32(109, 74, 51, 255));
            if (!G.Live) return;
            var me = app.me; float t = Time.unscaledTime;
            if (me != null && me.IsUnice) { if (G.doll.hasLast && G.t - G.doll.lastT < 14) Dot(G.doll.lastX, G.doll.lastY, 40 + Mathf.Sin(t * 6) * 10, null, Rose); }
            else if (me == null || me.outOfRound || me.hidden == null)
                foreach (var e in G.ents)
                {
                    if (e.IsUnice || e.outOfRound || e == me || e.x <= 0) continue;
                    var kc = Art.Hex(Kit.OutfitColor(e.k));
                    Dot(e.x, e.y, 22, e.hidden != null ? (Color?)null : kc, e.hidden != null ? kc : (Color?)null);
                    if (e == G.doll.holder) Dot(e.x, e.y, 42, null, Rose);
                }
            if (me != null && !me.outOfRound && me.x > 0)
            {
                Dot(me.x, me.y, 28, me.IsUnice ? (Color)new Color32(221, 214, 223, 255) : Color.white, me.IsUnice ? Rose : Art.Hex(Kit.OutfitColor(me.k)));
                if (!me.IsUnice && G.doll.holder == me) Dot(me.x, me.y, 46, null, Rose);
            }
        }

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

        // ---------- touch ----------
        VisualElement joyZone, joyBase, joyKnob, actE, actQ, actS, actR;
        public Vector2 Joy { get; private set; }
        public bool SneakOn { get; private set; }
        int joyPointer = -1; Vector2 joyCenter;

        void BuildTouch()
        {
            joyZone = E(touch); joyZone.style.position = Position.Absolute; joyZone.style.left = 0; joyZone.style.bottom = 0;
            joyZone.style.width = Length.Percent(45); joyZone.style.height = Length.Percent(70);
            joyBase = Ig(E(joyZone, "joy")); joyKnob = Ig(E(joyBase, "joy-knob"));
            joyZone.RegisterCallback<PointerDownEvent>(e =>
            {
                if (joyPointer >= 0) return;
                joyPointer = e.pointerId; joyZone.CapturePointer(e.pointerId);
                var lp = (Vector2)e.localPosition; joyCenter = lp;
                joyBase.style.left = lp.x - 102; joyBase.style.bottom = StyleKeyword.Auto; joyBase.style.top = lp.y - 102;
                MoveJoy(lp);
            });
            joyZone.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == joyPointer) MoveJoy(e.localPosition); });
            void End(int id) { if (id != joyPointer) return; if (joyZone.HasPointerCapture(id)) joyZone.ReleasePointer(id); joyPointer = -1; ResetJoy(); }
            joyZone.RegisterCallback<PointerUpEvent>(e => End(e.pointerId));
            joyZone.RegisterCallback<PointerCancelEvent>(e => End(e.pointerId));
            joyZone.RegisterCallback<PointerCaptureOutEvent>(e => { joyPointer = -1; ResetJoy(); });

            var acts = E(touch, "acts");
            actS = ActBtn(acts, "sneak", "act-s", () => { SneakOn = !SneakOn; actS.EnableInClassList("act-on", SneakOn); });
            actR = ActBtn(acts, "laugh", "act-r", () => app.TryLaugh());
            actQ = ActBtn(acts, "pass", "act-q", () => app.TryPass());
            actE = ActBtn(acts, "door", null, () => app.DoAct());
            actEIcon = actE.Q(className: "act-icon");
            actEIcon.generateVisualContent = mgc => DrawIcon(mgc, actEIconKind, actE.resolvedStyle.color);
            ResetJoy();
        }

        /// <summary>A round touch button drawn as a simple vector icon (language-neutral) instead of text.</summary>
        VisualElement ActBtn(VisualElement p, string iconKind, string cls, Action down)
        {
            var b = E(p, "act"); if (cls != null) b.AddToClassList(cls);
            var icon = Ig(E(b, "act-icon"));
            icon.generateVisualContent += mgc => DrawIcon(mgc, iconKind, b.resolvedStyle.color);
            b.RegisterCallback<PointerDownEvent>(e => { down(); e.StopPropagation(); });
            return b;
        }

        VisualElement actEIcon; string actEIconKind = "door";
        void SetActEIcon(string kind) { if (actEIconKind == kind) return; actEIconKind = kind; actEIcon.MarkDirtyRepaint(); }

        static void DrawIcon(MeshGenerationContext mgc, string kind, Color col)
        {
            var p = mgc.painter2D;
            var r = mgc.visualElement.contentRect;
            float cx = r.width / 2, cy = r.height / 2, s = Mathf.Min(r.width, r.height);
            p.strokeColor = col; p.fillColor = col; p.lineWidth = s * .08f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            void Dot(Vector2 c, float rad) { p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); }
            switch (kind)
            {
                case "sneak":
                    foreach (var dx in new[] { -1f, 1f }) Dot(new Vector2(cx + dx * s * .14f, cy - Mathf.Abs(dx) * s * .08f), s * .11f);
                    break;
                case "laugh":
                    p.BeginPath(); p.Arc(new Vector2(cx, cy - s * .02f), s * .22f, Angle.Degrees(15), Angle.Degrees(165)); p.Stroke();
                    foreach (var dx in new[] { -1f, 1f }) Dot(new Vector2(cx + dx * s * .16f, cy - s * .2f), s * .035f);
                    break;
                case "pass":
                    p.BeginPath(); p.MoveTo(new Vector2(cx - s * .22f, cy)); p.LineTo(new Vector2(cx + s * .16f, cy)); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(cx + s * .04f, cy - s * .15f)); p.LineTo(new Vector2(cx + s * .22f, cy)); p.LineTo(new Vector2(cx + s * .04f, cy + s * .15f)); p.Stroke();
                    break;
                case "door":
                    p.BeginPath(); p.MoveTo(new Vector2(cx - s * .15f, cy + s * .22f)); p.LineTo(new Vector2(cx - s * .15f, cy - s * .22f)); p.LineTo(new Vector2(cx + s * .15f, cy - s * .22f)); p.LineTo(new Vector2(cx + s * .15f, cy + s * .22f)); p.Stroke();
                    Dot(new Vector2(cx + s * .07f, cy), s * .025f);
                    break;
                case "eye":
                    p.BeginPath(); p.MoveTo(new Vector2(cx - s * .24f, cy));
                    p.BezierCurveTo(new Vector2(cx - s * .1f, cy - s * .18f), new Vector2(cx + s * .1f, cy - s * .18f), new Vector2(cx + s * .24f, cy));
                    p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(cx - s * .24f, cy));
                    p.BezierCurveTo(new Vector2(cx - s * .1f, cy + s * .18f), new Vector2(cx + s * .1f, cy + s * .18f), new Vector2(cx + s * .24f, cy));
                    p.Stroke();
                    Dot(new Vector2(cx, cy), s * .07f);
                    break;
            }
        }

        void MoveJoy(Vector2 lp)
        {
            var d = lp - joyCenter; float m = d.magnitude; const float R = 84;
            if (m > R) d *= R / m;
            joyKnob.style.translate = new Translate(d.x, d.y);
            Joy = d / R;
        }
        void ResetJoy()
        {
            Joy = Vector2.zero; joyKnob.style.translate = new Translate(0, 0);
            joyBase.style.left = 36; joyBase.style.top = StyleKeyword.Auto; joyBase.style.bottom = 30;
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
            ApplySafeArea();
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

        void ApplySafeArea()
        {
            var sa = Screen.safeArea; if (sa == lastSafe || Screen.height < 1) return; lastSafe = sa;
            float k = root.layout.height > 0 ? root.layout.height / Screen.height : 720f / Screen.height;
            safe.style.left = sa.xMin * k; safe.style.right = (Screen.width - sa.xMax) * k;
            safe.style.top = (Screen.height - sa.yMax) * k; safe.style.bottom = sa.yMin * k;
            foreach (var el in new[] { home, wait, resultOv }) { el.style.left = safe.style.left; el.style.right = safe.style.right; el.style.top = safe.style.top; el.style.bottom = safe.style.bottom; }
        }
    }

    /// <summary>A child's look for menus and lists: the same tinted layers as in the game.</summary>
    public sealed class Doll : VisualElement
    {
        readonly VisualElement[] ls = new VisualElement[14];
        readonly (string file, Color tint)[] slots = new (string, Color)[14];

        public Doll(float width)
        {
            pickingMode = PickingMode.Ignore;
            style.width = width; style.height = width * 68 / 52; style.flexShrink = 0;
            for (int i = 0; i < ls.Length; i++)
            {
                var l = new VisualElement { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute; l.style.left = 0; l.style.top = 0; l.style.right = 0; l.style.bottom = 0;
                ls[i] = l; Add(l);
            }
        }

        public void Set(int[] k, bool back = false, int frame = 0, bool flip = false)
        {
            EntView.KidLayers(k, back, frame, slots);
            for (int i = 0; i < ls.Length; i++)
            {
                var (file, tint) = slots[i];
                var sp = file != null ? Art.Char(file) : null;
                ls[i].style.display = sp != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (sp == null) continue;
                ls[i].style.backgroundImage = new StyleBackground(sp);
                ls[i].style.unityBackgroundImageTintColor = tint;
            }
            style.scale = new Scale(new Vector3(flip ? -1 : 1, 1, 1));
        }

        public void SetUnice()
        {
            for (int i = 1; i < ls.Length; i++) ls[i].style.display = DisplayStyle.None;
            ls[0].style.display = DisplayStyle.Flex;
            ls[0].style.backgroundImage = new StyleBackground(Art.UniceFrame("unice_i_s_0"));
            ls[0].style.unityBackgroundImageTintColor = Color.white;
        }
    }
}
