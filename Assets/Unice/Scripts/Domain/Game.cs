using System;
using System.Collections.Generic;
using System.Linq;

namespace Unice
{
    public enum Phase : byte { Home, Lobby, Hide, Hunt, Result, Final }
    public enum EvType : byte { None, Start, Enter, Giggle, Pass, Open, Catch, End, Final, Host, Left, Lobby }
    public enum Act : byte { None, Hide, Exit, Pass, Open, Catch }

    public struct GameEvent
    {
        public int seq; public EvType type; public int a, b, c, d, e; public string s;
    }

    /// <summary>What a player tells everyone else about themselves (the web version's "presence").</summary>
    public struct Presence
    {
        public string name; public int kit; public long joinT; public bool rdy; public int tp;
        public bool hasPos; public float x, y, fx, fy; public bool sneak;
        public int aSeq; public Act aType; public int aArg;
    }

    public sealed class PeerInfo
    {
        public string id; public bool isMe; public Presence pres;
        public byte[] lastSnap; public int lastSnapEp; public float lastSnapAt = -999;
        public float presAt = -999;   // when their presence last arrived (stale = app paused or gone)
    }

    public sealed class DollState { public Ent holder; public float giggleT = Rules.GiggleEvery; public bool hasLast; public float lastX, lastY, lastT; public Furn lastSpot; }

    public static class Rules
    {
        public const float RoundTime = 120, HideTime = 30, ResultTime = 9, GiggleEvery = 10, PassCd = 5, PassR = 60, CatchR = 30, Stun = 2.5f;
        public const int MaxPlayers = 8;
        public const float SpdRun = 150, SpdSneak = 78, SpdUnice = 152, SpdTired = 112, SpdPatrol = 138, SpdUnicePlayer = 165;
        public const float ViewHider = 300, ViewHidden = 110, ViewUnice = 300, ViewSpect = 520;
        public const float AiSense = 330, AiHear = 200, AiCheck = 1, NoiseR = 270, HeartR = 280;
        public static readonly string[] BotNames = { "Bia", "Tito", "Maru", "Zeca", "Nina", "Otto", "Lia", "Caio" };
    }

    /// <summary>
    /// The match. The host runs the rules and the bots and publishes snapshots; everyone else mirrors them.
    /// A straight port of the web version so both play the same.
    /// </summary>
    public sealed partial class Game
    {
        public Phase ph = Phase.Home;
        public int ep, r, rounds = 5, bots = 3, evSeq;
        public float hb, tl, t;
        public readonly List<Ent> ents = new List<Ent>();
        public Ent unice, nextU;
        public readonly DollState doll = new DollState();
        public readonly List<GameEvent> evs = new List<GameEvent>();
        public int[] res;            // [unWin, whoIdx, spotIdx, nextIdx]
        public bool isHost;
        public readonly Dictionary<string, int> aseq = new Dictionary<string, int>();

        public bool Live => ph == Phase.Hide || ph == Phase.Hunt;
        public Ent EntAt(int i) => i >= 0 && i < ents.Count ? ents[i] : null;

        // ---------- events ----------
        public void Ev(EvType type, int a = 0, int b = 0, int c = 0, int d = 0, int e = 0, string s = null)
        {
            evSeq++;
            evs.Add(new GameEvent { seq = evSeq, type = type, a = a, b = b, c = c, d = d, e = e, s = s });
            if (evs.Count > 10) evs.RemoveAt(0);
        }

        // ---------- helpers ----------
        public static Furn NearSpotFurn(Ent e)
        {
            Furn b = null; float bd = 34;
            foreach (var s in Map.Spots) { float d = Map.RectDist(s, e.x, e.y); if (d < bd) { bd = d; b = s; } }
            return b;
        }
        Furn NearFreeSpot(Ent e, float r)
        {
            Furn b = null; float bd = r;
            foreach (var s in Map.Spots)
            {
                if (s.occ != null || (s.res != null && s.res != e)) continue;
                float d = U.Dist(s.ax, s.ay, e.x, e.y);
                if (d < bd && Map.WalkLine(e.x, e.y, s.ax, s.ay)) { bd = d; b = s; }
            }
            return b;
        }
        public void Hide(Ent e, Furn s)
        {
            if (s.res != null && s.res != e) s.res.goalSpot = null;
            s.res = null; s.occ = e; e.hidden = s; e.goalSpot = null; e.prevSpot = s;
            e.x = s.ax; e.y = s.ay; e.path.Clear(); e.moving = false; e.bolted = false;
        }
        public static void ExitSpot(Ent e) { if (e.hidden != null) { e.hidden.occ = null; e.hidden = null; } }
        public Ent PassTarget(Ent a, float slack = 0)
        {
            Ent b = null; float bd = Rules.PassR + slack;
            foreach (var e in ents) { if (e == a || e.IsUnice || e.outOfRound || e.hidden != null) continue; float d = U.Dist(a, e); if (d < bd) { bd = d; b = e; } }
            return b;
        }
        public bool CanMoveNow(Ent e) => Live && !e.outOfRound && e.hidden == null && !(e.IsUnice && (ph == Phase.Hide || e.stun > 0));

        // ---------- rules (host) ----------
        void PassDoll(Ent a, Ent b) { doll.holder = b; b.passCd = Rules.PassCd; a.passCd = 0; Ev(EvType.Pass, a.idx, b.idx); }

        void Capture(Ent u, Ent h)
        {
            int x = (int)MathF.Round(h.x), y = (int)MathF.Round(h.y);
            ExitSpot(h);
            if (h == doll.holder) { Ev(EvType.Catch, u.idx, h.idx, 1, x, y); EndRound(true, h); return; }
            h.outOfRound = true; u.score += 1; u.stun = Rules.Stun; u.target = null; u.goal = null; u.path.Clear(); u.checkT = 0;
            if (h.goalSpot != null && h.goalSpot.res == h) h.goalSpot.res = null;
            Ev(EvType.Catch, u.idx, h.idx, 0, x, y);
        }
        void OpenSpot(Ent u, Furn s) { s.checkedAt = t; Ev(EvType.Open, s.si, s.occ != null ? 1 : 0); if (s.occ != null) Capture(u, s.occ); }
        void Giggle()
        {
            var h = doll.holder;
            doll.giggleT = Rules.GiggleEvery; doll.hasLast = true; doll.lastX = h.x; doll.lastY = h.y; doll.lastT = t; doll.lastSpot = h.hidden;
            Ev(EvType.Giggle, (int)MathF.Round(h.x), (int)MathF.Round(h.y));
            if (h.bot) h.leaveAt = t + U.Rand(.3f, 1);
        }

        public void DoAction(Ent e, Act type, int arg)
        {
            if (!Live || e.outOfRound) return;
            if (!e.IsUnice)
            {
                if (type == Act.Hide) { var s = arg >= 0 && arg < Map.Spots.Length ? Map.Spots[arg] : null; if (s != null && e.hidden == null && s.occ == null && Map.RectDist(s, e.x, e.y) < 60) Hide(e, s); }
                else if (type == Act.Exit) ExitSpot(e);
                else if (type == Act.Pass && doll.holder == e && e.hidden == null && e.passCd <= 0) { var m = PassTarget(e, 20); if (m != null) PassDoll(e, m); }
            }
            else if (ph == Phase.Hunt && e.stun <= 0)
            {
                if (type == Act.Open) { var s = arg >= 0 && arg < Map.Spots.Length ? Map.Spots[arg] : null; if (s != null && Map.RectDist(s, e.x, e.y) < 60) OpenSpot(e, s); }
                else if (type == Act.Catch) { var h = EntAt(arg); if (h != null && !h.IsUnice && !h.outOfRound && h.hidden == null && U.Dist(e, h) < Rules.CatchR + 45) Capture(e, h); }
            }
        }

        public struct Human { public string id, name; public int[] k; public long joinT; }

        /// <summary>
        /// Who can be drawn for the Unice or the doll: with two or more people in the room only people,
        /// otherwise (playing alone with bots) everyone, so it is not always the same person.
        /// </summary>
        List<Ent> Eligible(List<Ent> from)
        {
            var people = from.Where(e => !e.bot).ToList();
            return ents.Count(e => !e.bot) >= 2 && people.Count > 0 ? people : from;
        }

        void AddHuman(Human h) { if (ents.Count < Rules.MaxPlayers) ents.Add(new Ent(ents.Count, h.id, h.name, h.k, false)); }

        /// <summary>Starts a match. <paramref name="humans"/> must list this device first, then the others by arrival.</summary>
        public void NewMatch(List<Human> humans)
        {
            ents.Clear();
            foreach (var h in humans) AddHuman(h);
            var taken = new HashSet<string>(ents.Select(e => e.name.ToLowerInvariant()));
            var names = Rules.BotNames.Where(n => !taken.Contains(n.ToLowerInvariant())).ToList(); U.Shuffle(names);
            for (int i = 0; (i < bots || ents.Count < 2) && ents.Count < Rules.MaxPlayers; i++)
                ents.Add(new Ent(ents.Count, "b" + i, i < names.Count ? names[i] : "Robô " + (i + 1), Kit.Random(), true));
            r = 0;
            nextU = U.Pick(Eligible(ents));
            StartRound(humans);
        }

        public void StartRound(List<Human> humansNow)
        {
            r++;
            foreach (var h in humansNow) if (!ents.Any(e => e.pid == h.id)) AddHuman(h);
            foreach (var e in ents)
            {
                e.ResetRound(); e.tp++;
                foreach (var h in humansNow) if (!e.bot && e.pid == h.id) { e.name = h.name; e.k = h.k; }
            }
            foreach (var s in Map.Spots) { s.occ = null; s.res = null; s.checkedAt = -99; }
            var u = nextU != null && ents.Contains(nextU) ? nextU : U.Pick(ents);
            u.role = Role.Unice; u.turns++; unice = u;
            var hs = ents.Where(e => e != u).ToList();
            for (int i = 0; i < hs.Count; i++) { hs[i].x = hs[i].tx = 620 + i * 70; hs[i].y = hs[i].ty = 474; }
            doll.holder = U.Pick(Eligible(hs)); doll.giggleT = Rules.GiggleEvery; doll.hasLast = false; doll.lastSpot = null;
            ph = Phase.Hide; tl = Rules.HideTime; t = 0; res = null;
            Ev(EvType.Start, r, u.idx, doll.holder.idx);
        }

        void EndRound(bool unWin, Ent caught)
        {
            if (!Live) return;
            var U_ = unice; var holder = doll.holder;
            if (unWin) { U_.score += 3; nextU = caught; res = new[] { 1, caught.idx, -1, 0 }; }
            else
            {
                foreach (var h in ents) if (!h.IsUnice) h.score += 2;
                holder.score += 1;
                var pool = Eligible(ents.Where(e => e != U_).ToList()); int minT = pool.Min(e => e.turns);
                nextU = U.Pick(pool.Where(e => e.turns == minT).ToList());
                res = new[] { 0, holder.idx, holder.hidden != null ? holder.hidden.si : -1, 0 };
            }
            res[3] = nextU.idx;
            ph = Phase.Result; tl = Rules.ResultTime;
            Ev(EvType.End, res[0]);
        }

        public void BackToLobby() { ph = Phase.Lobby; ents.Clear(); r = 0; unice = null; doll.holder = null; Ev(EvType.Lobby); }

        // ---------- movement ----------
        public static void MoveEnt(Ent e, float dx, float dy)
        {
            float nx = e.x + dx; if (Map.CanStand(nx, e.y)) e.x = nx;
            float ny = e.y + dy; if (Map.CanStand(e.x, ny)) e.y = ny;
        }
        bool FollowPath(Ent e, float dt, float speed)
        {
            if (e.path.Count == 0) { e.moving = false; return true; }
            if (e.path.Count > 1 && Map.WalkLine(e.x, e.y, e.path[1].x, e.path[1].y)) e.path.RemoveAt(0);
            var p = e.path[0]; float dx = p.x - e.x, dy = p.y - e.y, d = MathF.Sqrt(dx * dx + dy * dy), step = speed * dt;
            e.moving = true;
            if (d <= step) { if (Map.CanStand(p.x, p.y)) { e.x = p.x; e.y = p.y; } e.path.RemoveAt(0); return e.path.Count == 0; }
            float ux = dx / d, uy = dy / d, ox = e.x, oy = e.y;
            MoveEnt(e, ux * step, uy * step); e.fx = ux; e.fy = uy;
            if (U.Dist(e.x, e.y, ox, oy) < step * .3f)
            {
                e.stuckT += dt;
                if (e.stuckT > .5f)
                {
                    e.stuckT = 0; e.stuckN++;
                    var last = e.path[e.path.Count - 1];
                    e.path = e.stuckN > 4 ? new List<P>() : Map.FindPath(e.x, e.y, last.x, last.y);
                    if (e.path.Count == 0) { e.stuckN = 0; return true; }
                }
            }
            else e.stuckT = 0;
            return false;
        }

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

        // ---------- host tick ----------
        public void SimUpdate(float dt)
        {
            t += dt;
            if (ph == Phase.Hide)
            {
                tl -= dt;
                if (tl <= 0) { var u = unice; ph = Phase.Hunt; tl = Rules.RoundTime; u.x = Map.DoorIn.x; u.y = Map.DoorIn.y; u.fx = 0; u.fy = -1; u.tp++; Ev(EvType.Enter); }
            }
            else { tl -= dt; if (tl <= 0) { EndRound(false, null); return; } }
            foreach (var e in ents) { if (e.passCd > 0) e.passCd -= dt; if (e.stun > 0) e.stun -= dt; }
            foreach (var e in ents)
            {
                if (!e.bot) continue;
                if (e.IsUnice) UniceAI(e, dt); else HiderAI(e, dt);
                if (!Live) return;
                e.loud = !e.IsUnice && e.moving && !e.sneak;
            }
            foreach (var e in ents)
                if (!e.IsUnice && !e.outOfRound && e.hidden == null && e.loud) { e.noiseT -= dt; if (e.noiseT <= 0) { e.noiseT = .42f; AiHear(e); } }
            var U_ = unice;
            if (ph == Phase.Hunt && U_.stun <= 0 && U_.checkT <= 0)
                foreach (var h in ents) if (!h.IsUnice && !h.outOfRound && h.hidden == null && U.Dist(U_, h) < Rules.CatchR) { Capture(U_, h); if (!Live) return; break; }
            if (ph == Phase.Hunt && doll.holder.hidden != null) { doll.giggleT -= dt; if (doll.giggleT <= 0) Giggle(); }
        }

        /// <summary>Host only: apply what the other players sent (their position and their last action).</summary>
        public void ReadInputs(IReadOnlyList<PeerInfo> peers, string myId, float dt)
        {
            foreach (var e in ents)
            {
                if (e.bot || e.pid == myId) continue;
                PeerInfo p = null;
                foreach (var q in peers) if (q.id == e.pid) { p = q; break; }
                if (p == null) { e.bot = true; e.ResetAI(); if (Live) Ev(EvType.Left, e.idx); continue; }
                var pr = p.pres;
                if (pr.aSeq > (aseq.TryGetValue(p.id, out int last) ? last : 0)) { aseq[p.id] = pr.aSeq; DoAction(e, pr.aType, pr.aArg); }
                if (pr.hasPos && pr.tp >= e.tp && CanMoveNow(e))
                {
                    float d = U.Dist(pr.x, pr.y, e.x, e.y);
                    if (d < 260 && Map.CanStand(pr.x, pr.y)) { e.spd += (d / dt - e.spd) * .25f; e.x = pr.x; e.y = pr.y; }
                    else { e.spd *= .7f; e.tp++; }   // impossible jump (lag spike): send the player back to where the host has them
                    float l = MathF.Sqrt(pr.fx * pr.fx + pr.fy * pr.fy); if (l > .1f) { e.fx = pr.fx / l; e.fy = pr.fy / l; }
                    e.sneak = pr.sneak;
                }
                else e.spd *= .7f;
                e.moving = e.spd > 15; e.loud = !e.IsUnice && e.moving && !e.sneak && e.spd > 60;
            }
        }

        /// <summary>Host only: one fixed step of the whole match.</summary>
        public void HostStep(float dt, IReadOnlyList<PeerInfo> peers, string myId, Action<float> movePlayer, Func<List<Human>> humansNow)
        {
            hb += dt;
            if (peers != null) ReadInputs(peers, myId, dt);
            if (Live) { movePlayer?.Invoke(dt); SimUpdate(dt); }
            else if (ph == Phase.Result)
            {
                tl -= dt;
                if (tl <= 0)
                {
                    if (r >= rounds) { ph = Phase.Final; Ev(EvType.Final); }
                    else StartRound(humansNow());
                }
            }
        }

        /// <summary>A player who joined later takes over as host from the last snapshot.</summary>
        public void TakeOver(IReadOnlyList<PeerInfo> peers, Ent me, string myName)
        {
            isHost = true; ep += 1; aseq.Clear();
            foreach (var p in peers) aseq[p.id] = p.pres.aSeq;
            foreach (var e in ents)
            {
                if (e != me) { e.x = e.tx; e.y = e.ty; }
                bool alive = false; foreach (var p in peers) if (p.id == e.pid) alive = true;
                if (!e.bot && !alive) e.bot = true;
                e.ResetAI();
            }
            foreach (var s in Map.Spots) s.res = null;
            if (doll.hasLast) doll.lastSpot = doll.holder != null ? doll.holder.hidden : null;
            Ev(EvType.Host, s: myName);
        }
    }
}
