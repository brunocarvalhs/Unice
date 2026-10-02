using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>The waiting room (full screen, made for a phone on its side): players, match options for the host, appearance.</summary>
    sealed class LobbyScreen : UiPart
    {
        public readonly VisualElement Root;
        VisualElement wList, hostOpts, sidePartida, sideAparencia, sideTabPartida, sideTabAparencia;
        Label wTitle, wCode, wNote, wCount, wStatus, optsInfo, botsVal, roundsVal, hideVal, huntVal, giggleVal, mapVal;
        Button copyBtn, actBtn, leave2; string wSig, sideTab = "tab.match"; bool lobbyHost, waitCanGo;
        static readonly int[] RoundChoices = { 3, 5, 7 };
        // Quintal/praça are a work in progress (flat-tint placeholder art) — only selectable in the Editor or a dev build.
        static readonly string[] PlayableMapIds = Debug.isDebugBuild ? Maps.AllIds : new[] { MapHouse.Id };

        public LobbyScreen(Ui ui) : base(ui)
        {
            Root = E(ui.root, "lobby");
            var top = E(Root, "lobby-top");
            leave2 = B(top, "", () => app.LeaveRoom(), "mbtn", "mbtn-top");
            var head = E(top, "lobby-head");
            wTitle = L(head, "", "lobby-title"); wCode = L(head, "", "lobby-code");
            copyBtn = BB(head, "lobby.copy_code", CopyCode, "mpill");
            ui.MicBtn(top, "mpill");
            ui.WhisperBtn(top, "mpill");
            wCount = L(top, "", "lobby-count");
            var body = E(Root, "lobby-body");
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
            mapVal = Stepper(hostOpts, "lobby.map_label", d => { int i = Array.IndexOf(PlayableMapIds, G.mapId); G.mapId = PlayableMapIds[((i < 0 ? 0 : i) + d + PlayableMapIds.Length) % PlayableMapIds.Length]; wSig = null; });
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
            wNote = L(Root, "", "lobby-note");
            RefreshSideTab();
        }

        /// <summary>Forces the next <see cref="Render"/> to rebuild (it skips when nothing it shows changed).</summary>
        public void Invalidate() => wSig = null;

        public void OnShow(string was) { if (was == "final") app.rdy = false; wSig = null; }

        void RefreshSideTab()
        {
            Show(sidePartida, sideTab == "tab.match"); Show(sideAparencia, sideTab == "tab.appearance");
            sideTabPartida.EnableInClassList("tabbtn-on", sideTab == "tab.match");
            sideTabAparencia.EnableInClassList("tabbtn-on", sideTab == "tab.appearance");
            if (sideTab == "tab.appearance") ui.kid.ShowIn(sideAparencia);
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

        public void Render()
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
    }
}
