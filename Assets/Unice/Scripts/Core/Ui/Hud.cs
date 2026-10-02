using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>In-game HUD: round/clock card, role card with the doll's giggle meter and noise, the house plan and player list.</summary>
    sealed class Hud : UiPart
    {
        public readonly VisualElement Root;

        /// <summary>Every 0.2 s while playing.</summary>
        public void Tick() { UpdateHud(); mini.MarkDirtyRepaint(); }
        public void Invalidate() => plSig = null;

        Label hRound, hClock, hPhase, hWhere, hRoom, hRole, hGoal, hDollWho, hDollNote, hNoiseTxt;
        VisualElement roleCard, gigWrap, gigFill, noiseBox, noiseFill, plist, plRows, mini;
        string plSig;

        public Hud(Ui ui) : base(ui)
        {
            Root = Ig(E(ui.safe, "layer")); var hud = Root;
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
            bool spectating = me == null || me.outOfRound;
            string sig = string.Join("|", G.ents.Select(e => $"{e.pid},{e.role},{e.outOfRound},{e.hidden != null},{e.moving},{e.sneak},{e == h},{e.score},{Kit.Pack(e.k)}")) + me?.pid + me?.role + G.ph + tv + Loc.Current + spectating + app.spectTarget;
            if (sig == plSig) return; plSig = sig;
            var hostP = app.Online ? app.FindHost() : null; string hostId = hostP != null ? hostP.id : app.Online ? null : app.myId;
            plRows.Clear();
            foreach (var e in G.ents)
            {
                string st = Loc.T(e.IsUnice ? "hud.st_unice" : e.outOfRound ? "hud.st_out" : tv ? (e.hidden != null ? "hud.st_hidden" : e.moving ? (e.sneak ? "hud.st_sneaking" : "hud.st_running") : "hud.st_still") : "hud.st_somewhere");
                var row = E(plRows, "plrow"); if (e.outOfRound) row.AddToClassList("plout");
                bool canWatch = spectating && !e.outOfRound;
                bool watching = spectating && (app.spectTarget != null ? app.spectTarget == e.pid : e == h);
                if (canWatch) { row.AddToClassList("plrow-pick"); row.RegisterCallback<PointerDownEvent>(_ => { app.spectTarget = e.pid; plSig = null; }); }
                else row.pickingMode = PickingMode.Ignore;
                row.EnableInClassList("plrow-on", watching);
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
            if (me != null && me.IsUnice) { if (G.doll.hasLast && G.t - G.doll.lastT < 14) Dot(G.doll.lastX, G.doll.lastY, 40 + Mathf.Sin(t * 6) * 10, null, Ui.Rose); }
            else if (me == null || me.outOfRound || me.hidden == null)
                foreach (var e in G.ents)
                {
                    if (e.IsUnice || e.outOfRound || e == me || e.x <= 0) continue;
                    var kc = Art.Hex(Kit.OutfitColor(e.k));
                    Dot(e.x, e.y, 22, e.hidden != null ? (Color?)null : kc, e.hidden != null ? kc : (Color?)null);
                    if (e == G.doll.holder) Dot(e.x, e.y, 42, null, Ui.Rose);
                }
            if (me != null && !me.outOfRound && me.x > 0)
            {
                Dot(me.x, me.y, 28, me.IsUnice ? (Color)new Color32(221, 214, 223, 255) : Color.white, me.IsUnice ? Ui.Rose : Art.Hex(Kit.OutfitColor(me.k)));
                if (!me.IsUnice && G.doll.holder == me) Dot(me.x, me.y, 46, null, Ui.Rose);
            }
        }
    }
}
