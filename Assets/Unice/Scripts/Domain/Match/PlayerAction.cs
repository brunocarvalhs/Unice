namespace Unice
{
    /// <summary>What a player can ask the host to do (sent in their <see cref="Presence"/>).</summary>
    public enum PlayerAction : byte
    {
        None,
        /// <summary>Hide in a spot. Arg = spot index.</summary>
        Hide,
        /// <summary>Leave the hiding spot.</summary>
        Exit,
        /// <summary>Hand the doll to whoever is closest.</summary>
        Pass,
        /// <summary>Unice: open a hiding spot. Arg = spot index.</summary>
        Open,
        /// <summary>Unice: catch the character touched. Arg = character index.</summary>
        Catch,
        /// <summary>Laugh on purpose (gives the position away like the doll's giggle).</summary>
        Laugh,
    }
}
