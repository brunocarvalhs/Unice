using System.Collections.Generic;

namespace Unice
{
    public enum Role { Hider, Unice }

    public sealed class Goal
    {
        public string kind;     // "spot" or "pos"
        public Furn spot;
        public float x, y;
        public int pri, left;
        public bool hasAround; public float ax, ay;
    }

    /// <summary>One child (or the Unice) in the match: a human or a bot. Positions are in house pixels.</summary>
    public sealed class Ent
    {
        public int idx;
        public string pid;          // network id of the human, or "b0".."b7" for bots
        public string name;
        public int[] k;
        public bool bot;
        public int score, turns, tp;

        // round state
        public float x, y, tx, ty, fx, fy = 1;
        public Role role;
        public bool outOfRound, moving, sneak, loud, talking;
        public Furn hidden;
        public float passCd, laughCd, spd, noiseT, noise, stun, tired, chaseT;
        public int face = 1;        // 1 facing right, -1 facing left (drawing)

        // AI state (host only)
        public string st = "seek";
        public List<P> path = new List<P>();
        public Goal goal, pending;
        public Furn goalSpot, checkSpot, prevSpot;
        public float repath, checkT, lastSeenU = -99, leaveAt, dashUntil, stuckT, reactAt, giggleSeen = -1;
        public int stuckN;
        public bool bolted, hasMem;
        public float memX, memY;
        public Ent target;

        public Ent(int idx, string pid, string name, int[] k, bool bot) { this.idx = idx; this.pid = pid; this.name = name; this.k = k; this.bot = bot; ResetRound(); }

        public bool IsUnice => role == Role.Unice;

        public void ResetRound()
        {
            x = y = tx = ty = -600; fx = 0; fy = 1; role = Role.Hider; outOfRound = false; hidden = null; passCd = 0; laughCd = 0; moving = sneak = loud = talking = false; spd = 0;
            noiseT = noise = stun = tired = chaseT = 0;
            lastSeenU = -99; leaveAt = 0; dashUntil = 0; giggleSeen = -1; prevSpot = null;
            ResetAI();
        }
        public void ResetAI()
        {
            st = "seek"; path.Clear(); goal = null; goalSpot = null; repath = 0; target = null; hasMem = false; checkT = 0; checkSpot = null; pending = null;
            stuckT = 0; stuckN = 0; bolted = false; leaveAt = 0;
        }
    }
}
