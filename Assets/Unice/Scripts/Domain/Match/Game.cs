using System;
using System.Collections.Generic;
using System.Linq;

namespace Unice
{
    /// <summary>
    /// The match. The host runs the rules and the bots and publishes snapshots; everyone else mirrors them.
    /// A straight port of the web version so both play the same.
    /// </summary>
    public sealed partial class Game
    {
        public Phase ph = Phase.Home;
        public int ep, r, rounds = 5, bots = 3, evSeq;
        public float hb, tl, t;
        public string mapId = MapHouse.Id;
        // host-configurable match settings (defaults match Rules); synced to peers in the snapshot
        public float hideTime = Rules.HideTime, roundTime = Rules.RoundTime, giggleEvery = Rules.GiggleEvery;
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
            doll.giggleT = giggleEvery; doll.hasLast = true; doll.lastX = h.x; doll.lastY = h.y; doll.lastT = t; doll.lastSpot = h.hidden;
            Ev(EvType.Giggle, (int)MathF.Round(h.x), (int)MathF.Round(h.y), h.idx);
            if (h.bot) h.leaveAt = t + U.Rand(.3f, 1);
        }
        /// <summary>Anyone can laugh on purpose; the Unice hears it exactly like the doll's own giggle.</summary>
        void Laugh(Ent e)
        {
            e.laughCd = Rules.LaughCd;
            doll.hasLast = true; doll.lastX = e.x; doll.lastY = e.y; doll.lastT = t; doll.lastSpot = e.hidden;
            if (doll.holder == e) doll.giggleT = giggleEvery;
            Ev(EvType.Giggle, (int)MathF.Round(e.x), (int)MathF.Round(e.y), e.idx);
        }

        public void DoAction(Ent e, Act type, int arg)
        {
            if (!Live || e.outOfRound) return;
            if (!e.IsUnice)
            {
                if (type == Act.Hide) { var s = arg >= 0 && arg < Map.Spots.Length ? Map.Spots[arg] : null; if (s != null && e.hidden == null && s.occ == null && Map.RectDist(s, e.x, e.y) < 60) Hide(e, s); }
                else if (type == Act.Exit) ExitSpot(e);
                else if (type == Act.Pass && doll.holder == e && e.passCd <= 0) { var m = PassTarget(e, 20); if (m != null) PassDoll(e, m); }
                else if (type == Act.Laugh && e.laughCd <= 0) Laugh(e);
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
            if (Map.Id != mapId) Map.Load(Maps.ById(mapId));
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
            for (int i = 0; i < hs.Count; i++) { hs[i].x = hs[i].tx = Map.SpawnX + i * 70; hs[i].y = hs[i].ty = Map.SpawnY; }
            doll.holder = U.Pick(Eligible(hs)); doll.giggleT = giggleEvery; doll.hasLast = false; doll.lastSpot = null;
            ph = Phase.Hide; tl = hideTime; t = 0; res = null;
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

        // ---------- host tick ----------
        public void SimUpdate(float dt)
        {
            t += dt;
            if (ph == Phase.Hide)
            {
                tl -= dt;
                if (tl <= 0) { var u = unice; ph = Phase.Hunt; tl = roundTime; u.x = Map.DoorIn.x; u.y = Map.DoorIn.y; u.fx = 0; u.fy = -1; u.tp++; Ev(EvType.Enter); }
            }
            else { tl -= dt; if (tl <= 0) { EndRound(false, null); return; } }
            foreach (var e in ents) { if (e.passCd > 0) e.passCd -= dt; if (e.laughCd > 0) e.laughCd -= dt; if (e.stun > 0) e.stun -= dt; }
            foreach (var e in ents)
            {
                if (!e.bot) continue;
                if (e.IsUnice) UniceAI(e, dt); else HiderAI(e, dt);
                if (!Live) return;
                e.loud = !e.IsUnice && e.moving && !e.sneak;
            }
            foreach (var e in ents)
                if (!e.IsUnice && !e.outOfRound && e.hidden == null && (e.loud || e.talking)) { e.noiseT -= dt; if (e.noiseT <= 0) { e.noiseT = .42f; AiHear(e); } }
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
                e.talking = pr.talking;
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
