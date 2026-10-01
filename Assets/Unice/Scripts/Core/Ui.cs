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
            labels = Ig(E(root, "layer"));
            cuesEl = Ig(E(root, "layer")); cuesEl.generateVisualContent += DrawCues;
            safe = Ig(E(root, "layer"));
            hud = Ig(E(safe, "layer")); BuildHud();
            touch = Ig(E(safe, "layer")); BuildTouch();
            BuildMessages();
            BuildHome(); BuildWait(); BuildResult(); BuildPause();
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

        public bool Typing
        {
            get
            {
                var f = root.panel?.focusController?.focusedElement as VisualElement;
                return f != null && (f is TextField || f.GetFirstAncestorOfType<TextField>() != null);
            }
        }

        // ---------- home ----------
        VisualElement home; TextField nameIn, codeIn; Label netLbl; Button createBtn, joinBtn, soundBtn, soloBtn;

        void BuildHome()
        {
            var sh = Sheet(root, out home);
            L(sh, "ESCONDE-ESCONDE ONLINE · 2 A 8 JOGADORES", "eyebrow");
            L(sh, "Unice", "title");
            L(sh, "A Unice é a velha dona da casa e morre de ciúmes da boneca e das coisas dela. Alguém vira a Unice e sai caçando quem pegou a boneca. Os outros se escondem e protegem quem está com ela. O problema é que a boneca ri.", "lede");
            var cols = E(sh, "cols");
            var c1 = E(cols, "col");
            L(c1, "SEU NOME", "lbl");
            nameIn = Field(c1, app.myName, 12);
            nameIn.RegisterValueChangedCallback(e => app.myName = U.CleanName(e.newValue));
            createBtn = B(c1, "Criar sala", () => app.EnterRoom(true, null), "btn"); createBtn.style.marginTop = 10;
            L(c1, "Você recebe um código para mandar aos amigos.", "muted");
            var c2 = E(cols, "col"); c2.style.marginRight = 0;
            L(c2, "CÓDIGO DA SALA", "lbl");
            var r = E(c2, "row");
            codeIn = Field(r, "", 10, "code-field", "grow");
            joinBtn = B(r, "Entrar", JoinFromInput, "btn"); joinBtn.style.marginLeft = 10;
            netLbl = L(sh, "", "net");
            var rules = E(sh, "rules");
            void Rule(string t, string b, string cls) { var x = E(rules, "rule"); if (cls != null) x.AddToClassList(cls); L(x, t, "rule-t"); L(x, b, "rule-b"); }
            Rule("O time vence", "se o relógio zerar e a Unice não tiver achado quem está com a boneca.", "rule-green");
            Rule("A Unice vence", "se pegar a boneca de volta. Pegar quem não está com ela só tira a pessoa da rodada.", "rule-red");
            Rule("30 segundos", "para se esconder antes de a Unice chegar em casa pela porta da frente. Escute a bengala.", null);
            Rule("No escuro", "No esconderijo você enxerga só um pouquinho em volta, e ouve o resto: passos, rangidos e risadas.", null);
            Rule("A boneca ri", "a cada 10 s escondida e entrega o lugar. Troque de esconderijo ou passe a boneca para alguém bem perto.", "rule-red");
            Rule("Rodízio", "A primeira Unice é sorteada. Quem é pego com a boneca vira a próxima.", null);
            var foot = E(sh, "row", "gap", "wrap");
            soloBtn = B(foot, "Jogar sozinho com robôs", () => app.PlaySolo(), "btn", "btn-ghost");
            soundBtn = B(foot, "Som: ligado", ToggleSound, "btn", "btn-link");
        }

        void JoinFromInput()
        {
            var c = U.CleanCode(codeIn.value);
            if (c.Length < 4) { app.SetNet("Digite o código da sala que seu amigo mandou.", true); return; }
            app.EnterRoom(false, c);
        }

        void ToggleSound()
        {
            app.sfx.On = !app.sfx.On;
            string t = app.sfx.On ? "Som: ligado" : "Som: desligado";
            soundBtn.text = t; pauseSound.text = t;
        }

        // ---------- waiting room ----------
        VisualElement wait, wList, hostOpts, opts; Label wEyebrow, wTitle, wCode, wNote, wCount, wStatus, optsInfo, botsVal, roundsVal;
        Button copyBtn, readyBtn, goBtn, leave2; Doll preview; string wSig;
        static readonly int[] RoundChoices = { 3, 5, 7 };

        void BuildWait()
        {
            var sh = Sheet(root, out wait);
            wEyebrow = L(sh, "", "eyebrow");
            var tr = E(sh, "row", "wrap");
            wTitle = L(tr, "Sala ", "title", "title-md"); wCode = L(tr, "", "title", "title-md", "code");
            copyBtn = B(tr, "Copiar código", CopyCode, "btn", "btn-ghost"); copyBtn.style.marginLeft = 16;
            wNote = L(sh, "", "muted");
            var cols = E(sh, "cols", "gap");
            var c1 = E(cols, "col");
            var lr = E(c1, "row"); L(lr, "JOGADORES", "lbl", "grow"); wCount = L(lr, "", "lbl");
            wList = E(c1);
            hostOpts = E(c1, "row", "gap", "wrap");
            botsVal = Stepper(hostOpts, "ROBÔS", d => { int max = Math.Max(0, Rules.MaxPlayers - app.LobbyHumans().Count); G.bots = Mathf.Clamp(Math.Min(G.bots, max) + d, 0, max); wSig = null; });
            roundsVal = Stepper(hostOpts, "RODADAS", d => { int i = Array.IndexOf(RoundChoices, G.rounds); G.rounds = RoundChoices[Mathf.Clamp((i < 0 ? 1 : i) + d, 0, RoundChoices.Length - 1)]; wSig = null; });
            optsInfo = L(c1, "", "muted");
            var br = E(c1, "row", "gap", "wrap");
            readyBtn = B(br, "Estou pronto", () => { app.ToggleReady(); wSig = null; }, "btn");
            goBtn = B(br, "Começar partida", () =>
            {
                int bots = Math.Min(G.bots, Math.Max(0, Rules.MaxPlayers - app.LobbyHumans().Count));
                app.StartMatch(bots, G.rounds);
            }, "btn");
            wStatus = L(c1, "", "muted"); wStatus.style.marginTop = 8;
            leave2 = B(c1, "Sair da sala", () => app.LeaveRoom(), "btn", "btn-link");

            var c2 = E(cols, "col"); c2.style.marginRight = 0;
            L(c2, "SUA CRIANÇA", "lbl");
            var pr = E(c2, "row");
            var pv = E(pr, "preview"); pv.style.alignItems = Align.Center; pv.style.justifyContent = Justify.Center;
            preview = new Doll(100); pv.Add(preview);
            B(pr, "Sortear visual", () => { app.SetKit(Kit.Random()); RenderOpts(); wSig = null; }, "btn", "btn-ghost");
            opts = E(c2); opts.style.marginTop = 10;
            BuildOpts();
        }

        Label Stepper(VisualElement p, string label, Action<int> change)
        {
            var s = E(p, "stepper");
            L(s, label, "lbl");
            B(s, "‹", () => change(-1), "step-btn");
            var v = L(s, "", "step-val");
            B(s, "›", () => change(1), "step-btn");
            return v;
        }

        void CopyCode()
        {
            GUIUtility.systemCopyBuffer = app.code;
            copyBtn.text = "Copiado!";
            copyBtn.schedule.Execute(() => copyBtn.text = "Copiar código").ExecuteLater(1800);
        }

        static readonly (string label, int slot, string[] list, bool color)[] OptRows =
        {
            ("PELE", 0, Kit.Skins, true), ("CABELO", 1, Kit.Hairs, false), ("COR DO CABELO", 2, Kit.HairColors, true), ("ROUPA", 3, Kit.Outfits, false),
            ("COR DA ROUPA", 4, Kit.OutfitColors, true), ("CHAPÉU", 5, Kit.Hats, false), ("ACESSÓRIO", 6, Kit.Accessories, false)
        };
        readonly List<(int slot, int v, VisualElement el)> swatches = new List<(int, int, VisualElement)>();
        readonly Dictionary<int, Label> cycleVals = new Dictionary<int, Label>();

        void BuildOpts()
        {
            foreach (var (label, slot, list, color) in OptRows)
            {
                var row = E(opts, "opt");
                L(row, label, "opt-l");
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
            }
            RenderOpts();
        }

        void RenderOpts()
        {
            foreach (var (slot, v, el) in swatches) el.EnableInClassList("sw-on", app.kit[slot] == v);
            foreach (var (label, slot, list, color) in OptRows) if (!color) cycleVals[slot].text = list[app.kit[slot]];
        }

        void RenderWait()
        {
            var hs = app.LobbyHumans(); var host = app.Online ? app.FindHost() : null; string hostId = host != null ? host.id : app.Online ? null : app.myId;
            int maxBots = Math.Max(0, Rules.MaxPlayers - hs.Count), bots = Math.Min(G.bots, maxBots);
            bool allReady = hs.Where(h => h.id != hostId).All(h => h.rdy);
            string sig = string.Join("|", hs.Select(h => h.id + h.name + Kit.Pack(h.k) + h.rdy)) + hostId + G.isHost + bots + G.rounds + app.rdy + app.code;
            if (sig == wSig) return; wSig = sig;
            bool on = app.Online;
            Txt(wCode, on ? app.code : ""); Txt(wTitle, on ? "Sala " : "Jogo sozinho"); Show(copyBtn, on);
            Txt(wEyebrow, on ? "SALA DE ESPERA" : "VOCÊ E OS ROBÔS");
            Txt(wNote, on ? "Mande o código para seus amigos. Eles abrem o jogo, digitam o código em \"Código da sala\" e tocam em Entrar." : "Monte sua criança e escolha quantos robôs vão jogar.");
            Txt(wCount, $"{hs.Count + bots}/{Rules.MaxPlayers}");
            wList.Clear();
            foreach (var h in hs)
            {
                var row = E(wList, "prow"); if (h.me) row.AddToClassList("prow-me");
                var d = new Doll(34); d.Set(h.k); row.Add(d);
                L(row, h.name + (h.me ? " (você)" : ""), "pname");
                var st = L(row, h.id == hostId ? "ANFITRIÃO" : h.rdy ? "PRONTO" : "ESCOLHENDO", "pstate");
                if (h.id == hostId || h.rdy) st.AddToClassList("pstate-ok");
            }
            if (bots > 0) L(E(wList, "prow", "prow-bots"), $"+ {bots} robô{(bots > 1 ? "s" : "")} para completar");
            Txt(botsVal, bots.ToString()); Txt(roundsVal, G.rounds.ToString());
            Show(hostOpts, G.isHost);
            Txt(optsInfo, G.isHost ? "" : $"Robôs: {bots} · Rodadas: {G.rounds}. O anfitrião escolhe.");
            Show(readyBtn, !G.isHost); readyBtn.text = app.rdy ? "Não estou pronto" : "Estou pronto";
            Show(goBtn, G.isHost); goBtn.SetEnabled(allReady && hs.Count + bots >= 2);
            int waiting = hs.Count(h => h.id != hostId && !h.rdy);
            Txt(wStatus, G.isHost ? (allReady ? (hs.Count + bots < 2 ? "Adicione pelo menos um robô." : "Todos prontos!") : $"Esperando {waiting} jogador(es) ficar pronto.")
                                  : "O anfitrião começa a partida quando todos estiverem prontos.");
            leave2.text = on ? "Sair da sala" : "Voltar";
        }

        // ---------- result / final ----------
        VisualElement resultOv, sheet; Label nextIn; Button againBtn; Label waitHost;

        void BuildResult() { sheet = Sheet(root, out resultOv); }

        void RenderResult(bool final)
        {
            sheet.Clear(); nextIn = null; againBtn = null; waitHost = null;
            var res = G.res ?? new[] { 0, -1, -1, -1 };
            bool unWin = res[0] == 1; var who = G.EntAt(res[1]); var Un = G.unice; var me = app.me; var nx = G.EntAt(res[3]);
            bool? won = me != null ? unWin == me.IsUnice : (bool?)null;
            string title = unWin ? (Un == me ? "Sua boneca voltou para você!" : "A Unice pegou a boneca de volta") : (Un == me ? "O tempo acabou" : "O time venceu!");
            var spot = res[2] >= 0 && res[2] < Map.Spots.Length ? Map.Spots[res[2]] : null;
            string reason = who == null ? "" : unWin ? $"{app.NameUp(Un)} tomou a boneca de {app.NameOf(who)} {Map.RoomOf(who.x, who.y).inPhrase}."
                                                     : $"A boneca terminou com {app.NameOf(who)}{(spot != null ? " " + spot.spot : "")}.";
            if (final)
            {
                int top = G.ents.Count > 0 ? G.ents.Max(e => e.score) : 0; var ws = G.ents.Where(e => e.score == top).ToList();
                bool iWon = me != null && ws.Contains(me);
                string head = iWon ? (ws.Count > 1 ? "Empate no topo!" : "Você venceu a partida!") : $"{string.Join(" e ", ws.Select(w => w.name))} {(ws.Count > 1 ? "venceram" : "venceu")}";
                L(sheet, $"FIM DAS {G.rounds} RODADAS", "eyebrow");
                L(sheet, head, "title", "title-md", iWon ? "title-win" : "title-red");
                L(sheet, $"Última rodada: {title} {reason}", "muted");
                Scores(me);
                var foot = E(sheet, "row", "gap", "wrap");
                againBtn = B(foot, "Nova partida", () => app.NewMatchLobby(), "btn");
                waitHost = L(foot, "Esperando o anfitrião começar uma nova partida.", "muted");
                B(foot, app.Online ? "Sair da sala" : "Voltar ao início", () => app.LeaveRoom(), "btn", "btn-link");
            }
            else
            {
                L(sheet, $"RODADA {G.r} DE {G.rounds}", "eyebrow");
                var t = L(sheet, title, "title", "title-md"); if (won != null) t.AddToClassList(won.Value ? "title-win" : "title-red");
                L(sheet, reason, "lede");
                Scores(me);
                var nr = E(sheet, "next");
                var d = new Doll(30); d.SetUnice(); nr.Add(d);
                L(nr, $"Próxima Unice: {app.NameOf(nx)}", "pname");
                L(nr, unWin ? "Quem é pego com a boneca vira a Unice." : "O time venceu, então houve sorteio.", "muted");
                var nrow = E(sheet, "row", "wrap");
                L(nrow, "Próxima rodada em ", "muted"); nextIn = L(nrow, "", "muted", "bold"); L(nrow, " s.", "muted");
                L(sheet, "Unice: +3 ao achar a boneca, +1 por captura. Time: +2 cada se o tempo acabar, +1 para quem termina com a boneca.", "muted");
            }
        }

        void Scores(Ent me)
        {
            int i = 0;
            foreach (var e in G.ents.OrderByDescending(e => e.score))
            {
                var row = E(sheet, "srow"); if (e == me) row.AddToClassList("prow-me");
                L(row, (++i).ToString(), "srank");
                var d = new Doll(26); d.Set(e.k); row.Add(d);
                L(row, e.name + (e == me ? " (você)" : "") + (e.bot ? " · robô" : ""), "pname");
                L(row, e.score.ToString(), "pts");
            }
        }

        // ---------- pause / menu ----------
        VisualElement pauseOv; Label pauseTitle, pauseNote; Button pauseSound, pauseLeave;

        void BuildPause()
        {
            var sh = Sheet(root, out pauseOv);
            pauseTitle = L(sh, "Pausado", "title", "title-md");
            pauseNote = L(sh, "", "muted");
            var r = E(sh, "row", "gap", "wrap");
            B(r, "Continuar", () => app.TogglePause(), "btn");
            pauseSound = B(r, "Som: ligado", ToggleSound, "btn", "btn-ghost");
            pauseLeave = B(r, "Sair", () => app.LeaveRoom(), "btn", "btn-link");
            Show(pauseOv, false);
        }

        public void ShowPause(bool on, bool online)
        {
            Show(pauseOv, on);
            pauseTitle.text = online ? "Menu" : "Pausado";
            pauseNote.text = online ? "A partida continua para os outros enquanto este menu está aberto." : "A partida está parada.";
            pauseLeave.text = online ? "Sair da sala" : "Sair da partida";
        }

        // ---------- HUD ----------
        Label hRound, hClock, hPhase, hWhere, hRoom, hRole, hGoal, hDollWho, hDollNote, hNoiseTxt;
        VisualElement roleCard, gigWrap, gigFill, noiseBox, noiseFill, plist, plRows, mini;
        string plSig;

        void BuildHud()
        {
            var tl = E(hud, "hud-card", "hud-tl"); Ig(tl);
            var rr = E(tl, "row"); L(rr, "RODADA ", "hud-lbl"); hRound = L(rr, "", "hud-lbl");
            hClock = L(tl, "0:30", "clock"); hPhase = L(tl, "", "phase"); hWhere = L(tl, "", "where"); hRoom = L(tl, "", "roomline");

            roleCard = E(hud, "hud-card", "hud-tr"); Ig(roleCard);
            hRole = L(roleCard, "", "role-t"); hGoal = L(roleCard, "", "role-g");
            var dr = E(roleCard, "row"); dr.style.marginTop = 8; L(dr, "A BONECA ", "hud-lbl"); hDollWho = L(dr, "", "hud-lbl");
            gigWrap = E(roleCard, "meter"); gigFill = E(gigWrap, "meter-fill");
            hDollNote = L(roleCard, "", "note");
            noiseBox = E(roleCard); noiseBox.style.marginTop = 8;
            var nr = E(noiseBox, "row"); L(nr, "SEU BARULHO ", "hud-lbl"); hNoiseTxt = L(nr, "", "hud-lbl");
            var nm = E(noiseBox, "meter", "meter-noise"); noiseFill = E(nm, "meter-fill");
            foreach (var l in roleCard.Query<Label>().ToList()) Ig(l);
            foreach (var l in tl.Query<Label>().ToList()) Ig(l);

            var btns = E(hud, "hud-btns"); Ig(btns);
            B(btns, "Mapa", () => { Show(plist, plist.style.display == DisplayStyle.None); plSig = null; }, "hud-btn");
            B(btns, "Menu", () => app.TogglePause(), "hud-btn");
            plist = E(hud, "hud-card", "plist"); Ig(plist);
            L(plist, "PLANTA DA CASA", "hud-lbl");
            mini = Ig(E(plist, "mini")); mini.generateVisualContent += DrawMini;
            L(plist, "JOGADORES", "hud-lbl").style.marginTop = 8;
            plRows = Ig(E(plist));
            Show(plist, false);
        }

        void UpdateHud()
        {
            var me = app.me; var Un = G.unice; var h = G.doll.holder; bool tv = app.TeamView, live = G.Live;
            Txt(hRound, $"{G.r}/{G.rounds}");
            Txt(hClock, U.Mmss(live ? G.tl : 0));
            hClock.EnableInClassList("clock-low", G.ph == Phase.Hunt && G.tl <= 15);
            Txt(hPhase, G.ph == Phase.Hide ? "Hora de se esconder" : G.ph == Phase.Hunt ? (Un == me ? "Você está procurando" : "A Unice está procurando") : G.ph == Phase.Result ? "Rodada encerrada" : "");
            Txt(hWhere, me != null && !me.outOfRound && me.x > 0 && live ? $"Você está {Map.RoomOf(me.x, me.y).inPhrase}" : "");
            Txt(hRoom, app.Online ? $"Sala {app.code}{(G.isHost ? " · você é o anfitrião" : "")}" : "Modo sozinho");
            string role, goal, cls = "";
            if (me == null) { role = "Assistindo"; goal = "Você entra na próxima rodada."; }
            else if (me.IsUnice) { role = "Você é a Unice"; cls = "role-unice"; goal = G.ph == Phase.Hide ? "Pegaram sua boneca e estão se escondendo. Você chega pela porta da frente quando o tempo acabar." : "Ache quem está com a sua boneca. Encostar pega; o botão Olhar abre esconderijos."; }
            else if (me.outOfRound) { role = "A Unice te pegou"; cls = "role-out"; goal = "Você saiu desta rodada. A câmera acompanha a boneca."; }
            else if (h == me) { role = "A boneca está com você"; cls = "role-holder"; goal = $"Escondida, ela ri a cada {Rules.GiggleEvery:0} s: mude de lugar ou passe para alguém perto."; }
            else { role = "Você se esconde"; goal = me.hidden != null ? "No esconderijo você vê pouco. Preste atenção nos sons." : "Fuja da Unice e proteja quem está com a boneca."; }
            Txt(hRole, role); Txt(hGoal, goal);
            foreach (var c in new[] { "role-unice", "role-out", "role-holder" }) roleCard.EnableInClassList(c, c == cls);
            float pct = 0; string note;
            if (h != null && tv)
            {
                Txt(hDollWho, h == me ? "COM VOCÊ" : "COM " + h.name.ToUpperInvariant());
                pct = G.ph == Phase.Hunt ? 1 - G.doll.giggleT / Rules.GiggleEvery : 0;
                note = G.ph == Phase.Hide ? "Ela só começa a rir quando a Unice entrar." : h.hidden != null ? $"Escondida {h.hidden.spot}: risadinha em {Mathf.CeilToInt(G.doll.giggleT)} s." : "Fora do esconderijo ela fica quieta, mas quem carrega pode ser visto.";
                if (h == me && me.passCd > 0) note += $" Você pode repassar em {Mathf.CeilToInt(me.passCd)} s.";
                Show(gigWrap, true);
            }
            else
            {
                Txt(hDollWho, h != null ? "ESCONDIDA" : "");
                note = G.doll.hasLast ? $"Última risadinha {Map.RoomOf(G.doll.lastX, G.doll.lastY).inPhrase}, há {Mathf.Max(0, Mathf.FloorToInt(G.t - G.doll.lastT))} s." : $"Com alguém escondido, ela ri a cada {Rules.GiggleEvery:0} s.";
                Show(gigWrap, false);
            }
            gigFill.style.width = Length.Percent(Mathf.Clamp01(pct) * 100);
            Txt(hDollNote, note);
            Show(noiseBox, me != null && !me.outOfRound && !me.IsUnice);
            if (me != null)
            {
                noiseFill.style.width = Length.Percent(Mathf.Clamp01(me.noise) * 100);
                Txt(hNoiseTxt, me.hidden != null ? "ESCONDIDO" : me.noise > .5f ? "PASSOS ALTOS" : me.moving ? "SILÊNCIO" : "PARADO");
            }
            if (plist.style.display == DisplayStyle.None) return;
            string sig = string.Join("|", G.ents.Select(e => $"{e.pid},{e.role},{e.outOfRound},{e.hidden != null},{e.moving},{e.sneak},{e == h},{e.score},{Kit.Pack(e.k)}")) + me?.pid + me?.role + G.ph + tv;
            if (sig == plSig) return; plSig = sig;
            var hostP = app.Online ? app.FindHost() : null; string hostId = hostP != null ? hostP.id : app.Online ? null : app.myId;
            plRows.Clear();
            foreach (var e in G.ents)
            {
                string st = e.IsUnice ? "é a Unice" : e.outOfRound ? "fora da rodada" : tv ? (e.hidden != null ? "no esconderijo" : e.moving ? (e.sneak ? "andando devagar" : "correndo") : "parado") : "em algum lugar";
                var row = Ig(E(plRows, "plrow")); if (e.outOfRound) row.AddToClassList("plout");
                var d = new Doll(24); if (e.IsUnice && live) d.SetUnice(); else d.Set(e.k); row.Add(d);
                var col = Ig(E(row, "grow")); col.style.marginLeft = 8;
                var nl = Ig(E(col, "row"));
                Ig(L(nl, e.name + (e == me ? " (você)" : ""), "plname"));
                if (e.bot) Ig(L(nl, "robô", "tag"));
                if (app.Online && e.pid == hostId) Ig(L(nl, "anfitrião", "tag"));
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
        Label banner, caption, pillTxt, count, countSub, prompt; VisualElement toasts, pill, captionBox, blackout, promptBox, vignette, slats, cuesEl;
        float bannerT, bannerDur, captionT;
        readonly List<(Label l, float until)> toastList = new List<(Label, float)>();

        void BuildMessages()
        {
            blackout = Ig(E(safe, "layer", "blackout"));
            count = Ig(L(blackout, "", "count")) as Label; countSub = Ig(L(blackout, "Você está voltando para casa. Eles estão se escondendo com a sua boneca…", "count-sub")) as Label;
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
        VisualElement joyZone, joyBase, joyKnob, actE, actQ, actS; Label actELbl;
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
                joyBase.style.left = lp.x - 85; joyBase.style.bottom = StyleKeyword.Auto; joyBase.style.top = lp.y - 85;
                MoveJoy(lp);
            });
            joyZone.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == joyPointer) MoveJoy(e.localPosition); });
            void End(int id) { if (id != joyPointer) return; if (joyZone.HasPointerCapture(id)) joyZone.ReleasePointer(id); joyPointer = -1; ResetJoy(); }
            joyZone.RegisterCallback<PointerUpEvent>(e => End(e.pointerId));
            joyZone.RegisterCallback<PointerCancelEvent>(e => End(e.pointerId));
            joyZone.RegisterCallback<PointerCaptureOutEvent>(e => { joyPointer = -1; ResetJoy(); });

            var acts = E(touch, "acts");
            actS = ActBtn(acts, "Devagar", "act-s", () => { SneakOn = !SneakOn; actS.EnableInClassList("act-on", SneakOn); });
            actQ = ActBtn(acts, "Passar", "act-q", () => app.TryPass());
            actE = ActBtn(acts, "Esconder", null, () => app.DoAct()); actELbl = actE.Q<Label>();
            ResetJoy();
        }

        VisualElement ActBtn(VisualElement p, string text, string cls, Action down)
        {
            var b = E(p, "act"); if (cls != null) b.AddToClassList(cls);
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            Ig(L(b, text));
            b.RegisterCallback<PointerDownEvent>(e => { down(); e.StopPropagation(); });
            return b;
        }

        void MoveJoy(Vector2 lp)
        {
            var d = lp - joyCenter; float m = d.magnitude; const float R = 70;
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
                if (want == "home") nameIn.value = app.myName;
                if (want == "wait") { if (was == "final") app.rdy = false; wSig = null; RenderOpts(); }
                if (want == "result" || want == "final") RenderResult(want == "final");
                if (want == "game") hudT = 0;
                if (want != "game") { ShowPause(false, false); app.paused = false; SneakOn = false; actS.EnableInClassList("act-on", false); }
                plSig = null;
            }
            if (want == "home")
            {
                bool ok = app.NetAvailable && !app.busy;
                createBtn.SetEnabled(ok); joinBtn.SetEnabled(ok); soloBtn.SetEnabled(!app.busy);
                Txt(netLbl, app.netText.Length > 0 ? app.netText : app.NetAvailable ? "Salas online prontas. Crie uma ou entre com um código." : "O modo online não está instalado nesta versão. Você ainda pode jogar sozinho com robôs.");
                netLbl.EnableInClassList("net-err", app.netErr && app.netText.Length > 0);
            }
            if (want == "wait") RenderWait();
            if (want == "result" && nextIn != null) Txt(nextIn, Mathf.Max(0, Mathf.CeilToInt(G.tl)).ToString());
            if (want == "final" && againBtn != null) { Show(againBtn, G.isHost); Show(waitHost, !G.isHost); }
        }

        void UpdatePrompt()
        {
            var p = app.me; var parts = new List<string>(); string eLbl = null; bool qShow = false, qOk = false;
            string K(string key) => mobile ? "" : $"<b>{key}</b> ";
            bool active = p != null && G.Live && !p.outOfRound;
            if (active)
            {
                if (p.IsUnice) { if (G.ph == Phase.Hunt && p.stun <= 0) { var s = Game.NearSpotFurn(p); if (s != null) { parts.Add($"{K("E")}Olhar {s.spot}"); eLbl = "Olhar"; } } }
                else if (p.hidden != null) { parts.Add($"{K("E")}Sair do esconderijo"); eLbl = "Sair"; }
                else
                {
                    var s = Game.NearSpotFurn(p);
                    if (s != null) { if (s.occ != null) parts.Add($"{s.occ.name} está {s.spot}"); else { parts.Add($"{K("E")}Esconder {s.spot}"); eLbl = "Esconder"; } }
                    if (G.doll.holder == p)
                    {
                        qShow = true; var m = G.PassTarget(p);
                        if (m != null) { qOk = p.passCd <= 0; parts.Add(p.passCd > 0 ? $"Passar em {Mathf.CeilToInt(p.passCd)} s" : $"{K("Q")}Passar a boneca para {m.name}"); }
                    }
                }
            }
            string txt = string.Join("    ", parts);
            if (prompt.text != txt) { prompt.text = txt; Show(promptBox, txt.Length > 0); }
            if (!mobile) return;
            Show(actE, active); Show(actQ, qShow); Show(actS, active && !p.IsUnice);
            if (active)
            {
                Txt(actELbl, eLbl ?? (p.IsUnice ? "Olhar" : "Esconder"));
                actE.style.opacity = eLbl != null ? 1 : .45f; actQ.style.opacity = qOk ? 1 : .45f;
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
                Txt(tag, Un ? (e == me ? "Unice (você)" : $"Unice · {e.name}") : e.name);
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
            if (!inGame) { vignette.style.unityBackgroundImageTintColor = new Color(.04f, .024f, .035f, .88f); vignette.style.backgroundColor = new Color(.04f, .024f, .035f, .3f); Show(vignette, true); }
            else if (app.heartI > 0 && live) { vignette.style.unityBackgroundImageTintColor = new Color(.886f, .29f, .32f, .18f + .4f * app.heartI * app.beat); vignette.style.backgroundColor = Color.clear; Show(vignette, true); }
            else Show(vignette, false);
            Show(slats, live && me != null && me.hidden != null);

            if (screen == "wait")
            {
                float t = Time.unscaledTime; var d = PrevDirs[(int)(t / 1.4f) % 4];
                preview.Set(app.kit, d.y < -.6f, 1 + Mathf.FloorToInt(t * 13 / (Mathf.PI * 2) * 6) % 6, d.x < -.3f);
            }

            // hiding phase: the Unice waits with eyes closed, the others see a countdown
            bool hidePh = G.ph == Phase.Hide; int n = Mathf.CeilToInt(G.tl);
            Show(blackout, hidePh && me != null && me.IsUnice);
            Show(pill, hidePh && (me == null || !me.IsUnice));
            if (hidePh) { Txt(count, n.ToString()); Txt(pillTxt, $"A Unice entra em {n} s"); }
        }
        bool cuesDrawn;
        static readonly Vector2[] PrevDirs = { new Vector2(.01f, 1), new Vector2(1, .2f), new Vector2(.01f, -1), new Vector2(-1, .2f) };

        void ApplySafeArea()
        {
            var sa = Screen.safeArea; if (sa == lastSafe || Screen.height < 1) return; lastSafe = sa;
            float k = root.layout.height > 0 ? root.layout.height / Screen.height : 720f / Screen.height;
            safe.style.left = sa.xMin * k; safe.style.right = (Screen.width - sa.xMax) * k;
            safe.style.top = (Screen.height - sa.yMax) * k; safe.style.bottom = sa.yMin * k;
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
