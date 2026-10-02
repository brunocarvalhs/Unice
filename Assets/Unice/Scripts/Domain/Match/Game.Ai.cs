using System;
using System.Collections.Generic;
using System.Linq;

namespace Unice
{
    /// <summary>The bots: hiders pick spots, flee and pass the doll; the Unice patrols, follows noise and opens furniture. Runs on the host only.</summary>
    public sealed partial class Game
    {
        // ---------- AI: hiders ----------
        bool Los(Ent a, Ent b) => Map.SightLine(a.x, a.y, b.x, b.y);

        void ChooseSpot(Ent h)
        {
            if (h.goalSpot != null && h.goalSpot.res == h) h.goalSpot.res = null;
            float ux = ph == Phase.Hide ? Map.DoorIn.x : unice.x, uy = ph == Phase.Hide ? Map.DoorIn.y : unice.y;
            Furn best = null; float bs = float.MinValue;
            foreach (var s in Map.Spots)
            {
                if (s.occ != null || (s.res != null && s.res != h)) continue;
                float sc = U.Dist(s.ax, s.ay, ux, uy) * .8f - U.Dist(s.ax, s.ay, h.x, h.y) * .55f + U.Rand(0, 140);
                if (doll.hasLast && U.Dist(s.ax, s.ay, doll.lastX, doll.lastY) < 350) sc -= 450;
                if (s == doll.lastSpot) sc -= 500;
                if (s == h.prevSpot) sc -= 700;
                if (sc > bs) { bs = sc; best = s; }
            }
            h.goalSpot = best;
            if (best != null) { best.res = h; h.path = Map.FindPath(h.x, h.y, best.ax, best.ay); }
        }
        P FleePoint(Ent h, Ent u)
        {
            var near = Map.Nodes.Where(n => { float dh = U.Dist(n.x, n.y, h.x, h.y); return dh > 80 && dh < 460; }).ToList();
            P best = default; bool found = false; float bs = float.MinValue;
            for (int i = 0; i < 20 && near.Count > 0; i++)
            {
                var n = U.Pick(near); float dh = U.Dist(n.x, n.y, h.x, h.y);
                float sc = U.Dist(n.x, n.y, u.x, u.y) - dh * .35f;
                if ((n.x - h.x) * (u.x - h.x) + (n.y - h.y) * (u.y - h.y) > 0) sc -= 300;
                if (sc > bs) { bs = sc; best = n; found = true; }
            }
            return found ? best : Map.Nodes[Map.NearestNode(h.x, h.y)];
        }
        void HiderAI(Ent h, float dt)
        {
            if (h.outOfRound) return;
            var u = unice; bool hunting = ph == Phase.Hunt; float d = U.Dist(h, u);
            bool sees = hunting && (d < Rules.AiHear || (d < Rules.AiSense && Los(h, u)));
            if (sees) h.lastSeenU = t;
            bool holder = doll.holder == h;
            if (holder && sees && d < 260 && h.hidden == null && h.passCd <= 0) { var m = PassTarget(h); if (m != null) PassDoll(h, m); }
            if (h.hidden != null)
            {
                h.moving = false;
                if (holder && h.leaveAt > 0 && t >= h.leaveAt) { h.leaveAt = 0; h.dashUntil = t + 2.5f; ExitSpot(h); h.st = "seek"; ChooseSpot(h); return; }
                if (hunting && d < 100 && !h.bolted)
                {
                    h.bolted = true;
                    if (U.Rand(0, 1) < .3f) { ExitSpot(h); h.st = "flee"; h.lastSeenU = t; h.path.Clear(); }
                }
                else if (d > 220) h.bolted = false;
                return;
            }
            if (sees) h.st = "flee";
            if (h.st == "flee")
            {
                if (t - h.lastSeenU > 1.4f) { h.st = "seek"; h.goalSpot = null; }
                else
                {
                    if (d > 200 && !Los(h, u)) { var s = NearFreeSpot(h, 70); if (s != null) { Hide(h, s); return; } }
                    h.repath -= dt;
                    if (h.repath <= 0 || h.path.Count == 0) { h.repath = 1.2f; var f = FleePoint(h, u); h.path = Map.FindPath(h.x, h.y, f.x, f.y); }
                    h.sneak = false; FollowPath(h, dt, Rules.SpdRun); return;
                }
            }
            if (h.goalSpot == null || h.goalSpot.occ != null || (h.goalSpot.res != null && h.goalSpot.res != h)) ChooseSpot(h);
            var sp = h.goalSpot;
            if (sp == null) { h.moving = false; return; }
            h.sneak = hunting && d < 380 && t > h.dashUntil;
            if (FollowPath(h, dt, h.sneak ? Rules.SpdSneak : Rules.SpdRun))
            {
                if (sp.occ == null && U.Dist(h.x, h.y, sp.ax, sp.ay) < 26) Hide(h, sp);
                else { if (sp.res == h) sp.res = null; h.goalSpot = null; }
            }
        }

        // ---------- AI: Unice ----------
        void SetGoal(Ent u, Goal g)
        {
            if (u.goal != null && u.goal.pri > g.pri) return;
            if (g.spot != null) { g.x = g.spot.ax; g.y = g.spot.ay; }
            u.goal = g; u.path = Map.FindPath(u.x, u.y, g.x, g.y);
        }
        Furn SpotNear(float x, float y, float r, float fresh)
        {
            Furn b = null; float bd = r;
            foreach (var s in Map.Spots) { if (t - s.checkedAt < fresh) continue; float d = U.Dist(s.ax, s.ay, x, y); if (d < bd) { bd = d; b = s; } }
            return b;
        }
        void Patrol(Ent u)
        {
            if (U.Rand(0, 1) < .45f) { var n = U.Pick(Map.Nodes); SetGoal(u, new Goal { kind = "pos", x = n.x, y = n.y, pri = 0, left = 0 }); return; }
            Furn best = null; float bs = float.MinValue;
            foreach (var s in Map.Spots) { float sc = MathF.Min(40, t - s.checkedAt) * 8 - U.Dist(s.ax, s.ay, u.x, u.y) * .45f + U.Rand(0, 70); if (sc > bs) { bs = sc; best = s; } }
            SetGoal(u, new Goal { kind = "spot", spot = best, pri = 0, left = 0 });
        }
        void ArriveU(Ent u)
        {
            var g = u.goal; if (g == null) return;
            if (g.kind == "spot" && U.Dist(u.x, u.y, g.spot.ax, g.spot.ay) < 34) { u.checkT = Rules.AiCheck; u.checkSpot = g.spot; return; }
            u.goal = null;
            var s = g.left > 0 ? SpotNear(g.x, g.y, 260, 6) : null;
            if (s != null) SetGoal(u, new Goal { kind = "spot", spot = s, pri = 1, left = g.left - 1, hasAround = true, ax = g.x, ay = g.y });
        }
        void ResolveCheck(Ent u, Furn s)
        {
            s.checkedAt = t;
            var g = u.goal; u.goal = null; u.checkSpot = null;
            Ev(EvType.Open, s.si, s.occ != null ? 1 : 0);
            if (s.occ != null) { Capture(u, s.occ); return; }
            if (g != null && g.left > 0)
            {
                float ax = g.hasAround ? g.ax : s.ax, ay = g.hasAround ? g.ay : s.ay;
                var n = SpotNear(ax, ay, 280, 6);
                if (n != null) SetGoal(u, new Goal { kind = "spot", spot = n, pri = 1, left = g.left - 1, hasAround = true, ax = ax, ay = ay });
            }
        }
        void UniceAI(Ent u, float dt)
        {
            u.moving = false;
            if (ph != Phase.Hunt) return;
            if (u.stun > 0) return;
            if (u.tired > 0) u.tired -= dt;
            if (u.checkT > 0) { u.checkT -= dt; if (u.checkT <= 0) ResolveCheck(u, u.checkSpot); return; }
            Ent tg = null; float tgd = float.MaxValue; bool keep = false;
            foreach (var h in ents)
            {
                if (h.IsUnice || h.outOfRound || h.hidden != null) continue;
                float d = U.Dist(u, h);
                if (d >= Rules.ViewUnice || !Los(u, h)) continue;
                if (h == u.target) keep = true;
                if (d < tgd) { tgd = d; tg = h; }
            }
            if (keep) tg = u.target;
            if (tg != null)
            {
                u.target = tg; u.hasMem = true; u.memX = tg.x; u.memY = tg.y; u.goal = null;
                u.chaseT += dt; if (u.chaseT > 5) { u.chaseT = 0; u.tired = 3; }
                u.repath -= dt;
                if (u.repath <= 0 || u.path.Count == 0) { u.repath = .3f; u.path = Map.FindPath(u.x, u.y, tg.x, tg.y); }
                FollowPath(u, dt, u.tired > 0 ? Rules.SpdTired : Rules.SpdUnice); return;
            }
            u.target = null; u.chaseT = MathF.Max(0, u.chaseT - dt * 2);
            if (u.hasMem) { SetGoal(u, new Goal { kind = "pos", x = u.memX, y = u.memY, pri = 2, left = 2 }); u.hasMem = false; }
            if (doll.hasLast && doll.lastT > u.giggleSeen)
            {
                u.giggleSeen = doll.lastT;
                u.pending = new Goal { kind = doll.lastSpot != null ? "spot" : "pos", spot = doll.lastSpot, x = doll.lastX, y = doll.lastY, pri = 3, left = 2, hasAround = true, ax = doll.lastX, ay = doll.lastY };
                u.reactAt = t + U.Rand(.3f, .7f);
            }
            if (u.pending != null && t >= u.reactAt) { var g = u.pending; u.pending = null; u.goal = null; SetGoal(u, g); }
            if (u.goal == null) Patrol(u);
            if (FollowPath(u, dt, u.goal != null && u.goal.pri >= 2 ? Rules.SpdUnice : Rules.SpdPatrol)) ArriveU(u);
        }
        void AiHear(Ent e)
        {
            var u = unice;
            if (!u.bot || ph != Phase.Hunt || U.Dist(e, u) >= Rules.NoiseR || u.target != null || u.checkT > 0) return;
            var g = u.goal;
            if (g == null || g.pri < 1 || U.Dist(g.x, g.y, e.x, e.y) > 90) SetGoal(u, new Goal { kind = "pos", x = e.x, y = e.y, pri = 1, left = 1 });
        }
    }
}
