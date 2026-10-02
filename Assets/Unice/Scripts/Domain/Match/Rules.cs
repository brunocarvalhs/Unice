namespace Unice
{
    /// <summary>The tuning numbers of the game: times in seconds, distances and speeds in house pixels. Same as the web version.</summary>
    public static class Rules
    {
        // ---------- round timing (the host can change hide, hunt and giggle times in the waiting room) ----------
        public const float HideTime = 30;
        public const float HuntTime = 120;
        /// <summary>How long the result screen shows before the next round.</summary>
        public const float ResultTime = 9;
        /// <summary>The doll giggles this often while its holder is hidden.</summary>
        public const float GiggleInterval = 10;

        public const int MaxPlayers = 8;
        public static readonly string[] BotNames = { "Bia", "Tito", "Maru", "Zeca", "Nina", "Otto", "Lia", "Caio" };

        // ---------- actions ----------
        public const float PassCooldown = 5;
        public const float LaughCooldown = 5;
        /// <summary>The doll can be passed to someone this close.</summary>
        public const float PassRange = 60;
        /// <summary>The Unice catches whoever it gets this close to.</summary>
        public const float CatchRange = 30;
        /// <summary>The Unice can't move for this long after catching someone without the doll.</summary>
        public const float StunTime = 2.5f;

        // ---------- speeds ----------
        public const float RunSpeed = 150;
        public const float SneakSpeed = 78;
        public const float UnicePlayerSpeed = 165;
        public const float UniceChaseSpeed = 152;
        public const float UniceTiredSpeed = 112;
        public const float UnicePatrolSpeed = 138;

        // ---------- how far each one sees ----------
        public const float HiderViewRange = 300;
        /// <summary>Peeking out of a hiding spot.</summary>
        public const float HiddenViewRange = 110;
        public const float UniceViewRange = 300;
        public const float SpectatorViewRange = 520;

        // ---------- bots and noise ----------
        /// <summary>A hider bot sees the Unice this far (with a clear line of sight)...</summary>
        public const float BotSightRange = 330;
        /// <summary>...and hears it this far, walls or not.</summary>
        public const float BotHearingRange = 200;
        /// <summary>How long the Unice bot takes to open a spot.</summary>
        public const float BotSpotCheckTime = 1;
        /// <summary>Running or talking within this distance of the Unice gets heard.</summary>
        public const float NoiseRange = 270;
        /// <summary>A hider hears its heartbeat when the Unice is closer than this.</summary>
        public const float HeartbeatRange = 280;
    }
}
