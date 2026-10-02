using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// What this player hears: sounds placed in the house (quieter with distance, panned, muffled through walls, with a cue
    /// and a caption when the source isn't visible), footsteps, the clock, the heartbeat near the Unice, the last-seconds tick.
    /// </summary>
    sealed class Soundscape
    {
        readonly App app;
        public float heartI, beat;
        float ownT, clockT, heartT; int tickS;
        readonly Dictionary<int, float> stepT = new Dictionary<int, float>();

        public Soundscape(App app) { this.app = app; }

        static string Side(float dx, float dy)
        {
            float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
            if (ax < 60 && ay < 60) return Loc.T("app.side.near");
            string h = Loc.T(dx < 0 ? "app.side.left" : "app.side.right"), v = Loc.T(dy < 0 ? "app.side.above" : "app.side.below");
            return ax > ay * 1.6f ? h : ay > ax * 1.6f ? v : v + ", " + h;
        }

        /// <summary>Plays a sound as heard from (x, y): quieter with distance, panned, muffled through walls. Returns the loudness.</summary>
        public float Hear(float x, float y, float radius, string label, Color col, string snd, float minG = 0)
        {
            if (!app.hasView) return 0;
            var me = app.me; var L = app.view; bool hid = me != null && me.hidden != null;
            float r = radius * (hid ? 1.3f : 1), d = U.Dist(x, y, L.x, L.y);
            float g = Mathf.Max(d >= r ? 0 : Mathf.Pow(1 - d / r, 2), minG);
            if (g <= .01f) return 0;
            app.sfx.Play(snd, g, Mathf.Clamp((x - L.x) / 300, -1, 1), d > 40 && !Map.SightLine(L.x, L.y, x, y));
            if (label != null && me != null && !me.outOfRound && !app.Seen(x, y))
            {
                app.ui.Cue(Mathf.Atan2(y - L.y, x - L.x), g, col);
                if (hid) app.ui.Caption($"{label} · {(g > .45f ? Loc.T("app.close.near") : g > .15f ? Loc.T("app.close.mid") : Loc.T("app.close.far"))} · {Side(x - L.x, y - L.y)}");
            }
            return g;
        }

        /// <summary>Per step while a round is live and not paused.</summary>
        public void Tick(float dt)
        {
            var G = app.G; var me = app.me;
            foreach (var e in G.ents)
            {
                if (e == me || e.outOfRound || e.hidden != null || !e.moving || e.x < 0) continue;
                stepT.TryGetValue(e.idx, out float st); st -= dt; stepT[e.idx] = st;
                if (st > 0) continue;
                if (e.IsUnice) { stepT[e.idx] = .45f; if (app.TeamView) Hear(e.x, e.y, 380, Loc.T("sound.cane"), Ui.Rose, "cane"); }
                else if (e.loud)
                {
                    stepT[e.idx] = .42f;
                    float g = Hear(e.x, e.y, Rules.NoiseR, Loc.T("sound.running"), Ui.Candle, "step");
                    if (g > 0 && me != null && me.IsUnice) app.world.Ripple(e.x, e.y, "step");
                }
            }
            if (me != null && me.loud) { ownT -= dt; if (ownT <= 0) { ownT = .42f; app.world.Ripple(me.x, me.y, "own"); } }
            clockT -= dt; if (clockT <= 0) { clockT = 1; Hear(1652, 182, 260, null, default, "clock"); }
            if (me != null) me.noise += ((me.loud ? 1 : 0) - me.noise) * Mathf.Min(1, dt * 6);
            var Un = G.unice;
            if (G.ph == Phase.Hunt && me != null && !me.IsUnice && !me.outOfRound && Un != null && Un.x > 0)
            {
                float d = U.Dist(me, Un); heartI = d < Rules.HeartR ? 1 - d / Rules.HeartR : 0;
                heartT -= dt;
                if (heartI > 0 && heartT <= 0) { heartT = .42f + (1 - heartI) * .7f; beat = 1; app.sfx.Play("heart"); }
            }
            else heartI = 0;
            if (G.ph == Phase.Hunt) { int s = Mathf.CeilToInt(G.tl); if (s <= 10 && s > 0 && s != tickS) { tickS = s; app.sfx.Play("tick"); } }
        }
    }
}
