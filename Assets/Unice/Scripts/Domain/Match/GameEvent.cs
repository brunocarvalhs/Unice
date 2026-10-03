namespace Unice
{
    /// <summary>What happened. The comment on each value says which <see cref="GameEvent"/> fields it uses.</summary>
    public enum GameEventType : byte
    {
        None,
        /// <summary>A round started. value = round number, actor = the Unice, target = who got the doll.</summary>
        Start,
        /// <summary>The hiding time is over: the Unice comes through the front door.</summary>
        Enter,
        /// <summary>The doll giggled, or someone laughed on purpose. actor = who, x/y = where.</summary>
        Giggle,
        /// <summary>The doll changed hands. actor = who gave it, target = who got it.</summary>
        Pass,
        /// <summary>The Unice opened a hiding spot. actor = the Unice, value = spot index, target = who was inside (-1 if empty).</summary>
        Open,
        /// <summary>The Unice caught someone. actor = the Unice, target = who was caught, value = 1 if they had the doll, x/y = where.</summary>
        Catch,
        /// <summary>The round ended. value = 1 if the Unice won (got the doll back), 0 if time ran out.</summary>
        End,
        /// <summary>The last round ended: the final scoreboard.</summary>
        Final,
        /// <summary>Someone else took over as host. text = their name.</summary>
        Host,
        /// <summary>A player left the room; a bot plays in their place. actor = who.</summary>
        Left,
        /// <summary>The host went back to the waiting room.</summary>
        Lobby,
    }

    /// <summary>
    /// Something that happened in the match. The host appends events; every device (host included) plays each one once,
    /// as banners, sounds and effects. Which fields are filled depends on <see cref="type"/>; unused character fields are -1.
    /// </summary>
    public struct GameEvent
    {
        /// <summary>Increasing number, so a device knows which events it already played.</summary>
        public int sequence;
        public GameEventType type;
        /// <summary>Index (in <see cref="Game.characters"/>) of who did it, or -1.</summary>
        public int actor;
        /// <summary>Index of who it happened to, or -1.</summary>
        public int target;
        /// <summary>A number whose meaning depends on <see cref="type"/> (round, spot index, a 0/1 flag).</summary>
        public int value;
        /// <summary>Where it happened, in house pixels (Giggle, Catch).</summary>
        public int x, y;
        /// <summary>A name (Host).</summary>
        public string text;
    }
}
