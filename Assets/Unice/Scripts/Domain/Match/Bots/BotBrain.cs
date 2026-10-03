using System.Collections.Generic;

namespace Unice
{
    /// <summary>A hider bot is either looking for a spot to hide, or running away from the Unice.</summary>
    public enum HiderMode { Seek, Flee }

    /// <summary>What a bot remembers between steps: its path, goals and memory. Only the host uses it.</summary>
    public sealed class BotBrain
    {
        // ---------- movement ----------
        /// <summary>Waypoints still to walk, from <see cref="Navigation.FindPath"/>.</summary>
        public List<Point> path = new List<Point>();
        /// <summary>Seconds until the path is recomputed (while fleeing or chasing).</summary>
        public float repathTimer;
        /// <summary>Barely moving for this long counts as stuck.</summary>
        public float stuckTime;
        /// <summary>Times in a row it got stuck; it gives up on the path after a few.</summary>
        public int stuckCount;

        // ---------- hider ----------
        public HiderMode mode = HiderMode.Seek;
        /// <summary>The spot it is walking to (and has reserved).</summary>
        public Furniture targetSpot;
        /// <summary>The last spot it hid in this round; it avoids hiding there again.</summary>
        public Furniture previousSpot;
        /// <summary>Round clock when it last saw or heard the Unice.</summary>
        public float lastSawUniceAt = -99;
        /// <summary>Doll holder: round clock at which it leaves its spot after a giggle gave it away (0 = stay).</summary>
        public float leaveSpotAt;
        /// <summary>Runs (doesn't sneak) until this round clock, after leaving a spot.</summary>
        public float dashUntil;
        /// <summary>Hidden and the Unice came close: it already decided whether to bolt, until the Unice moves away.</summary>
        public bool startled;

        // ---------- Unice ----------
        /// <summary>Where it is going, and why.</summary>
        public BotGoal goal;
        /// <summary>A goal it will switch to at <see cref="reactAt"/> (reacting to a giggle takes a moment).</summary>
        public BotGoal pendingGoal;
        public float reactAt;
        /// <summary>The spot it is opening, and the seconds left until it is open.</summary>
        public Furniture checkingSpot;
        public float checkTimer;
        /// <summary>The hider it is chasing (seen, in reach), or null.</summary>
        public Character chaseTarget;
        /// <summary>Seconds of the current chase; a long chase makes it tired.</summary>
        public float chaseTime;
        /// <summary>After losing sight of its target, it goes to where it last saw it.</summary>
        public bool remembersTarget;
        public float rememberedX, rememberedY;
        /// <summary>Round clock of the last giggle it reacted to.</summary>
        public float lastGiggleHeardAt = -1;

        /// <summary>Start of a round: forget everything, including what it remembered from the last round.</summary>
        public void ResetForRound()
        {
            lastSawUniceAt = -99; dashUntil = 0; lastGiggleHeardAt = -1; previousSpot = null; chaseTime = 0;
            Reset();
        }

        /// <summary>Drop the current plans (keeps what it remembers about this round).</summary>
        public void Reset()
        {
            mode = HiderMode.Seek; path.Clear(); goal = null; pendingGoal = null; targetSpot = null; repathTimer = 0;
            chaseTarget = null; remembersTarget = false; checkTimer = 0; checkingSpot = null;
            stuckTime = 0; stuckCount = 0; startled = false; leaveSpotAt = 0;
        }
    }
}
