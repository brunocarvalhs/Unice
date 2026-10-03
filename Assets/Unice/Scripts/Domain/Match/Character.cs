namespace Unice
{
    public enum Role { Hider, Unice }

    /// <summary>
    /// One child (or the Unice) in the match, played by a human or a bot. Positions are in house pixels.
    /// The bot-only state (path, goals, memory) lives in <see cref="brain"/>.
    /// </summary>
    public sealed class Character
    {
        // ---------- identity (kept between rounds) ----------
        /// <summary>Position in <see cref="Game.characters"/>; events and snapshots refer to characters by it.</summary>
        public int index;
        /// <summary>Network id of the human's device, or "b0".."b7" for bots.</summary>
        public string playerId;
        public string name;
        /// <summary>The look: one option per <see cref="Kit"/> slot.</summary>
        public int[] kit;
        /// <summary>Played by the host's AI (from the start, or because the human left).</summary>
        public bool isBot;
        public int score;
        /// <summary>How many rounds this character has been the Unice (the least-used goes next).</summary>
        public int uniceTurns;
        /// <summary>
        /// Increases whenever the host moves this character on its own (new round, the Unice entering, a lag spike).
        /// Devices drop any position they sent before seeing the new value, so the character doesn't jump back.
        /// </summary>
        public int teleportSeq;

        // ---------- round state ----------
        public float x, y;
        /// <summary>Guests: where the host last saw this character; it slides there every frame.</summary>
        public float targetX, targetY;
        /// <summary>Unit vector of where it is looking.</summary>
        public float faceX, faceY = 1;
        /// <summary>1 drawn facing right, -1 facing left.</summary>
        public int facingSign = 1;
        public Role role;
        /// <summary>Caught by the Unice this round: watches until the next one.</summary>
        public bool outOfRound;
        public bool moving, sneaking, talking;
        /// <summary>Running (not sneaking): the Unice can hear it.</summary>
        public bool loud;
        /// <summary>The furniture it is hiding in, or null.</summary>
        public Furniture hidingSpot;
        public float passCooldown, laughCooldown;
        /// <summary>Host: estimated speed of a remote player (from their position updates), to tell walking from running.</summary>
        public float speed;
        /// <summary>Host: seconds until its noise can alert the Unice bot again.</summary>
        public float noiseTimer;
        /// <summary>0..1, smoothed: how loud this device's player is (the HUD meter).</summary>
        public float noiseLevel;
        /// <summary>The Unice can't move for this long after catching someone who didn't have the doll.</summary>
        public float stunTime;
        /// <summary>The Unice bot slows down for this long after a long chase.</summary>
        public float tiredTime;

        /// <summary>Bot-only memory; reset whenever a human takes over or leaves.</summary>
        public readonly BotBrain brain = new BotBrain();

        public Character(int index, string playerId, string name, int[] kit, bool isBot)
        {
            this.index = index; this.playerId = playerId; this.name = name; this.kit = kit; this.isBot = isBot;
            ResetRound();
        }

        public bool IsUnice => role == Role.Unice;

        /// <summary>Back to the start-of-round state: a hider, off the map until placed, no cooldowns.</summary>
        public void ResetRound()
        {
            x = y = targetX = targetY = -600; faceX = 0; faceY = 1;
            role = Role.Hider; outOfRound = false; hidingSpot = null;
            passCooldown = 0; laughCooldown = 0;
            moving = sneaking = loud = talking = false; speed = 0;
            noiseTimer = noiseLevel = stunTime = tiredTime = 0;
            brain.ResetForRound();
        }

        /// <summary>Forget the bot's plans (a new host, or a human just became a bot).</summary>
        public void ResetBot() => brain.Reset();
    }
}
