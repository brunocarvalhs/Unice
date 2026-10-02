using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unice
{
    /// <summary>Turns the match's events into what this player sees and hears (banners, toasts, sounds, ripples, the catch shake) and logs them to analytics.</summary>
    sealed class EventFeed
    {
        readonly App app;
        Game G => app.G;
        int lastEv = -1;

        public EventFeed(App app) { this.app = app; }

        /// <summary>New room or match: the next <see cref="Handle"/> catches up without replaying old events.</summary>
        public void Reset() => lastEv = -1;

        /// <summary>Plays every event newer than the last one handled. The first call after a reset only catches up.</summary>
        public void Handle()
        {
            if (lastEv < 0) { lastEv = G.evSeq; return; }
            foreach (var e in G.evs.ToArray())
                if (e.seq > lastEv) { lastEv = e.seq; try { OnEvent(e); } catch (Exception x) { Metrics.Error(x, "event " + e.type); } }
            if (G.evSeq < lastEv - 50) lastEv = G.evSeq;
        }

        string MyRole => app.me == null ? "spectator" : app.me.IsUnice ? "unice" : app.me == G.doll.holder ? "holder" : "hider";

        void OnEvent(GameEvent v)
        {
            var me = app.me; var ui = app.ui; var sfx = app.sfx; var world = app.world;
            Ent E(int i) => G.EntAt(i);
            switch (v.type)
            {
                case EvType.Start:
                {
                    var u = E(v.b); var h = E(v.c);
                    Metrics.Log("round_start", ("round", v.a), ("rounds", G.rounds), ("role", MyRole), ("online", app.Online), ("map", G.mapId));
                    if (me == null) ui.Banner(Loc.T("ev.start.watch", v.a, app.NameUp(u)), "info", 3);
                    else if (me == u) ui.Banner(Loc.T("ev.start.you_unice"), "rose", 3.2f);
                    else if (me == h) ui.Banner(Loc.T("ev.start.you_holder"), "rose", 3.2f);
                    else ui.Banner(Loc.T("ev.start.other", app.NameUp(u)), "candle", 3.2f);
                    break;
                }
                case EvType.Enter:
                    sfx.Play("boom"); app.sound.Hear(Map.DoorIn.x, Map.DoorIn.y, 1400, Loc.T("ev.enter.sound"), Ui.Rose, "creak", .35f);
                    ui.Banner(G.unice == me ? Loc.T("ev.enter.unice") : Loc.T("ev.enter.others"), "rose", 2.4f);
                    break;
                case EvType.Giggle:
                {
                    float x = v.a, y = v.b; var laugher = E(v.c); var r = Map.RoomOf(x, y);
                    bool isHolder = laugher != null && laugher == G.doll.holder;
                    world.Ripple(x, y, "giggle"); ui.Fx(x, y - 30, Loc.T("ev.giggle.fx"), Ui.Rose, 18, 1.8f, true);
                    app.sound.Hear(x, y, 2200, Loc.T("ev.giggle.sound"), Ui.Rose, "giggle", .3f);
                    if (me != null && me.IsUnice && G.Live) ui.Banner(Loc.T("ev.giggle.unice", Loc.RoomIn(r)), "rose", 2);
                    else if (laugher == me) ui.Banner(Loc.T(isHolder ? "ev.giggle.holder" : "ev.giggle.laughed"), "rose", 2.4f);
                    else if (isHolder) ui.Toast(Loc.T("ev.giggle.toast_doll", Loc.RoomIn(r)));
                    else ui.Toast(Loc.T("ev.giggle.toast_who", app.NameUp(laugher), Loc.RoomIn(r)));
                    break;
                }
                case EvType.Pass:
                {
                    var A = E(v.a); var B = E(v.b); if (A == null || B == null) break;
                    app.sound.Hear(A.x, A.y, 220, Loc.T("ev.pass.sound"), Ui.Rose, "swish");
                    if (app.TeamView || app.Seen(A.x, A.y)) ui.Fx((A.x + B.x) / 2, (A.y + B.y) / 2 - 26, Loc.T("ev.pass.fx"), Ui.Rose, 14, 1);
                    if (B == me) ui.Banner(Loc.T("ev.pass.you_got"), "rose", 2);
                    else if (A == me) ui.Banner(Loc.T("ev.pass.you_gave", B.name), "rose", 1.5f);
                    else if (app.TeamView) ui.Toast(Loc.T("ev.pass.toast", A.name, B.name));
                    break;
                }
                case EvType.Open:
                {
                    if (v.a < 0 || v.a >= Map.Spots.Length) break;
                    var s = Map.Spots[v.a];
                    app.sound.Hear(s.Cx, s.Cy, 450, Loc.T("ev.open.sound"), Ui.Candle, "creak");
                    if (v.b == 0 && app.Seen(s.Cx, s.Cy)) ui.Fx(s.ax, s.ay - 24, Loc.T("ev.open.empty"), Ui.Muted, 14, 1);
                    break;
                }
                case EvType.Catch:
                {
                    var Hh = E(v.b); var Un = E(v.a); bool withDoll = v.c != 0; float x = v.d, y = v.e; if (Hh == null) break;
                    if (Hh == me || Un == me) Metrics.Log("catch", ("side", Hh == me ? "caught" : "catcher"), ("with_doll", withDoll), ("time_left", (int)G.tl), ("room", Map.RoomOf(x, y).id));
                    sfx.Play("sting"); ui.Fx(x, y - 26, Loc.T(withDoll ? "ev.catch.mine" : "ev.catch.gotcha"), Ui.Blood, withDoll ? 24 : 20, 1.8f, true);
                    world.Ripple(x, y, "catch");
                    app.rig.Shake(withDoll ? 16f : 10f);
                    ui.Flash(new Color(.75f, .12f, .16f, withDoll ? .4f : .22f), withDoll ? .3f : .18f);
                    world.Hit(Hh.idx); if (Un != null) world.Hit(Un.idx);
                    if (Hh == me) ui.Banner(Loc.T(withDoll ? "ev.catch.you_doll" : "ev.catch.you_out"), "blood", 2.6f);
                    else if (Un == me && !withDoll) ui.Banner(Loc.T("ev.catch.not_holder", Hh.name), "candle", 2);
                    else if (!withDoll) ui.Toast(Loc.T("ev.catch.toast", Hh.name, Loc.RoomIn(Map.RoomOf(x, y))));
                    break;
                }
                case EvType.End:
                {
                    bool unWin = v.a != 0;
                    Metrics.Log("round_end", ("round", G.r), ("unice_won", unWin), ("role", me == null ? "spectator" : me.IsUnice ? "unice" : "hider"),
                        ("won", me != null && unWin == me.IsUnice), ("caught", me != null && me.outOfRound), ("online", app.Online));
                    if (me != null) sfx.Play(unWin == me.IsUnice ? "win" : "lose");
                    if (!unWin) ui.Banner(Loc.T("ev.end.time"), "moss", 1.6f);
                    app.sound.heartI = 0;
                    break;
                }
                case EvType.Final:
                    if (me != null) Metrics.Log("match_end", ("rounds", G.rounds), ("score", me.score), ("place", 1 + G.ents.Count(e => e.score > me.score)), ("players", G.ents.Count), ("online", app.Online));
                    break;
                case EvType.Host:
                    if (!G.isHost || U.CleanName(v.s) != app.myName) ui.Toast(Loc.T("ev.host.other", U.CleanName(v.s)));
                    else ui.Toast(Loc.T("ev.host.you"));
                    break;
                case EvType.Left: { var e = E(v.a); if (e != null) ui.Toast(Loc.T("ev.left", e.name)); break; }
            }
        }
    }
}
