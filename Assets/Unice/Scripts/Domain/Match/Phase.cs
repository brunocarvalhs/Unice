namespace Unice
{
    /// <summary>Where the match is: Home → Lobby → (Hide → Hunt → Result) × rounds → Final.</summary>
    public enum Phase : byte
    {
        /// <summary>Main menu, not in a room.</summary>
        Home,
        /// <summary>Waiting room: players gather, the host sets the options.</summary>
        Lobby,
        /// <summary>Everyone hides while the Unice waits outside with eyes closed.</summary>
        Hide,
        /// <summary>The Unice is in the house looking for the doll.</summary>
        Hunt,
        /// <summary>A round just ended: who won, who is the next Unice.</summary>
        Result,
        /// <summary>All rounds played: the final scoreboard.</summary>
        Final,
    }
}
