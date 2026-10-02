using System.Linq;

namespace Unice
{
    public struct ViewInfo { public float x, y, r; public bool los, spect; }

    /// <summary>
    /// This device's point of view: where it looks from and how far it sees (its player, or whoever it spectates),
    /// what is visible from there, and how to call each character to this player ("você" vs. their name).
    /// </summary>
    sealed class Perspective
    {
        readonly App app;
        Game G => app.G;
        public ViewInfo view; public bool hasView;
        /// <summary>While spectating, who to follow (their pid) — null means auto (the doll holder). Self-healing: a stale pid just falls back.</summary>
        public string spectTarget;

        public Perspective(App app) { this.app = app; }

        /// <summary>Per step: follows the player (or the spectated one) once there's something to look at.</summary>
        public void Update() { if (app.me != null || G.ph != Phase.Home) { view = Compute(); hasView = true; } }

        /// <summary>False only for the Unice while a round is live: then the hiders' information (who has the doll, who is hidden) stays secret.</summary>
        public bool TeamView => app.me == null || app.me.outOfRound || !app.me.IsUnice || !G.Live;

        ViewInfo Compute()
        {
            var p = app.me;
            if (p == null || p.outOfRound)
            {
                Ent h = spectTarget != null ? G.ents.FirstOrDefault(e => e.pid == spectTarget && !e.outOfRound) : null;
                h ??= G.doll.holder;
                bool ok = h != null && h.x > 0;
                return new ViewInfo { x = ok ? h.x : 860, y = ok ? h.y : 590, r = Rules.ViewSpect, los = false, spect = true };
            }
            return new ViewInfo { x = p.x, y = p.y, r = p.hidden != null ? Rules.ViewHidden : p.IsUnice ? Rules.ViewUnice : Rules.ViewHider, los = true };
        }

        public bool Seen(float x, float y)
        {
            if (!hasView || U.Dist(x, y, view.x, view.y) > view.r) return false;
            return !view.los || Map.SightLine(view.x, view.y, x, y);
        }

        public string NameOf(Ent e) => e == null ? Loc.T("app.someone") : e == app.me ? Loc.T("app.you") : e.name;
        public string NameUp(Ent e) => e == null ? Loc.T("app.someone.up") : e == app.me ? Loc.T("app.you.up") : e.name;
    }
}
