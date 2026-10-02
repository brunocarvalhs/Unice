namespace Unice
{
    /// <summary>How the last round ended, shown on the result screen. Character fields are indexes in <see cref="Game.characters"/>.</summary>
    public sealed class RoundResult
    {
        /// <summary>True when the Unice caught whoever had the doll; false when the time ran out.</summary>
        public bool uniceWon;
        /// <summary>Who had the doll at the end: the one caught (Unice won) or the one who kept it until time ran out.</summary>
        public int dollHolder = -1;
        /// <summary>Where the doll holder was hiding when time ran out, or -1 (caught, or not hidden).</summary>
        public int holderSpotIndex = -1;
        /// <summary>Who will be the Unice next round.</summary>
        public int nextUnice = -1;
    }
}
